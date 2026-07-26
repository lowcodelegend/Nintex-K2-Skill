import { spawn } from "node:child_process";
import { mkdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";

function argument(name, fallback = undefined) {
  const index = process.argv.indexOf(`--${name}`);
  return index >= 0 ? process.argv[index + 1] : fallback;
}

function argumentsFor(name) {
  const flag = `--${name}`;
  const values = [];
  for (let index = 0; index < process.argv.length; index += 1) {
    if (process.argv[index] === flag && index + 1 < process.argv.length) {
      values.push(process.argv[index + 1]);
    }
  }
  return values;
}

const url = argument("url");
const output = path.resolve(argument("output"));
const profile = path.resolve(argument("profile"));
const width = Number(argument("width", "1440"));
const height = Number(argument("height", "1000"));
const port = Number(argument("port", "9333"));
const settleMilliseconds = Number(argument("settle", "3000"));
const trustedAuthHost = argument("trusted-auth-host", "");
const clickNames = argumentsFor("click-name");
const clickName = clickNames[0] || "";
const dismissDialogs = process.argv.includes("--dismiss-dialogs");
const paletteProbeText = argument("palette-probe-text", "");
const noScreenshot = process.argv.includes("--no-screenshot");
const edge = argument(
  "edge",
  "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
);

if (!url || !output || !profile || !Number.isInteger(width) || !Number.isInteger(height)) {
  throw new Error("Required: --url, --output, --profile, --width, and --height.");
}

const delay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

async function fetchJson(endpoint, options = {}) {
  const response = await fetch(endpoint, options);
  if (!response.ok) throw new Error(`${options.method || "GET"} ${endpoint}: HTTP ${response.status}`);
  return response.json();
}

async function waitForDevTools() {
  let lastError;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      return await fetchJson(`http://127.0.0.1:${port}/json/version`);
    } catch (error) {
      lastError = error;
      await delay(100);
    }
  }
  throw new Error(`Edge DevTools endpoint did not start: ${lastError?.message || "unknown error"}`);
}

class CdpSession {
  constructor(webSocketUrl) {
    this.nextId = 0;
    this.pending = new Map();
    this.diagnostics = [];
    this.networkPending = new Map();
    this.socket = new WebSocket(webSocketUrl);
  }

  async connect() {
    await new Promise((resolve, reject) => {
      this.socket.addEventListener("open", resolve, { once: true });
      this.socket.addEventListener("error", reject, { once: true });
    });
    this.socket.addEventListener("message", (event) => {
      const message = JSON.parse(String(event.data));
      if (message.id && this.pending.has(message.id)) {
        const pending = this.pending.get(message.id);
        this.pending.delete(message.id);
        if (message.error) pending.reject(new Error(message.error.message));
        else pending.resolve(message.result);
        return;
      }
      if (message.method === "Log.entryAdded" || message.method === "Runtime.exceptionThrown") {
        this.diagnostics.push(message);
      } else if (message.method === "Network.requestWillBeSent") {
        this.networkPending.set(message.params.requestId, {
          url: message.params.request.url,
          type: message.params.type
        });
      } else if (message.method === "Network.loadingFinished" || message.method === "Network.loadingFailed") {
        this.networkPending.delete(message.params.requestId);
        if (message.method === "Network.loadingFailed") this.diagnostics.push(message);
      }
    });
  }

  send(method, params = {}, timeoutMilliseconds = 15000) {
    const id = ++this.nextId;
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`CDP ${method} timed out after ${timeoutMilliseconds} ms.`));
      }, timeoutMilliseconds);
      this.pending.set(id, {
        resolve: (value) => {
          clearTimeout(timeout);
          resolve(value);
        },
        reject: (error) => {
          clearTimeout(timeout);
          reject(error);
        }
      });
      this.socket.send(JSON.stringify({ id, method, params }));
    });
  }

  close() {
    try {
      this.socket.close();
    } catch {
      // The browser may already have closed the target.
    }
  }
}

function diagnosticText(message) {
  const entry = message.params?.entry;
  const exception = message.params?.exceptionDetails;
  return {
    method: message.method,
    text: entry?.text || exception?.exception?.description || exception?.text ||
      message.params?.errorText || "",
    url: entry?.url || exception?.url || message.params?.requestId || null
  };
}

await mkdir(path.dirname(output), { recursive: true });
await mkdir(profile, { recursive: true });

const edgeArguments = [
  "--headless=new",
  "--disable-gpu",
  "--disable-breakpad",
  "--disable-component-extensions-with-background-pages",
  "--disable-extensions",
  "--disable-background-networking",
  "--disable-component-update",
  "--disable-default-apps",
  "--disable-sync",
  "--no-default-browser-check",
  "--mute-audio",
  "--no-sandbox",
  "--js-flags=--max-old-space-size=64",
  "--no-first-run",
  `--remote-debugging-port=${port}`,
  `--user-data-dir=${profile}`
];
if (trustedAuthHost) {
  edgeArguments.push(`--auth-server-allowlist=${trustedAuthHost}`);
  edgeArguments.push(`--auth-negotiate-delegate-allowlist=${trustedAuthHost}`);
}
edgeArguments.push("about:blank");

const browser = spawn(edge, edgeArguments, {
  detached: false,
  stdio: "ignore",
  windowsHide: true
});
let session;

try {
  await waitForDevTools();
  const target = await fetchJson(
    `http://127.0.0.1:${port}/json/new?${encodeURIComponent("about:blank")}`,
    { method: "PUT" }
  );
  session = new CdpSession(target.webSocketDebuggerUrl);
  await session.connect();
  await session.send("Page.enable");
  await session.send("Runtime.enable");
  await session.send("Log.enable");
  await session.send("Network.enable");
  await session.send("Emulation.setDeviceMetricsOverride", {
    width,
    height,
    deviceScaleFactor: 1,
    mobile: width < 600,
    screenWidth: width,
    screenHeight: height
  });
  await session.send("Page.navigate", { url });

  let readyState = "loading";
  const readyDeadline = Date.now() + 15000;
  while (Date.now() < readyDeadline) {
    try {
      const result = await session.send("Runtime.evaluate", {
        expression: "document.readyState",
        returnByValue: true
      }, 2000);
      readyState = result?.result?.value || readyState;
      if (readyState === "interactive" || readyState === "complete") break;
    } catch {
      // K2 replaces the execution context during navigation; retry until the deadline.
    }
    await delay(100);
  }
  await delay(settleMilliseconds);

  let clickProbe = null;
  const clickProbes = [];
  for (const requestedClickName of clickNames) {
    const beforeClick = await session.send("Runtime.evaluate", {
      expression: `(() => {
        const requestedName = ${JSON.stringify(requestedClickName)};
        const root = document.querySelector('[name="' +
          (window.CSS && CSS.escape ? CSS.escape(requestedName) : requestedName.replace(/"/g, '\\\\"')) +
          '"]');
        const target = root && (
          root.matches('button,input,a') ? root :
          root.querySelector('button,input,a') || root
        );
        const tabs = Array.from(document.querySelectorAll('ul.tab-box-tabs a.tab'));
        const selectedTab = tabs.findIndex((tab) => tab.classList.contains('selected'));
        if (!root || !target) return {
          requestedName, found: false, selectedTabBefore: selectedTab
        };
        const rectangle = target.getBoundingClientRect();
        const allowed = target.dispatchEvent(new MouseEvent('click', {
          bubbles: true,
          cancelable: true,
          view: window,
          clientX: rectangle.left + rectangle.width / 2,
          clientY: rectangle.top + rectangle.height / 2
        }));
        return {
          requestedName,
          found: true,
          targetTag: target.tagName,
          targetName: target.getAttribute('name') || root.getAttribute('name') || '',
          selectedTabBefore: selectedTab,
          dispatchCanceled: !allowed
        };
      })()`,
      returnByValue: true
    }, 5000);
    clickProbe = beforeClick.result.value;
    clickProbes.push(clickProbe);
    await delay(700);
    if (dismissDialogs) {
      const dismissed = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const visible = (node) => !!node &&
            getComputedStyle(node).display !== 'none' &&
            getComputedStyle(node).visibility !== 'hidden' &&
            !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
          const dialogs = Array.from(document.querySelectorAll(
            '.popup,.dialog,.message-box,[role="dialog"]'
          )).filter(visible);
          const actions = dialogs.flatMap((dialog) =>
            Array.from(dialog.querySelectorAll('a,button,input[type="button"]'))
          ).filter(visible);
          const target = actions.find((node) =>
            /^(ok|close|continue)$/i.test((node.textContent || node.value || '').trim())
          ) || actions[actions.length - 1];
          if (!target) return { found: false };
          target.dispatchEvent(new MouseEvent('click', {
            bubbles: true, cancelable: true, view: window
          }));
          return {
            found: true,
            text: (target.textContent || target.value || '').trim()
          };
        })()`,
        returnByValue: true
      }, 5000);
      clickProbe.dialogDismissal = dismissed.result.value;
      await delay(500);
    }
  }

  let paletteProbe = null;
  if (paletteProbeText) {
    const opened = await session.send("Runtime.evaluate", {
      expression: `(() => {
        const control = document.querySelector('northstar-command-palette');
        const row = control && control.closest('.k2sp-command-palette-row');
        const panel = control && control.closest('.formpanel');
        const trigger = control && control.shadowRoot &&
          control.shadowRoot.querySelector('.northstar-command__trigger');
        const visible = (node) => !!node &&
          getComputedStyle(node).display !== 'none' &&
          getComputedStyle(node).visibility !== 'hidden' &&
          !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
        if (!control || !trigger) return {
          found: false,
          controlVisible: visible(control),
          rowVisible: visible(row),
          panelDisplay: panel ? getComputedStyle(panel).display : null,
          rowClass: row ? row.className : null,
          panelClass: panel ? panel.className : null,
          panelStyle: panel ? panel.getAttribute('style') : null,
          panelParentClass: panel && panel.parentElement ? panel.parentElement.className : null,
          directGuidedPanel: !!panel &&
            !!panel.parentElement?.classList.contains('k2sp-guided-journey')
        };
        trigger.click();
        return {
          found: true,
          controlVisible: visible(control),
          rowVisible: visible(row),
          panelDisplay: panel ? getComputedStyle(panel).display : null,
          triggerVisible: visible(trigger),
          rowClass: row ? row.className : null,
          panelClass: panel ? panel.className : null,
          panelStyle: panel ? panel.getAttribute('style') : null,
          panelParentClass: panel && panel.parentElement ? panel.parentElement.className : null,
          directGuidedPanel: !!panel &&
            !!panel.parentElement?.classList.contains('k2sp-guided-journey')
        };
      })()`,
      returnByValue: true
    }, 5000);
    paletteProbe = opened.result.value;
    await delay(150);
    if (paletteProbe.found) {
      const typed = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const control = document.querySelector('northstar-command-palette');
          const input = control && control.shadowRoot &&
            control.shadowRoot.querySelector('input[type="search"]');
          if (!input) return { inputFound: false };
          input.value = ${JSON.stringify(paletteProbeText)};
          input.dispatchEvent(new Event('input', { bubbles: true }));
          return { inputFound: true };
        })()`,
        returnByValue: true
      }, 5000);
      Object.assign(paletteProbe, typed.result.value);
      await delay(350);
      const result = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const control = document.querySelector('northstar-command-palette');
          const root = control && control.shadowRoot;
          const input = root && root.querySelector('input[type="search"]');
          const dialog = root && root.querySelector('[role="dialog"]');
          const options = root ? root.querySelectorAll('[role="option"]') : [];
          const status = root && root.querySelector('[role="status"]');
          return {
            dialogVisible: !!dialog,
            inputValue: input ? input.value : null,
            focused: !!input && root.activeElement === input,
            optionCount: options.length,
            statusText: status ? (status.textContent || '').trim() : ''
          };
        })()`,
        returnByValue: true
      }, 5000);
      Object.assign(paletteProbe, result.result.value);
    }
  }

  if (!noScreenshot) {
    const capture = await session.send("Page.captureScreenshot", {
      format: "png",
      captureBeyondViewport: false,
      fromSurface: true
    }, 30000);
    await writeFile(output, Buffer.from(capture.data, "base64"));
  }

  const evaluated = await session.send("Runtime.evaluate", {
    expression: `({
      clickProbe: ${JSON.stringify(clickProbe)},
      clickProbes: ${JSON.stringify(clickProbes)},
      commandPaletteProbe: ${JSON.stringify(paletteProbe)},
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
      url: location.href,
      title: document.title,
      textLength: (document.body && document.body.innerText || "").trim().length,
      readyState: document.readyState,
      shellCount: document.querySelectorAll("#k2sp-shell").length,
      northstarVersion: window.K2SP_NORTHSTAR_CONFIG && window.K2SP_NORTHSTAR_CONFIG.version || null,
      northstarReady: !!(window.__k2spNorthstar && window.__k2spNorthstar.stylesReady),
      contentReady: !!(document.body && document.body.classList.contains("k2sp-ready")),
      h1Count: Array.from(document.querySelectorAll("h1"))
        .filter((heading) => !!(heading.offsetWidth || heading.offsetHeight || heading.getClientRects().length)).length,
      totalH1Count: document.querySelectorAll("h1").length,
      headings: Array.from(document.querySelectorAll("h1")).map((heading) => ({
        text: (heading.textContent || "").trim(),
        className: heading.className || "",
        id: heading.id || "",
        visible: !!(heading.offsetWidth || heading.offsetHeight || heading.getClientRects().length)
      })),
      bodyClass: document.body ? document.body.className : "",
      northstarScripts: Array.from(document.scripts)
        .map((script) => script.src)
        .filter((source) => /NorthstarAssets/i.test(source)),
      northstarStyles: Array.from(document.querySelectorAll('link[rel="stylesheet"]'))
        .map((link) => link.href)
        .filter((source) => /NorthstarAssets/i.test(source)),
      navigationItems: Array.from(document.querySelectorAll("#k2sp-shell .k2sp-nav-item"))
        .map((item) => {
          const label = item.querySelector(".k2sp-nav-text") || item;
          const icon = item.querySelector(".k2sp-nav-icon");
          return {
            text: (label.textContent || "").replace(/\\s+/g, " ").trim(),
            active: item.classList.contains("active"),
            textColor: getComputedStyle(label).color,
            iconColor: icon ? getComputedStyle(icon).color : null,
            backgroundColor: getComputedStyle(item).backgroundColor
          };
        }),
      validationFeedback: (() => {
        const visible = (node) => {
          if (!node || !node.isConnected) return false;
          const style = getComputedStyle(node);
          return style.display !== "none" && style.visibility !== "hidden" &&
            !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
        };
        const activePanel = Array.from(document.querySelectorAll(
          ".k2sp-guided-journey .formpanel"
        )).find(visible) || null;
        const selector = [
          "input.invalid", "textarea.invalid", "select.invalid",
          ".input-control.invalid", ".file-wrapper.invalid",
          ".SourceCode-Forms-Controls-Web-Label.invalid",
          ".SourceCode-Forms-Controls-Web-DataLabel.invalid"
        ].join(",");
        const focusTarget = (node) => node && (
          node.matches("input,textarea,select,button,a,[tabindex]") ? node :
          node.querySelector("input,textarea,select,button,a,[tabindex]")
        );
        const seen = new Set();
        const invalid = Array.from(activePanel ? activePanel.querySelectorAll(selector) : [])
          .filter((node) => {
            if (!visible(node) || node.closest(".tooltip.validation")) return false;
            const nested = node.querySelector && node.querySelector(selector);
            if (nested && visible(nested)) return false;
            const target = focusTarget(node);
            const rectangle = (target || node).getBoundingClientRect();
            const key = target?.id || target?.getAttribute("name") || node.id ||
              node.getAttribute("name") ||
              Math.round(rectangle.top) + ":" + Math.round(rectangle.left);
            if (seen.has(key)) return false;
            seen.add(key);
            return true;
          });
        const summaries = Array.from(document.querySelectorAll(".k2sp-validation-summary"))
          .filter(visible);
        const details = invalid.map((node) => {
          const target = focusTarget(node);
          const treatment = node.matches(".input-control,.file-wrapper") ? node :
            node.closest(".input-control,.file-wrapper") || node;
          const style = getComputedStyle(treatment);
          const targetStyle = getComputedStyle(target || node);
          const visibleTreatment =
            (style.outlineStyle !== "none" && parseFloat(style.outlineWidth) > 0) ||
            style.boxShadow !== "none" ||
            targetStyle.boxShadow !== "none" ||
            /rgb\\((?:180|185|217|240),\\s*(?:35|45|38),\\s*(?:24|32|38)\\)/.test(
              style.borderColor + " " + targetStyle.borderColor
            );
          const rectangle = (target || node).getBoundingClientRect();
          return {
            name: target?.getAttribute("name") || node.getAttribute("name") || "",
            id: target?.id || node.id || "",
            tag: (target || node).tagName,
            ariaInvalid: target?.getAttribute("aria-invalid") || "",
            visibleTreatment,
            borderColor: style.borderColor,
            backgroundColor: style.backgroundColor,
            outline: style.outline,
            boxShadow: style.boxShadow,
            top: Math.round(rectangle.top),
            left: Math.round(rectangle.left)
          };
        });
        const tabs = Array.from(document.querySelectorAll("ul.tab-box-tabs a.tab"));
        const firstTarget = focusTarget(invalid[0]);
        return {
          requestedClick: ${JSON.stringify(clickName)},
          invalidCount: invalid.length,
          visibleTreatmentCount: details.filter((item) => item.visibleTreatment).length,
          ariaInvalidCount: details.filter((item) => item.ariaInvalid === "true").length,
          summaryVisible: summaries.length === 1,
          summaryText: summaries[0] ?
            (summaries[0].textContent || "").replace(/\\s+/g, " ").trim() : "",
          summaryInvalidCount: summaries[0] ?
            Number(summaries[0].getAttribute("data-invalid-count") || 0) : 0,
          focusedName: document.activeElement?.getAttribute("name") || "",
          firstInvalidFocused: !!firstTarget && document.activeElement === firstTarget,
          selectedTabAfter: tabs.findIndex((tab) => tab.classList.contains("selected")),
          compatibility: window.__k2spNorthstar?.validationCompatibility || null,
          feedbackState: window.__k2spNorthstar?.validationFeedback || null,
          controls: details
        };
      })(),
      customControls: Array.from(document.querySelectorAll("northstar-command-palette,northstar-dashboard-widget"))
        .map((control) => ({
          tag: control.tagName.toLowerCase(),
          name: control.getAttribute("name") || control.Name || "",
          variant: control.Variant || "",
          dataCount: Array.isArray(control.Data) ? control.Data.length : Array.isArray(control.Suggestions) ? control.Suggestions.length : -1,
          hasShadow: !!control.shadowRoot,
          shadowTextLength: control.shadowRoot ? (control.shadowRoot.textContent || "").trim().length : 0,
          width: Math.round(control.getBoundingClientRect().width),
          height: Math.round(control.getBoundingClientRect().height)
        })),
      customControlRuntime: {
        baseType: typeof window.K2BaseControl,
        paletteDefined: !!window.customElements.get("northstar-command-palette"),
        dashboardDefined: !!window.customElements.get("northstar-dashboard-widget"),
        scripts: Array.from(document.scripts).slice(-12).map((script) => ({
          source: script.src,
          async: script.async,
          defer: script.defer,
          readyState: script.readyState || ""
        }))
      },
      slowResources: performance.getEntriesByType("resource")
        .filter((entry) => entry.duration > 1000 || /custom|control-runtime|northstar-dashboard/i.test(entry.name))
        .map((entry) => ({
          name: entry.name,
          initiatorType: entry.initiatorType,
          duration: Math.round(entry.duration),
          transferSize: entry.transferSize
        })),
      viewTitles: Array.from(document.querySelectorAll("[data-sf-title]"))
        .map((node) => node.getAttribute("data-sf-title"))
        .filter(Boolean),
      guidedJourney: (() => {
        const named = Array.from(document.querySelectorAll(
          '[name^="prgJourneyStep"],[name^="lblJourneyStepHeading"],[name^="dlbJourneyStepDescription"],[name^="btnJourney"],[name^="tblJourney"]'
        ));
        const tabs = document.querySelector("ul.tab-box-tabs");
        const tabBox = document.querySelector(".tab-box.form-tabs");
        const describe = (node) => {
          if (!node) return null;
          const rectangle = node.getBoundingClientRect();
          return {
            tag: node.tagName,
            id: node.id || "",
            name: node.getAttribute("name") || "",
            className: typeof node.className === "string" ? node.className : "",
            style: node.getAttribute("style") || "",
            text: (node.textContent || "").replace(/\s+/g, " ").trim().slice(0, 240),
            visible: !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length),
            left: Math.round(rectangle.left),
            top: Math.round(rectangle.top),
            width: Math.round(rectangle.width),
            height: Math.round(rectangle.height)
          };
        };
        return {
          controls: named.map((node) => ({
            ...describe(node),
            ancestors: [node.parentElement, node.parentElement?.parentElement,
              node.closest(".editor-cell"), node.closest(".row"), node.closest(".formpanel")]
              .map(describe)
          })),
          tabs: describe(tabs),
          tabAnchors: tabs ? Array.from(tabs.querySelectorAll("a")).map((anchor) => {
            const indicator = anchor.querySelector(".k2sp-step-number");
            const tick = indicator && getComputedStyle(indicator, "::after");
            return {
              ...describe(anchor),
              stepState: anchor.getAttribute("data-k2sp-step-state") || "",
              stepLocked: anchor.getAttribute("data-k2sp-step-locked") || "",
              ariaDisabled: anchor.getAttribute("aria-disabled") || "",
              parent: describe(anchor.parentElement),
              indicator: indicator ? {
                ...describe(indicator),
                display: getComputedStyle(indicator).display,
                alignItems: getComputedStyle(indicator).alignItems,
                justifyContent: getComputedStyle(indicator).justifyContent,
                fontSize: getComputedStyle(indicator).fontSize,
                tickContent: tick.content,
                tickPosition: tick.position,
                tickTop: tick.top,
                tickLeft: tick.left,
                tickWidth: tick.width,
                tickHeight: tick.height,
                tickTransform: tick.transform,
                tickBorderBottomWidth: tick.borderBottomWidth,
                tickBorderLeftWidth: tick.borderLeftWidth
              } : null
            };
          }) : [],
          directAdvanceProbe: (() => {
            const anchors = tabs ? Array.from(tabs.querySelectorAll("a.tab")) : [];
            if (anchors.length < 2 || !anchors[0].classList.contains("selected")) return null;
            const selectedBefore = anchors.findIndex((anchor) => anchor.classList.contains("selected"));
            const dispatchAllowed = anchors[1].dispatchEvent(new MouseEvent("click", {
              bubbles: true,
              cancelable: true,
              view: window
            }));
            const selectedAfter = anchors.findIndex((anchor) => anchor.classList.contains("selected"));
            return {
              selectedBefore,
              selectedAfter,
              dispatchCanceled: !dispatchAllowed,
              blocked: selectedBefore === 0 && selectedAfter === 0 && !dispatchAllowed
            };
          })(),
          fieldControls: Array.from(document.querySelectorAll(
            '.formpanel input,.formpanel textarea,.formpanel select,.formpanel [contenteditable="true"]'
          )).slice(0, 20).map((control) => ({
            ...describe(control),
            type: control.getAttribute("type") || "",
            placeholder: control.getAttribute("placeholder") || "",
            parent: describe(control.parentElement),
            grandparent: describe(control.parentElement?.parentElement)
          })),
          actionRows: Array.from(document.querySelectorAll(
            '[name^="tblJourneyActions"],[name="tblMasterDetailSave"],' +
            '[name="tblWorkflowStart"],[name="tblPreFillActions"]'
          )).map((table) => {
            const cells = Array.from(table.querySelectorAll(".editor-cell"));
            const buttons = Array.from(table.querySelectorAll(
              '[name^="btnJourneyBack"],[name^="btnJourneyContinue"],' +
              '[name="btnSave"],[name="btnFinishDraft"],[name^="btnSubmit"],' +
              '[name="btnPreFill"]'
            ));
            return {
              ...describe(table),
              panelIndex: Array.from(document.querySelectorAll(
                ".k2sp-guided-journey .formpanel"
              )).indexOf(table.closest(".formpanel")),
              cells: cells.map((cell) => ({
                ...describe(cell),
                textAlign: getComputedStyle(cell).textAlign
              })),
              buttons: buttons.map((button) => ({
                ...describe(button),
                backgroundColor: getComputedStyle(button).backgroundColor,
                color: getComputedStyle(button).color,
                borderColor: getComputedStyle(button).borderColor,
                cellTextAlign: button.closest(".editor-cell") ?
                  getComputedStyle(button.closest(".editor-cell")).textAlign : ""
              }))
            };
          }),
          preFill: (() => {
            const control = document.querySelector('[name="btnPreFill"]');
            const panels = Array.from(document.querySelectorAll(
              ".k2sp-guided-journey .formpanel"
            ));
            return control ? {
              ...describe(control),
              panelIndex: panels.indexOf(control.closest(".formpanel"))
            } : null;
          })(),
          tabBox: describe(tabBox),
          form: describe(document.querySelector(".runtime-form .form") || document.querySelector(".form"))
        };
      })(),
      frameworkProbe: (() => {
        const label = Array.from(document.querySelectorAll("label, span, div"))
          .find((node) => node.children.length === 0 && node.textContent.trim() === "User:");
        const ancestors = [];
        for (let node = label; node && ancestors.length < 12; node = node.parentElement) {
          ancestors.push({
            tag: node.tagName,
            id: node.id || "",
            className: typeof node.className === "string" ? node.className : "",
            role: node.getAttribute("role") || "",
            name: node.getAttribute("name") || ""
          });
        }
        return ancestors;
      })(),
      regions: [
        ".k2sp-page-intro",
        ".k2sp-insight",
        ".runtime-form",
        ".form",
        ".k2sp-application-content",
        ".k2sp-kpis",
        '.panel[name="Header"]'
      ].map((selector) => {
        const node = document.querySelector(selector);
        if (!node) return { selector, found: false };
        const rectangle = node.getBoundingClientRect();
        return {
          selector,
          found: true,
          display: getComputedStyle(node).display,
          position: getComputedStyle(node).position,
          zoom: getComputedStyle(node).zoom || "1",
          paddingTop: getComputedStyle(node).paddingTop,
          top: Math.round(rectangle.top),
          bottom: Math.round(rectangle.bottom),
          height: Math.round(rectangle.height)
        };
      }),
      kpiCells: Array.from(
        document.querySelectorAll(".k2sp-kpi-native-grid > .k2sp-kpi-cell")
      ).map((node) => {
        const control = node.querySelector("[name]");
        const cellRectangle = node.getBoundingClientRect();
        const controlRectangle = control?.getBoundingClientRect();
        return {
          className: node.className,
          controlName: control?.getAttribute("name") || "",
          gridRow: getComputedStyle(node).gridRow,
          gridColumn: getComputedStyle(node).gridColumn,
          textAlign: getComputedStyle(node).textAlign,
          left: Math.round(cellRectangle.left),
          width: Math.round(cellRectangle.width),
          controlLeft: controlRectangle ? Math.round(controlRectangle.left) : null,
          controlWidth: controlRectangle ? Math.round(controlRectangle.width) : null
        };
      })
    })`,
    returnByValue: true
  }, 5000);

  process.stdout.write(JSON.stringify({
    layout: evaluated.result.value,
    diagnostics: session.diagnostics.map(diagnosticText),
    networkPending: Array.from(session.networkPending.values())
  }));
} finally {
  if (session) {
    await session.send("Browser.close", {}, 2000).catch(() => {});
  }
  session?.close();
  if (!browser.killed) browser.kill();
  await delay(250);
  await rm(profile, { recursive: true, force: true }).catch(() => {});
}
