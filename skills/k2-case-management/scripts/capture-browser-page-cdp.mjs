import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { mkdir, rm, writeFile } from "node:fs/promises";
import net from "node:net";
import path from "node:path";

class DependencyFreeWebSocket {
  constructor(address) {
    this.address = new URL(address);
    this.listeners = new Map();
    this.buffer = Buffer.alloc(0);
    this.handshakeComplete = false;
    this.socket = net.createConnection({
      host: this.address.hostname,
      port: Number(this.address.port || 80)
    });
    this.socket.on("connect", () => {
      const key = randomBytes(16).toString("base64");
      this.socket.write([
        `GET ${this.address.pathname}${this.address.search} HTTP/1.1`,
        `Host: ${this.address.host}`,
        "Upgrade: websocket",
        "Connection: Upgrade",
        `Sec-WebSocket-Key: ${key}`,
        "Sec-WebSocket-Version: 13",
        "\r\n"
      ].join("\r\n"));
    });
    this.socket.on("data", (chunk) => this.receive(chunk));
    this.socket.on("error", (error) => this.emit("error", error));
    this.socket.on("close", () => this.emit("close", {}));
  }

  addEventListener(type, listener, options = {}) {
    const entries = this.listeners.get(type) || [];
    entries.push({ listener, once: options?.once === true });
    this.listeners.set(type, entries);
  }

  emit(type, event) {
    const entries = (this.listeners.get(type) || []).slice();
    for (const entry of entries) {
      entry.listener(event);
      if (entry.once) {
        const current = this.listeners.get(type) || [];
        this.listeners.set(type, current.filter((candidate) => candidate !== entry));
      }
    }
  }

  receive(chunk) {
    this.buffer = Buffer.concat([this.buffer, chunk]);
    if (!this.handshakeComplete) {
      const boundary = this.buffer.indexOf("\r\n\r\n");
      if (boundary < 0) return;
      const headers = this.buffer.subarray(0, boundary).toString("utf8");
      if (!/^HTTP\/1\.1 101\b/.test(headers)) {
        this.emit("error", new Error(`WebSocket upgrade failed: ${headers.split("\r\n")[0]}`));
        this.socket.destroy();
        return;
      }
      this.buffer = this.buffer.subarray(boundary + 4);
      this.handshakeComplete = true;
      this.emit("open", {});
    }
    this.readFrames();
  }

  readFrames() {
    while (this.buffer.length >= 2) {
      const first = this.buffer[0];
      const second = this.buffer[1];
      const opcode = first & 0x0f;
      const masked = (second & 0x80) !== 0;
      let length = second & 0x7f;
      let offset = 2;
      if (length === 126) {
        if (this.buffer.length < 4) return;
        length = this.buffer.readUInt16BE(2);
        offset = 4;
      } else if (length === 127) {
        if (this.buffer.length < 10) return;
        const longLength = this.buffer.readBigUInt64BE(2);
        if (longLength > BigInt(Number.MAX_SAFE_INTEGER)) {
          this.emit("error", new Error("WebSocket frame exceeds the safe buffer size."));
          this.socket.destroy();
          return;
        }
        length = Number(longLength);
        offset = 10;
      }
      const maskLength = masked ? 4 : 0;
      if (this.buffer.length < offset + maskLength + length) return;
      const mask = masked ? this.buffer.subarray(offset, offset + 4) : null;
      offset += maskLength;
      const payload = Buffer.from(this.buffer.subarray(offset, offset + length));
      this.buffer = this.buffer.subarray(offset + length);
      if (mask) {
        for (let index = 0; index < payload.length; index += 1) {
          payload[index] ^= mask[index % 4];
        }
      }
      if (opcode === 0x1) this.emit("message", { data: payload.toString("utf8") });
      else if (opcode === 0x8) {
        this.socket.end();
        return;
      } else if (opcode === 0x9) {
        this.writeFrame(payload, 0x0a);
      }
    }
  }

  writeFrame(payload, opcode = 0x1) {
    payload = Buffer.isBuffer(payload) ? payload : Buffer.from(String(payload), "utf8");
    const mask = randomBytes(4);
    let header;
    if (payload.length < 126) {
      header = Buffer.alloc(2);
      header[1] = 0x80 | payload.length;
    } else if (payload.length <= 0xffff) {
      header = Buffer.alloc(4);
      header[1] = 0x80 | 126;
      header.writeUInt16BE(payload.length, 2);
    } else {
      header = Buffer.alloc(10);
      header[1] = 0x80 | 127;
      header.writeBigUInt64BE(BigInt(payload.length), 2);
    }
    header[0] = 0x80 | opcode;
    const masked = Buffer.alloc(payload.length);
    for (let index = 0; index < payload.length; index += 1) {
      masked[index] = payload[index] ^ mask[index % 4];
    }
    this.socket.write(Buffer.concat([header, mask, masked]));
  }

  send(value) {
    if (!this.handshakeComplete) throw new Error("WebSocket is not open.");
    this.writeFrame(value);
  }

  close() {
    if (!this.socket.destroyed) {
      if (this.handshakeComplete) this.writeFrame(Buffer.alloc(0), 0x08);
      this.socket.end();
    }
  }
}

const WebSocketClient = globalThis.WebSocket || DependencyFreeWebSocket;

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
const assistantBehaviorRequested = process.argv.includes("--assistant-probe");
const assistantOpenProbeRequested = process.argv.includes("--assistant-open-probe");
const assistantLiveMessage = argument("assistant-live-message", "");
const assistantProbeRequested = assistantBehaviorRequested ||
  assistantOpenProbeRequested ||
  assistantLiveMessage.length > 0;
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
    this.socket = new WebSocketClient(webSocketUrl);
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
        return {
          requestedName,
          found: true,
          targetTag: target.tagName,
          targetName: target.getAttribute('name') || root.getAttribute('name') || '',
          selectedTabBefore: selectedTab,
          x: rectangle.left + rectangle.width / 2,
          y: rectangle.top + rectangle.height / 2,
          width: rectangle.width,
          height: rectangle.height
        };
      })()`,
      returnByValue: true
    }, 5000);
    clickProbe = beforeClick.result.value;
    clickProbes.push(clickProbe);
    if (clickProbe.found && clickProbe.width > 0 && clickProbe.height > 0) {
      await session.send("Input.dispatchMouseEvent", {
        type: "mouseMoved", x: clickProbe.x, y: clickProbe.y
      });
      await session.send("Input.dispatchMouseEvent", {
        type: "mousePressed", x: clickProbe.x, y: clickProbe.y,
        button: "left", clickCount: 1
      });
      await session.send("Input.dispatchMouseEvent", {
        type: "mouseReleased", x: clickProbe.x, y: clickProbe.y,
        button: "left", clickCount: 1
      });
      clickProbe.inputMethod = "CDP.Input.dispatchMouseEvent";
    }
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
    const located = await session.send("Runtime.evaluate", {
      expression: `(() => {
        const visible = (node) => !!node &&
          getComputedStyle(node).display !== 'none' &&
          getComputedStyle(node).visibility !== 'hidden' &&
          !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
        const controls = Array.from(document.querySelectorAll(
          'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
        ));
        const control = controls.find((candidate) => {
          const candidateTrigger = candidate.shadowRoot &&
            candidate.shadowRoot.querySelector('.northstar-command__trigger');
          return visible(candidate) && visible(candidateTrigger);
        }) || null;
        const row = control && control.closest('.k2sp-command-palette-row');
        const panel = control && control.closest('.formpanel');
        const trigger = control && control.shadowRoot &&
          control.shadowRoot.querySelector('.northstar-command__trigger');
        const host = document.querySelector('.k2sp-command-palette-host');
        const fallback = host && host.querySelector('.k2sp-search-fallback');
        if (!control || !trigger) return {
          found: false,
          controlVisible: visible(control),
          rowVisible: visible(row),
          fallbackVisible: visible(fallback),
          shellContract: window.__k2spNorthstar?.commandPalette || null,
          panelDisplay: panel ? getComputedStyle(panel).display : null,
          rowClass: row ? row.className : null,
          panelClass: panel ? panel.className : null,
          panelStyle: panel ? panel.getAttribute('style') : null,
          panelParentClass: panel && panel.parentElement ? panel.parentElement.className : null,
          directGuidedPanel: !!panel &&
            !!panel.parentElement?.classList.contains('k2sp-guided-journey')
        };
        const triggerRect = trigger.getBoundingClientRect();
        const rowRect = row && row.getBoundingClientRect();
        const hostRect = host && host.getBoundingClientRect();
        return {
          found: true,
          x: triggerRect.left + triggerRect.width / 2,
          y: triggerRect.top + triggerRect.height / 2,
          width: triggerRect.width,
          height: triggerRect.height,
          inputMethod: 'CDP.Input.dispatchMouseEvent',
          controlVisible: visible(control),
          rowVisible: visible(row),
          fallbackVisible: visible(fallback),
          fallbackHidden: !visible(fallback) &&
            (!fallback || fallback.hidden || fallback.getAttribute('aria-hidden') === 'true'),
          visuallyHosted: !!rowRect && !!hostRect &&
            Math.abs(rowRect.left - hostRect.left) <= 2 &&
            Math.abs(rowRect.top - hostRect.top) <= 2 &&
            Math.abs(rowRect.width - hostRect.width) <= 2,
          ownershipPreserved: !!row && !!host && !host.contains(row),
          shellContract: window.__k2spNorthstar?.commandPalette || null,
          panelDisplay: panel ? getComputedStyle(panel).display : null,
          triggerVisible: visible(trigger),
          controlName: control.getAttribute('name') || control.Name || '',
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
    paletteProbe = located.result.value;
    if (paletteProbe.found) {
      await session.send("Input.dispatchMouseEvent", {
        type: "mouseMoved", x: paletteProbe.x, y: paletteProbe.y
      });
      await session.send("Input.dispatchMouseEvent", {
        type: "mousePressed", x: paletteProbe.x, y: paletteProbe.y,
        button: "left", clickCount: 1
      });
      await session.send("Input.dispatchMouseEvent", {
        type: "mouseReleased", x: paletteProbe.x, y: paletteProbe.y,
        button: "left", clickCount: 1
      });
      await delay(200);
      const afterPointer = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const controls = Array.from(document.querySelectorAll(
            'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
          ));
          const open = controls.filter((control) => control.shadowRoot &&
            control.shadowRoot.querySelectorAll('[role="dialog"]').length === 1);
          return {
            pointerDialogCount: open.reduce((count, control) =>
              count + control.shadowRoot.querySelectorAll('[role="dialog"]').length, 0),
            pointerControlNames: open.map((control) =>
              control.getAttribute('name') || control.Name || '')
          };
        })()`,
        returnByValue: true
      }, 5000);
      Object.assign(paletteProbe, afterPointer.result.value);

      await session.send("Input.dispatchKeyEvent", {
        type: "keyDown", key: "Escape", code: "Escape", windowsVirtualKeyCode: 27
      });
      await session.send("Input.dispatchKeyEvent", {
        type: "keyUp", key: "Escape", code: "Escape", windowsVirtualKeyCode: 27
      });
      await delay(100);
      const afterClose = await session.send("Runtime.evaluate", {
        expression: `({
          dialogCountAfterPointerEscape: Array.from(
            document.querySelectorAll(
              'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
            )
          ).reduce((count, control) => count +
            (control.shadowRoot?.querySelectorAll('[role="dialog"]').length || 0), 0)
        })`,
        returnByValue: true
      }, 5000);
      Object.assign(paletteProbe, afterClose.result.value);

      await session.send("Input.dispatchKeyEvent", {
        type: "keyDown", key: "k", code: "KeyK",
        windowsVirtualKeyCode: 75, modifiers: 2
      });
      await session.send("Input.dispatchKeyEvent", {
        type: "keyUp", key: "k", code: "KeyK",
        windowsVirtualKeyCode: 75, modifiers: 2
      });
      await delay(200);
      const afterKeyboard = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const controls = Array.from(document.querySelectorAll(
            'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
          ));
          const open = controls.filter((control) => control.shadowRoot &&
            control.shadowRoot.querySelectorAll('[role="dialog"]').length === 1);
          const input = open[0]?.shadowRoot?.querySelector('input[type="search"]') || null;
          if (input) input.focus();
          return {
            keyboardDialogCount: open.reduce((count, control) =>
              count + control.shadowRoot.querySelectorAll('[role="dialog"]').length, 0),
            keyboardControlNames: open.map((control) =>
              control.getAttribute('name') || control.Name || ''),
            sameControl:
              open.length === 1 &&
              open[0] &&
              (open[0].getAttribute('name') || open[0].Name || '') ===
                ${JSON.stringify(paletteProbe.controlName)},
            inputFound: !!input
          };
        })()`,
        returnByValue: true
      }, 5000);
      Object.assign(paletteProbe, afterKeyboard.result.value);
      if (paletteProbe.inputFound) {
        await session.send("Input.insertText", { text: paletteProbeText });
      }
      await delay(350);
      const result = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const control = Array.from(document.querySelectorAll(
            'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
          )).find((candidate) => candidate.shadowRoot &&
            candidate.shadowRoot.querySelector('[role="dialog"]')) || null;
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
            statusText: status ? (status.textContent || '').trim() : '',
            passed:
              !!dialog &&
              !!input &&
              root.activeElement === input &&
              ${JSON.stringify(paletteProbe.pointerDialogCount)} === 1 &&
              ${JSON.stringify(paletteProbe.dialogCountAfterPointerEscape)} === 0 &&
              ${JSON.stringify(paletteProbe.keyboardDialogCount)} === 1 &&
              ${JSON.stringify(paletteProbe.sameControl)} === true &&
              ${JSON.stringify(paletteProbe.visuallyHosted)} === true &&
              ${JSON.stringify(paletteProbe.fallbackHidden)} === true
          };
        })()`,
        returnByValue: true
      }, 5000);
      Object.assign(paletteProbe, result.result.value);
    }
  }

  let assistantProbe = null;
  if (assistantProbeRequested) {
    const before = await session.send("Runtime.evaluate", {
      expression: `(() => {
        const control = Array.from(document.querySelectorAll(
          'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
        )).find((candidate) => candidate.shadowRoot &&
          candidate.shadowRoot.querySelector('[role="dialog"]')) || null;
        const option = control && Array.from(
          control.shadowRoot.querySelectorAll('[role="option"]')
        ).find((candidate) => /assistant/i.test(candidate.textContent || ''));
        const rectangle = option && option.getBoundingClientRect();
        const sidebar = document.querySelector('#k2sp-shell .k2sp-sidebar');
        return {
          optionFound: !!option,
          x: rectangle ? rectangle.left + rectangle.width / 2 : 0,
          y: rectangle ? rectangle.top + rectangle.height / 2 : 0,
          width: rectangle ? rectangle.width : 0,
          height: rectangle ? rectangle.height : 0,
          before: {
            clientWidth: document.documentElement.clientWidth,
            scrollWidth: document.documentElement.scrollWidth,
            clientHeight: document.documentElement.clientHeight,
            scrollHeight: document.documentElement.scrollHeight,
            scrollX: window.scrollX,
            scrollY: window.scrollY,
            sidebarWidth: sidebar ? sidebar.getBoundingClientRect().width : null
          }
        };
      })()`,
      returnByValue: true
    }, 5000);
    assistantProbe = before.result.value;
    if (assistantProbe.optionFound && assistantProbe.width > 0 && assistantProbe.height > 0) {
      await session.send("Input.dispatchMouseEvent", {
        type: "mouseMoved", x: assistantProbe.x, y: assistantProbe.y
      });
      await session.send("Input.dispatchMouseEvent", {
        type: "mousePressed", x: assistantProbe.x, y: assistantProbe.y,
        button: "left", clickCount: 1
      });
      await session.send("Input.dispatchMouseEvent", {
        type: "mouseReleased", x: assistantProbe.x, y: assistantProbe.y,
        button: "left", clickCount: 1
      });
      for (let attempt = 0; attempt < 100; attempt += 1) {
        const readiness = await session.send("Runtime.evaluate", {
          expression: `(() => {
            const control = Array.from(document.querySelectorAll(
              'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
            )).find((candidate) => candidate._assistantState === 'error');
            const overlay = document.querySelector('.northstar-agent-overlay');
            const portal = overlay && overlay.querySelector('.northstar-agent-portal');
            return {
              overlayReady: !!overlay && (
                !!overlay.querySelector('langflow-chat') ||
                !!overlay.querySelector('[role="alert"]') ||
                !!(portal && portal.querySelector('.northstar-agent-portal__session'))
              ),
              componentError: !!control,
              componentMessage: control ? control._assistantMessage : ''
            };
          })()`,
          returnByValue: true
        }, 5000);
        if (readiness.result.value.overlayReady ||
            readiness.result.value.componentError) {
          assistantProbe.componentMessage =
            readiness.result.value.componentMessage || "";
          break;
        }
        await delay(200);
      }
      const openState = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const visible = (node) => !!node &&
            getComputedStyle(node).display !== 'none' &&
            getComputedStyle(node).visibility !== 'hidden' &&
            node.getAttribute('aria-hidden') !== 'true' &&
            !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
          const overlays = Array.from(document.querySelectorAll(
            '.northstar-agent-overlay'
          ));
          const overlay = overlays[0] || null;
          const frame = overlay && overlay.querySelector(
            '.northstar-agent-overlay__frame'
          );
          const chat = overlay && overlay.querySelector('langflow-chat');
          const portal = overlay && overlay.querySelector('.northstar-agent-portal');
          const error = overlay && overlay.querySelector(
            '.northstar-agent-overlay__error[role="alert"]'
          );
          const sidebar = document.querySelector('#k2sp-shell .k2sp-sidebar');
          const modal = document.createElement('div');
          modal.className = 'k2-modal';
          Object.assign(modal.style, {
            position: 'fixed', inset: '20px', display: 'block',
            width: '200px', height: '100px', zIndex: '10000'
          });
          document.body.append(modal);
          const modalPrecedence = !!frame && (
            (getComputedStyle(frame).visibility === 'hidden' ||
              overlay.getAttribute('aria-hidden') === 'true') ||
            Number(getComputedStyle(modal).zIndex) >
              Number(getComputedStyle(overlay).zIndex)
          );
          modal.remove();
          return {
            overlayCount: overlays.length,
            chatCount: overlay ? overlay.querySelectorAll('langflow-chat').length : 0,
            portalCount: overlay ? overlay.querySelectorAll('.northstar-agent-portal').length : 0,
            portalSessionCount: portal ? portal.querySelectorAll(
              '.northstar-agent-portal__session'
            ).length : 0,
            portalHasNewChat: !!(portal && portal.querySelector(
              '.northstar-agent-portal__new'
            )),
            portalHasFileInput: !!(portal && portal.querySelector('input[type="file"]')),
            errorCount: overlay ? overlay.querySelectorAll(
              '.northstar-agent-overlay__error[role="alert"]'
            ).length : 0,
            errorText: error ? (error.textContent || '').trim() : '',
            overlayVisible: visible(overlay),
            overlayPosition: overlay ? getComputedStyle(overlay).position : null,
            overlayPointerEvents: overlay ? getComputedStyle(overlay).pointerEvents : null,
            chatPointerEvents: chat ? getComputedStyle(chat).pointerEvents : null,
            overlayZIndex: overlay ? Number(getComputedStyle(overlay).zIndex) : null,
            frameWidth: frame ? frame.getBoundingClientRect().width : null,
            frameHeight: frame ? frame.getBoundingClientRect().height : null,
            modalPrecedence,
            open: {
              clientWidth: document.documentElement.clientWidth,
              scrollWidth: document.documentElement.scrollWidth,
              clientHeight: document.documentElement.clientHeight,
              scrollHeight: document.documentElement.scrollHeight,
              scrollX: window.scrollX,
              scrollY: window.scrollY,
              sidebarWidth: sidebar ? sidebar.getBoundingClientRect().width : null
            }
          };
        })()`,
        returnByValue: true
      }, 5000);
      Object.assign(assistantProbe, openState.result.value);

      if (assistantProbe.portalCount === 1 && assistantBehaviorRequested) {
        const behaviorState = await session.send("Runtime.evaluate", {
          expression: `(async () => {
            const waitFor = async (predicate, timeout = 5000) => {
              const deadline = Date.now() + timeout;
              while (Date.now() < deadline) {
                if (predicate()) return true;
                await new Promise((resolve) => setTimeout(resolve, 25));
              }
              return false;
            };
            const control = Array.from(document.querySelectorAll(
              'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
            )).find((candidate) => candidate._assistantExperience === 'command-portal');
            if (!control) return { portalBehaviorExercised: false };
            const originalConfirm = window.confirm;
            try {
              const originalSessionId = control._portalSessionId;
              document.querySelector('.northstar-agent-portal__new')?.click();
              const newSessionChanged = await waitFor(
                () => !!control._portalSessionId &&
                  control._portalSessionId !== originalSessionId);

              const documentAttachment = new File(
                ['case attachment'], 'case.pdf', { type: 'application/pdf' });
              const imageAttachment = new File(
                ['image attachment'], 'case.png', { type: 'image/png' });
              await control._addPortalFiles([documentAttachment, imageAttachment]);
              const documentUploaded = control._portalAttachments.some(
                (item) => item.name === 'case.pdf' &&
                  item.status === 'ready' &&
                  item.path === 'test/file-1.pdf');
              const imageUploaded = control._portalAttachments.some(
                (item) => item.name === 'case.png' &&
                  item.status === 'ready' &&
                  item.path.endsWith('/test-image.png'));

              const input = document.querySelector(
                '.northstar-agent-portal__composer textarea');
              if (input) {
                input.value = 'Investigate case 42';
                input.dispatchEvent(new Event('input', { bubbles: true }));
                document.querySelector('.northstar-agent-portal__send')?.click();
              }
              const streamedReplyReady = await waitFor(
                () => !control._portalSending &&
                  control._portalMessages.some((message) =>
                    message.role === 'assistant' &&
                    message.text === 'Portal ready'));

              const requests = Array.isArray(window.__portalRequests)
                ? window.__portalRequests : [];
              const runRequest = requests.find((request) =>
                request.method === 'POST' &&
                request.url.includes('/api/v1/run/'));
              let runPayload = {};
              try { runPayload = JSON.parse(runRequest?.body || '{}'); } catch {}
              const runPayloadValid = runPayload.input_value === 'Investigate case 42' &&
                runPayload.session_id === control._portalSessionId &&
                runPayload.tweaks &&
                runPayload.tweaks[control.LangflowFileComponentId] &&
                runPayload.tweaks[control.LangflowFileComponentId].path[0] ===
                  'test/file-1.pdf' &&
                runPayload.tweaks[control.LangflowChatInputComponentId] &&
                runPayload.tweaks[control.LangflowChatInputComponentId].files.endsWith(
                  '/test-image.png');

              const backtick = String.fromCharCode(96);
              const fence = backtick.repeat(3);
              control._portalMessages.push({
                id: 'markdown-probe',
                role: 'assistant',
                timestamp: new Date().toISOString(),
                files: [],
                text: [
                  '# Markdown response',
                  '',
                  '**Bold** and *emphasis* with ' + backtick + 'inline code' + backtick + '.',
                  '',
                  '- First item',
                  '- Second item',
                  '',
                  '> Governed quotation',
                  '',
                  '| Field | Value |',
                  '| --- | --- |',
                  '| Status | Ready |',
                  '',
                  '[Safe link](https://example.com/case)',
                  '[Unsafe link](javascript:alert(1))',
                  '<img src=x onerror=alert(1)>',
                  '',
                  fence + 'js',
                  '<script>window.__markdownInjected = true;</script>',
                  fence
                ].join('\\n')
              });
              control._renderAssistantPortal();
              const markdownCopies = Array.from(document.querySelectorAll(
                '.northstar-agent-portal__message-copy.is-markdown'));
              const markdown = markdownCopies[markdownCopies.length - 1] || null;
              const markdownRendered = !!markdown &&
                !!markdown.querySelector('h3') &&
                !!markdown.querySelector('strong') &&
                !!markdown.querySelector('em') &&
                !!markdown.querySelector('code') &&
                !!markdown.querySelector('ul li') &&
                !!markdown.querySelector('blockquote') &&
                !!markdown.querySelector('table th') &&
                !!markdown.querySelector('pre code') &&
                !!markdown.querySelector('a[href^="https://example.com/"]');
              const markdownSanitized = !!markdown &&
                !markdown.querySelector('script,img,iframe,object,embed') &&
                !Array.from(markdown.querySelectorAll('a')).some((link) =>
                  !/^(https?:|mailto:)$/i.test(link.protocol)) &&
                markdown.textContent.includes('<img src=x onerror=alert(1)>') &&
                window.__markdownInjected !== true;
              const markdownHeadingPreservesPageH1 = !!markdown &&
                !markdown.querySelector('h1,h2');

              window.confirm = () => true;
              Array.from(document.querySelectorAll(
                '.northstar-agent-portal__actions button'
              )).find((button) => button.textContent.trim() === 'Clear')?.click();
              const cleared = await waitFor(() =>
                !control._portalSending &&
                control._portalMessages.length === 0);
              const deleteRequested = requests.some((request) =>
                request.method === 'DELETE' &&
                request.url.includes('/api/v1/monitor/messages/session/'));

              return {
                portalBehaviorExercised: true,
                newSessionChanged,
                documentUploaded,
                imageUploaded,
                streamedReplyReady,
                runPayloadValid,
                markdownRendered,
                markdownSanitized,
                markdownHeadingPreservesPageH1,
                cleared,
                deleteRequested
              };
            } finally {
              window.confirm = originalConfirm;
            }
          })()`,
          returnByValue: true,
          awaitPromise: true
        }, 10000);
        Object.assign(assistantProbe, behaviorState.result.value);
      }

      if (assistantProbe.portalCount === 1 && assistantLiveMessage.length > 0) {
        const liveState = await session.send("Runtime.evaluate", {
          expression: `(async () => {
            const waitFor = async (predicate, timeout = 90000) => {
              const deadline = Date.now() + timeout;
              while (Date.now() < deadline) {
                if (predicate()) return true;
                await new Promise((resolve) => setTimeout(resolve, 100));
              }
              return false;
            };
            const control = Array.from(document.querySelectorAll(
              'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
            )).find((candidate) => candidate._assistantExperience === 'command-portal');
            if (!control) return { liveMessageSent: false, liveError: 'Portal control not found.' };
            document.querySelector('.northstar-agent-portal__new')?.click();
            await waitFor(() => !!control._portalSessionId, 5000);
            const input = document.querySelector(
              '.northstar-agent-portal__composer textarea');
            const send = document.querySelector('.northstar-agent-portal__send');
            if (!input || !send) {
              return { liveMessageSent: false, liveError: 'Composer controls not found.' };
            }
            input.value = ${JSON.stringify(assistantLiveMessage)};
            input.dispatchEvent(new Event('input', { bubbles: true }));
            send.click();
            const completed = await waitFor(
              () => control._portalSending === false &&
                control._portalMessages.some((message) =>
                  message.role === 'assistant' &&
                  typeof message.text === 'string' &&
                  message.text.trim().length > 0));
            const replies = control._portalMessages.filter(
              (message) => message.role === 'assistant');
            const reply = replies.length ? replies[replies.length - 1] : null;
            const markdownCopies = Array.from(document.querySelectorAll(
              '.northstar-agent-portal__message.is-assistant ' +
              '.northstar-agent-portal__message-copy.is-markdown'));
            const markdown = markdownCopies.length
              ? markdownCopies[markdownCopies.length - 1] : null;
            return {
              liveMessageSent: true,
              liveCompleted: completed,
              liveSessionId: control._portalSessionId || '',
              liveReplyText: reply ? reply.text : '',
              liveStatus: control._portalStatus || '',
              liveMessageCount: control._portalMessages.length,
              liveMarkdownRendered: !!markdown &&
                markdown.children.length > 0 &&
                !markdown.querySelector('script,img,iframe,object,embed'),
              liveMarkdownElements: markdown ? {
                headings: markdown.querySelectorAll('h3,h4,h5,h6').length,
                strong: markdown.querySelectorAll('strong').length,
                lists: markdown.querySelectorAll('ul,ol').length,
                code: markdown.querySelectorAll('code').length,
                blockquotes: markdown.querySelectorAll('blockquote').length,
                tables: markdown.querySelectorAll('table').length,
                links: markdown.querySelectorAll('a').length
              } : null
            };
          })()`,
          returnByValue: true,
          awaitPromise: true
        }, 100000);
        Object.assign(assistantProbe, liveState.result.value);
      }

      await session.send("Input.dispatchKeyEvent", {
        type: "keyDown", key: "Escape", code: "Escape", windowsVirtualKeyCode: 27
      });
      await session.send("Input.dispatchKeyEvent", {
        type: "keyUp", key: "Escape", code: "Escape", windowsVirtualKeyCode: 27
      });
      await delay(150);
      const closedState = await session.send("Runtime.evaluate", {
        expression: `(() => {
          const control = Array.from(document.querySelectorAll(
            'northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp'
          )).find((candidate) => candidate.shadowRoot &&
            candidate.shadowRoot.querySelector('.northstar-command__trigger')) || null;
          const trigger = control && control.shadowRoot.querySelector(
            '.northstar-command__trigger'
          );
          const sidebar = document.querySelector('#k2sp-shell .k2sp-sidebar');
          return {
            overlayCountAfterClose: document.querySelectorAll(
              '.northstar-agent-overlay'
            ).length,
            chatCountAfterClose: document.querySelectorAll(
              '.northstar-agent-overlay langflow-chat'
            ).length,
            portalCountAfterClose: document.querySelectorAll(
              '.northstar-agent-overlay .northstar-agent-portal'
            ).length,
            launcherFocusedAfterClose: !!trigger &&
              control.shadowRoot.activeElement === trigger,
            after: {
              clientWidth: document.documentElement.clientWidth,
              scrollWidth: document.documentElement.scrollWidth,
              clientHeight: document.documentElement.clientHeight,
              scrollHeight: document.documentElement.scrollHeight,
              scrollX: window.scrollX,
              scrollY: window.scrollY,
              sidebarWidth: sidebar ? sidebar.getBoundingClientRect().width : null
            }
          };
        })()`,
        returnByValue: true
      }, 5000);
      Object.assign(assistantProbe, closedState.result.value);
      assistantProbe.layoutStable = JSON.stringify(assistantProbe.before) ===
        JSON.stringify(assistantProbe.open) &&
        JSON.stringify(assistantProbe.before) === JSON.stringify(assistantProbe.after);
      assistantProbe.passed =
        assistantProbe.overlayCount === 1 &&
        assistantProbe.overlayPosition === "fixed" &&
        assistantProbe.overlayPointerEvents === "none" &&
        (assistantProbe.chatCount === 1 || assistantProbe.portalCount === 1 ||
          assistantProbe.errorCount === 1) &&
        assistantProbe.modalPrecedence === true &&
        assistantProbe.overlayCountAfterClose === 0 &&
        assistantProbe.chatCountAfterClose === 0 &&
        assistantProbe.portalCountAfterClose === 0 &&
        assistantProbe.launcherFocusedAfterClose === true &&
        assistantProbe.layoutStable === true;
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
      assistantOverlayProbe: ${JSON.stringify(assistantProbe)},
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
      customControls: Array.from(document.querySelectorAll("northstar-command-palette,northstar-case-assistant-palette,northstar-case-command-portal,northstar-case-command-portal-markdown,northstar-command-palette-acp,northstar-dashboard-widget"))
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
        paletteDefined: [
          "northstar-command-palette",
          "northstar-case-assistant-palette",
          "northstar-case-command-portal",
          "northstar-case-command-portal-markdown",
          "northstar-command-palette-acp"
        ].some((name) => !!window.customElements.get(name)),
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
