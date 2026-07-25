import { spawn } from "node:child_process";
import { mkdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";

function argument(name, fallback = undefined) {
  const index = process.argv.indexOf(`--${name}`);
  return index >= 0 ? process.argv[index + 1] : fallback;
}

const url = argument("url");
const output = path.resolve(argument("output"));
const profile = path.resolve(argument("profile"));
const width = Number(argument("width", "1440"));
const height = Number(argument("height", "1000"));
const port = Number(argument("port", "9333"));
const settleMilliseconds = Number(argument("settle", "3000"));
const trustedAuthHost = argument("trusted-auth-host", "");
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
