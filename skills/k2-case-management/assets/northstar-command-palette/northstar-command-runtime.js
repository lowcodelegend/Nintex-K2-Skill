(function () {
  "use strict";

  function changed(control, property) {
    if (window.K2 && typeof window.K2.RaisePropertyChanged === "function") {
      window.K2.RaisePropertyChanged(control, property);
    }
  }

  function loadStyles(control, parent) {
    if (!control.ControlType) control.ControlType = "northstar-command-palette";
    if (!Array.isArray(control.RuntimeStyleFileNames) || control.RuntimeStyleFileNames.length === 0) {
      control.RuntimeStyleFileNames = ["northstar-command-runtime.css"];
    }
    if (!Array.isArray(control.DesigntimeStyleFileNames)) {
      control.DesigntimeStyleFileNames = ["northstar-command-designtime.css"];
    }
    const styles = window.SourceCode && window.SourceCode.Forms && window.SourceCode.Forms.ControlStyles;
    return styles && typeof styles.loadStyleResources === "function"
      ? Promise.resolve(styles.loadStyleResources(control, parent))
      : Promise.resolve();
  }

  function asBoolean(value) {
    return value === true || String(value).toLowerCase() === "true";
  }

  function text(value) {
    return value == null ? "" : String(value).trim();
  }

  const PROTOTYPE_CASE_CONTEXT_PREFIX = "[[northstar-case-context:";
  const PROTOTYPE_CASE_CONTEXT_SUFFIX = "]]";

  function normalizeCaseTypeShortcode(value) {
    const candidate = text(value).toUpperCase();
    return /^[A-Z][A-Z0-9_-]{0,15}$/.test(candidate) ? candidate : "";
  }

  function caseTypeShortcodeFromUrl(value) {
    let parsed;
    try {
      parsed = new URL(value || window.location.href, window.location.href);
    } catch (_) {
      return "";
    }
    for (const name of [
      "caseType", "case_type", "caseTypeCode", "case_type_code",
      "caseTypeShortcode", "case_type_shortcode"
    ]) {
      const explicit = normalizeCaseTypeShortcode(parsed.searchParams.get(name));
      if (explicit) return explicit;
    }
    const formMatch = parsed.pathname.match(/\/Form\/([^/?#]+)/i);
    if (!formMatch) return "";
    let formName = formMatch[1];
    try {
      formName = decodeURIComponent(formName.replace(/\+/g, " "));
    } catch (_) {
      // Leave malformed URL text encoded; it will fail the shortcode allowlist below.
    }
    const prefix = formName.match(/^([A-Za-z][A-Za-z0-9_-]{0,15})\./);
    return prefix ? normalizeCaseTypeShortcode(prefix[1]) : "";
  }

  function prototypeCaseContext(shortcode) {
    const normalized = normalizeCaseTypeShortcode(shortcode);
    return normalized ? {
      case_type_shortcode: normalized,
      source: "url",
      trusted: false
    } : null;
  }

  function injectPrototypeCaseContext(value, shortcode) {
    const context = prototypeCaseContext(shortcode);
    const message = String(value == null ? "" : value);
    if (!context) return message;
    return PROTOTYPE_CASE_CONTEXT_PREFIX + JSON.stringify(context) +
      PROTOTYPE_CASE_CONTEXT_SUFFIX + "\n" + message;
  }

  function stripPrototypeCaseContext(value) {
    const message = String(value == null ? "" : value);
    if (!message.startsWith(PROTOTYPE_CASE_CONTEXT_PREFIX)) return message;
    const lineEnd = message.indexOf("\n");
    const marker = lineEnd >= 0 ? message.slice(0, lineEnd) : message;
    if (!marker.endsWith(PROTOTYPE_CASE_CONTEXT_SUFFIX)) return message;
    const encoded = marker.slice(
      PROTOTYPE_CASE_CONTEXT_PREFIX.length,
      -PROTOTYPE_CASE_CONTEXT_SUFFIX.length);
    try {
      const context = JSON.parse(encoded);
      if (!context || context.source !== "url" || context.trusted !== false ||
          !normalizeCaseTypeShortcode(context.case_type_shortcode)) {
        return message;
      }
    } catch (_) {
      return message;
    }
    return lineEnd >= 0 ? message.slice(lineEnd + 1) : "";
  }

  function pick(item, names) {
    for (const name of names) {
      if (Object.prototype.hasOwnProperty.call(item, name) && item[name] != null) return item[name];
    }
    return "";
  }

  function safeTarget(value) {
    const raw = text(value);
    if (!raw) return "";
    try {
      const parsed = new URL(raw, window.location.origin);
      if (parsed.origin !== window.location.origin || !/^\/Runtime\//i.test(parsed.pathname)) return "";
      return parsed.pathname + parsed.search + parsed.hash;
    } catch (_) {
      return "";
    }
  }

  function normalize(items) {
    if (typeof items === "string") {
      try { items = JSON.parse(items); } catch (_) { items = []; }
    }
    if (!Array.isArray(items)) return [];
    return items.slice(0, 50).map(function (item, index) {
      if (!item || typeof item !== "object") return null;
      const target = safeTarget(pick(item, ["TargetUrl", "targetUrl", "Route", "route"]));
      const active = pick(item, ["IsActive", "isActive", "Active", "active"]);
      if (!target || (active !== "" && !asBoolean(active))) return null;
      return {
        code: text(pick(item, ["SuggestionCode", "suggestionCode", "Code", "code"])) || "suggestion-" + index,
        kind: text(pick(item, ["Kind", "kind"])) || "Destination",
        title: text(pick(item, ["Title", "title", "Label", "label"])) || "Untitled destination",
        subtitle: text(pick(item, ["Subtitle", "subtitle", "Description", "description"])),
        icon: text(pick(item, ["IconToken", "iconToken", "Icon", "icon"])) || "arrow",
        target: target,
        order: Number(pick(item, ["SortOrder", "sortOrder"])) || index
      };
    }).filter(Boolean).sort(function (left, right) {
      return left.order - right.order || left.title.localeCompare(right.title);
    });
  }

  function trimTitle(value, fallback) {
    const normalized = text(value).replace(/\s+/g, " ");
    if (!normalized) return fallback || "New conversation";
    return normalized.length > 52 ? normalized.slice(0, 49) + "…" : normalized;
  }

  function formatPortalTime(value) {
    const parsed = value ? new Date(value) : null;
    if (!parsed || Number.isNaN(parsed.getTime())) return "";
    return parsed.toLocaleString([], {
      month: "short", day: "numeric", hour: "2-digit", minute: "2-digit"
    });
  }

  function normalizePortalFiles(value) {
    if (typeof value === "string") {
      try { value = JSON.parse(value); } catch (_) {
        value = text(value) ? [value] : [];
      }
    }
    if (!Array.isArray(value)) return [];
    return value.map(function (item) {
      if (item && typeof item === "object") {
        return text(item.name || item.path || item.file_path);
      }
      return text(item);
    }).filter(Boolean);
  }

  function safeMarkdownHref(value) {
    const raw = String(value == null ? "" : value).trim();
    if (!raw || /[\u0000-\u001f\u007f]/.test(raw)) return "";
    try {
      const parsed = new URL(raw, window.location.href);
      return /^(https?:|mailto:)$/i.test(parsed.protocol) ? parsed.href : "";
    } catch (_) {
      return "";
    }
  }

  function appendInlineMarkdown(parent, value) {
    const source = String(value == null ? "" : value);
    let cursor = 0;
    let literal = "";
    const flush = function () {
      if (!literal) return;
      parent.append(document.createTextNode(literal));
      literal = "";
    };
    const appendWrapped = function (tagName, content) {
      flush();
      const node = document.createElement(tagName);
      appendInlineMarkdown(node, content);
      parent.append(node);
    };

    while (cursor < source.length) {
      if (source[cursor] === "\\" && cursor + 1 < source.length &&
          /[\\`*_[\]{}()#+\-.!|>~]/.test(source[cursor + 1])) {
        literal += source[cursor + 1];
        cursor += 2;
        continue;
      }

      if (source[cursor] === "`") {
        let markerLength = 1;
        while (source[cursor + markerLength] === "`") markerLength += 1;
        const marker = "`".repeat(markerLength);
        const closing = source.indexOf(marker, cursor + markerLength);
        if (closing >= cursor + markerLength) {
          flush();
          const code = document.createElement("code");
          code.textContent = source.slice(cursor + markerLength, closing);
          parent.append(code);
          cursor = closing + markerLength;
          continue;
        }
      }

      if (source[cursor] === "[") {
        const closeLabel = source.indexOf("]", cursor + 1);
        if (closeLabel > cursor + 1 && source[closeLabel + 1] === "(") {
          const closeTarget = source.indexOf(")", closeLabel + 2);
          if (closeTarget > closeLabel + 2) {
            const destination = source.slice(closeLabel + 2, closeTarget).trim();
            const targetMatch = destination.match(
              /^(\S+?)(?:\s+["']([^"']*)["'])?$/);
            const href = targetMatch ? safeMarkdownHref(targetMatch[1]) : "";
            if (href) {
              flush();
              const link = document.createElement("a");
              link.href = href;
              if (/^https?:/i.test(link.protocol)) {
                link.target = "_blank";
                link.rel = "noopener noreferrer";
              }
              if (targetMatch[2]) link.title = targetMatch[2];
              appendInlineMarkdown(link, source.slice(cursor + 1, closeLabel));
              parent.append(link);
              cursor = closeTarget + 1;
              continue;
            }
          }
        }
      }

      let wrapped = false;
      for (const token of [
        { marker: "**", tagName: "strong" },
        { marker: "__", tagName: "strong" },
        { marker: "~~", tagName: "del" }
      ]) {
        if (!source.startsWith(token.marker, cursor)) continue;
        const closing = source.indexOf(token.marker, cursor + token.marker.length);
        if (closing <= cursor + token.marker.length) continue;
        appendWrapped(token.tagName,
          source.slice(cursor + token.marker.length, closing));
        cursor = closing + token.marker.length;
        wrapped = true;
        break;
      }
      if (wrapped) continue;

      if (source[cursor] === "*" || source[cursor] === "_") {
        const marker = source[cursor];
        const closing = source.indexOf(marker, cursor + 1);
        const previous = cursor > 0 ? source[cursor - 1] : "";
        const following = closing >= 0 && closing + 1 < source.length
          ? source[closing + 1] : "";
        const wordInternal = marker === "_" &&
          /[\p{L}\p{N}]/u.test(previous) &&
          /[\p{L}\p{N}]/u.test(following);
        if (!wordInternal && closing > cursor + 1) {
          appendWrapped("em", source.slice(cursor + 1, closing));
          cursor = closing + 1;
          continue;
        }
      }

      literal += source[cursor];
      cursor += 1;
    }
    flush();
  }

  function splitMarkdownTableRow(value) {
    let source = String(value == null ? "" : value).trim();
    if (source.startsWith("|")) source = source.slice(1);
    if (source.endsWith("|") && !source.endsWith("\\|")) source = source.slice(0, -1);
    const cells = [];
    let cell = "";
    let escaped = false;
    for (const character of source) {
      if (escaped) {
        cell += character;
        escaped = false;
      } else if (character === "\\") {
        escaped = true;
      } else if (character === "|") {
        cells.push(cell.trim());
        cell = "";
      } else {
        cell += character;
      }
    }
    if (escaped) cell += "\\";
    cells.push(cell.trim());
    return cells;
  }

  function isMarkdownTableDelimiter(value) {
    const cells = splitMarkdownTableRow(value);
    return cells.length > 0 && cells.every(function (cell) {
      return /^:?-{3,}:?$/.test(cell);
    });
  }

  function appendMarkdown(container, value) {
    const source = String(value == null ? "" : value)
      .replace(/\r\n?/g, "\n");
    const lines = source.split("\n");
    const startsBlock = function (line, index) {
      return /^ {0,3}(`{3,}|~{3,})/.test(line) ||
        /^ {0,3}#{1,6}\s+/.test(line) ||
        /^ {0,3}>\s?/.test(line) ||
        /^ {0,3}(?:[-+*]|\d+[.)])\s+/.test(line) ||
        /^ {0,3}(?:(?:\*\s*){3,}|(?:-\s*){3,}|(?:_\s*){3,})$/.test(line) ||
        (index + 1 < lines.length &&
          line.includes("|") &&
          isMarkdownTableDelimiter(lines[index + 1]));
    };
    let index = 0;

    while (index < lines.length) {
      const line = lines[index];
      if (!line.trim()) {
        index += 1;
        continue;
      }

      const fence = line.match(/^ {0,3}(`{3,}|~{3,})\s*([A-Za-z0-9_-]{0,32})\s*$/);
      if (fence) {
        const markerCharacter = fence[1][0];
        const markerLength = fence[1].length;
        const body = [];
        index += 1;
        while (index < lines.length &&
            !new RegExp("^ {0,3}" + markerCharacter + "{" + markerLength + ",}\\s*$")
              .test(lines[index])) {
          body.push(lines[index]);
          index += 1;
        }
        if (index < lines.length) index += 1;
        const pre = document.createElement("pre");
        const code = document.createElement("code");
        if (fence[2]) code.className = "language-" + fence[2].toLocaleLowerCase();
        code.textContent = body.join("\n");
        pre.append(code);
        container.append(pre);
        continue;
      }

      const heading = line.match(/^ {0,3}(#{1,6})\s+(.+?)\s*#*\s*$/);
      if (heading) {
        const level = Math.min(6, heading[1].length + 2);
        const node = document.createElement("h" + level);
        appendInlineMarkdown(node, heading[2]);
        container.append(node);
        index += 1;
        continue;
      }

      if (/^ {0,3}(?:(?:\*\s*){3,}|(?:-\s*){3,}|(?:_\s*){3,})$/.test(line)) {
        container.append(document.createElement("hr"));
        index += 1;
        continue;
      }

      if (/^ {0,3}>\s?/.test(line)) {
        const quoteLines = [];
        while (index < lines.length && /^ {0,3}>\s?/.test(lines[index])) {
          quoteLines.push(lines[index].replace(/^ {0,3}>\s?/, ""));
          index += 1;
        }
        const quote = document.createElement("blockquote");
        appendMarkdown(quote, quoteLines.join("\n"));
        container.append(quote);
        continue;
      }

      const listMatch = line.match(/^ {0,3}([-+*]|\d+[.)])\s+(.+)$/);
      if (listMatch) {
        const ordered = /^\d/.test(listMatch[1]);
        const list = document.createElement(ordered ? "ol" : "ul");
        while (index < lines.length) {
          const itemMatch = lines[index].match(/^ {0,3}([-+*]|\d+[.)])\s+(.+)$/);
          if (!itemMatch || /^\d/.test(itemMatch[1]) !== ordered) break;
          const item = document.createElement("li");
          appendInlineMarkdown(item, itemMatch[2]);
          list.append(item);
          index += 1;
        }
        container.append(list);
        continue;
      }

      if (index + 1 < lines.length &&
          line.includes("|") &&
          isMarkdownTableDelimiter(lines[index + 1])) {
        const headings = splitMarkdownTableRow(line);
        const table = document.createElement("table");
        const tableHead = document.createElement("thead");
        const headingRow = document.createElement("tr");
        headings.forEach(function (cell) {
          const headingCell = document.createElement("th");
          headingCell.scope = "col";
          appendInlineMarkdown(headingCell, cell);
          headingRow.append(headingCell);
        });
        tableHead.append(headingRow);
        table.append(tableHead);
        index += 2;
        const tableBody = document.createElement("tbody");
        while (index < lines.length && lines[index].includes("|") && lines[index].trim()) {
          const row = document.createElement("tr");
          const cells = splitMarkdownTableRow(lines[index]);
          headings.forEach(function (_, cellIndex) {
            const cell = document.createElement("td");
            appendInlineMarkdown(cell, cells[cellIndex] || "");
            row.append(cell);
          });
          tableBody.append(row);
          index += 1;
        }
        table.append(tableBody);
        const tableWrap = document.createElement("div");
        tableWrap.className = "northstar-agent-portal__markdown-table";
        tableWrap.setAttribute("tabindex", "0");
        tableWrap.setAttribute("role", "region");
        tableWrap.setAttribute("aria-label", "Scrollable response table");
        tableWrap.append(table);
        container.append(tableWrap);
        continue;
      }

      const paragraphLines = [line];
      index += 1;
      while (index < lines.length &&
          lines[index].trim() &&
          !startsBlock(lines[index], index)) {
        paragraphLines.push(lines[index]);
        index += 1;
      }
      const paragraph = document.createElement("p");
      paragraphLines.forEach(function (paragraphLine, lineIndex) {
        if (lineIndex > 0) paragraph.append(document.createElement("br"));
        appendInlineMarkdown(paragraph, paragraphLine);
      });
      container.append(paragraph);
    }
  }

  function messageTextFromRun(payload) {
    if (!payload || typeof payload !== "object") return "";
    if (typeof payload.message === "string") return payload.message;
    if (payload.message && typeof payload.message.text === "string") return payload.message.text;
    if (payload.result && typeof payload.result.message === "string") return payload.result.message;
    const outputs = Array.isArray(payload.outputs) ? payload.outputs : [];
    for (const outer of outputs) {
      const nested = outer && Array.isArray(outer.outputs) ? outer.outputs : [];
      for (const output of nested) {
        const result = output && output.results;
        if (result && result.message && typeof result.message.text === "string") {
          return result.message.text;
        }
        if (result && typeof result.text === "string") return result.text;
      }
    }
    return "";
  }

  const APPROVED_LANGFLOW_SCRIPT =
    "https://cdn.jsdelivr.net/gh/langflow-ai/langflow-embedded-chat@v1.0.8/dist/build/static/js/bundle.min.js";

  function loadLangflow(scriptUrl) {
    if (scriptUrl !== APPROVED_LANGFLOW_SCRIPT) {
      return Promise.reject(new Error("The case assistant script is not the approved pinned Langflow bundle."));
    }
    if (window.customElements.get("langflow-chat")) return Promise.resolve();
    if (!window.__northstarLangflowLoads) window.__northstarLangflowLoads = Object.create(null);
    if (window.__northstarLangflowLoads[scriptUrl]) return window.__northstarLangflowLoads[scriptUrl];
    window.__northstarLangflowLoads[scriptUrl] = new Promise(function (resolve, reject) {
      let settled = false;
      let timeout = null;
      const finish = function (error) {
        if (settled) return;
        settled = true;
        window.clearTimeout(timeout);
        if (error) {
          delete window.__northstarLangflowLoads[scriptUrl];
          reject(error);
        } else {
          resolve();
        }
      };
      const waitForControl = function () {
        if (window.customElements.get("langflow-chat")) finish();
        else window.customElements.whenDefined("langflow-chat").then(function () { finish(); }, function () {
          finish(new Error("The Langflow chat element did not register."));
        });
      };
      const existing = document.querySelector("script[data-northstar-langflow-chat]");
      if (existing) {
        if (window.customElements.get("langflow-chat")) finish();
        else {
          existing.addEventListener("load", waitForControl, {once: true});
          existing.addEventListener("error", function () {
            finish(new Error("The Langflow chat bundle could not be loaded."));
          }, {once: true});
        }
        timeout = window.setTimeout(function () {
          finish(new Error("The Langflow chat bundle did not become ready within 20 seconds."));
        }, 20000);
        return;
      }
      const script = document.createElement("script");
      script.src = scriptUrl;
      script.async = true;
      script.crossOrigin = "anonymous";
      script.dataset.northstarLangflowChat = "true";
      script.addEventListener("load", waitForControl, {once: true});
      script.addEventListener("error", function () {
        finish(new Error("The Langflow chat bundle could not be loaded. Check CSP and network access."));
      }, {once: true});
      document.head.append(script);
      timeout = window.setTimeout(function () {
        finish(new Error("The Langflow chat bundle did not become ready within 20 seconds."));
      }, 20000);
    });
    return window.__northstarLangflowLoads[scriptUrl];
  }

  function observeLangflowAuthentication(control, hostUrl, flowId) {
    if (typeof window.fetch !== "function") return function () {};
    let monitor = window.__northstarLangflowAuthenticationMonitor;
    if (!monitor) {
      monitor = {
        originalFetch: window.fetch,
        subscribers: new Set()
      };
      window.__northstarLangflowAuthenticationMonitor = monitor;
      window.fetch = function () {
        const args = arguments;
        return monitor.originalFetch.apply(window, args).then(function (response) {
          let url = "";
          try {
            url = typeof args[0] === "string" ? args[0] :
              args[0] && args[0].url ? args[0].url : "";
          } catch (_) {
            url = "";
          }
          if (response && (response.status === 401 || response.status === 403)) {
            monitor.subscribers.forEach(function (subscriber) {
              if (subscriber.matches(url)) subscriber.failed(response.status);
            });
          }
          return response;
        });
      };
    }
    const expected = hostUrl + "/api/v1/run/" + flowId;
    const subscriber = {
      control: control,
      matches: function (url) {
        return typeof url === "string" &&
          (url === expected || url.indexOf(expected + "?") === 0);
      },
      failed: function (status) {
        if (control.isConnected) control._showAssistantAuthenticationError(status);
      }
    };
    monitor.subscribers.add(subscriber);
    return function () {
      const current = window.__northstarLangflowAuthenticationMonitor;
      if (!current) return;
      current.subscribers.delete(subscriber);
      if (current.subscribers.size === 0) {
        window.fetch = current.originalFetch;
        delete window.__northstarLangflowAuthenticationMonitor;
      }
    };
  }

  if (!window.customElements.get("northstar-command-palette")) {
    window.customElements.define("northstar-command-palette", class NorthstarCommandPalette extends K2BaseControl {
      constructor() {
        super();
        this._value = "";
        this._suggestions = [];
        this._searchUrlTemplate = "/Runtime/Runtime/Form/All%20Cases?q={query}";
        this._placeholder = "Search cases, work and reports";
        this._assistantEnabled = false;
        this._assistantExperience = "embedded-widget";
        this._assistantLabel = "Ask Case Assistant";
        this._assistantDescription = "Ask questions and take supported case actions";
        this._langflowHostUrl = "";
        this._langflowFlowId = "";
        this._langflowScriptUrl = APPROVED_LANGFLOW_SCRIPT;
        this._langflowAuthenticationMode = "server-proxy";
        this._langflowWindowTitle = "Case Assistant";
        this._langflowChatPosition = "bottom-right";
        this._langflowWidth = 420;
        this._langflowHeight = 640;
        this._langflowFileComponentId = "";
        this._langflowChatInputComponentId = "";
        this._langflowAllowedFileTypes =
          ".pdf,.txt,.md,.csv,.docx,.xlsx,.png,.jpg,.jpeg,.gif,.bmp,.webp";
        this._langflowMaxFileSizeMb = 25;
        this._assistantState = "idle";
        this._assistantMessage = "";
        this._langflowElement = null;
        this._assistantOverlay = null;
        this._assistantFrame = null;
        this._assistantCloseButton = null;
        this._assistantModalObserver = null;
        this._assistantAuthCleanup = null;
        this._portalSessions = [];
        this._portalSessionId = "";
        this._portalMessages = [];
        this._portalAttachments = [];
        this._portalSending = false;
        this._portalAbortController = null;
        this._portalStatus = "";
        this._portalSidebarOpen = false;
        this._portalComposerValue = "";
        this._portalFocusComposer = false;
        this._caseTypeShortcode = "";
        this._width = "100%";
        this._height = "48px";
        this._isVisible = true;
        this._isEnabled = true;
        this._isReadOnly = false;
        this._tabIndex = "0";
        this._stylesReady = null;
        this._rendering = false;
        this._open = false;
        this._query = "";
        this._activeIndex = 0;
        this._restoreFocus = null;
        this._onDocumentKeyDown = this._handleDocumentKeyDown.bind(this);
        this._onAssistantResize = this._syncAssistantOverlay.bind(this);
      }

      static get observedAttributes() {
        return ["controlstyle", "name", "controltype", "value", "width", "height", "isvisible", "isenabled", "isreadonly", "tabindex"];
      }

      get Value() { return this._value; }
      set Value(value) { this._value = text(value); changed(this, "Value"); }
      get Suggestions() { return this._suggestions; }
      set Suggestions(value) {
        this._suggestions = normalize(value);
        this.render();
        changed(this, "Suggestions");
      }
      get SearchUrlTemplate() { return this._searchUrlTemplate; }
      set SearchUrlTemplate(value) { this._searchUrlTemplate = text(value) || this._searchUrlTemplate; changed(this, "SearchUrlTemplate"); }
      get Placeholder() { return this._placeholder; }
      set Placeholder(value) { this._placeholder = text(value) || "Search cases, work and reports"; this.render(); changed(this, "Placeholder"); }
      get AssistantEnabled() { return this._assistantEnabled; }
      set AssistantEnabled(value) { this._assistantEnabled = asBoolean(value); this.render(); changed(this, "AssistantEnabled"); }
      get AssistantExperience() { return this._assistantExperience; }
      set AssistantExperience(value) {
        this._assistantExperience = value === "command-portal" ? value : "embedded-widget";
        changed(this, "AssistantExperience");
      }
      get AssistantLabel() { return this._assistantLabel; }
      set AssistantLabel(value) { this._assistantLabel = text(value) || "Ask Case Assistant"; this.render(); changed(this, "AssistantLabel"); }
      get AssistantDescription() { return this._assistantDescription; }
      set AssistantDescription(value) { this._assistantDescription = text(value) || "Ask questions and take supported case actions"; this.render(); changed(this, "AssistantDescription"); }
      get LangflowHostUrl() { return this._langflowHostUrl; }
      set LangflowHostUrl(value) { this._langflowHostUrl = text(value); changed(this, "LangflowHostUrl"); }
      get LangflowFlowId() { return this._langflowFlowId; }
      set LangflowFlowId(value) { this._langflowFlowId = text(value); changed(this, "LangflowFlowId"); }
      get LangflowScriptUrl() { return this._langflowScriptUrl; }
      set LangflowScriptUrl(value) { this._langflowScriptUrl = text(value) || APPROVED_LANGFLOW_SCRIPT; changed(this, "LangflowScriptUrl"); }
      get LangflowAuthenticationMode() { return this._langflowAuthenticationMode; }
      set LangflowAuthenticationMode(value) {
        this._langflowAuthenticationMode = value === "server-open-alpha" ? value : "server-proxy";
        changed(this, "LangflowAuthenticationMode");
      }
      get LangflowWindowTitle() { return this._langflowWindowTitle; }
      set LangflowWindowTitle(value) { this._langflowWindowTitle = text(value) || "Case Assistant"; changed(this, "LangflowWindowTitle"); }
      get LangflowChatPosition() { return this._langflowChatPosition; }
      set LangflowChatPosition(value) { this._langflowChatPosition = text(value) || "bottom-right"; changed(this, "LangflowChatPosition"); }
      get LangflowWidth() { return this._langflowWidth; }
      set LangflowWidth(value) { this._langflowWidth = Math.max(320, Math.min(1200, Number(value) || 420)); changed(this, "LangflowWidth"); }
      get LangflowHeight() { return this._langflowHeight; }
      set LangflowHeight(value) { this._langflowHeight = Math.max(420, Math.min(1200, Number(value) || 640)); changed(this, "LangflowHeight"); }
      get LangflowFileComponentId() { return this._langflowFileComponentId; }
      set LangflowFileComponentId(value) { this._langflowFileComponentId = text(value); changed(this, "LangflowFileComponentId"); }
      get LangflowChatInputComponentId() { return this._langflowChatInputComponentId; }
      set LangflowChatInputComponentId(value) {
        this._langflowChatInputComponentId = text(value);
        changed(this, "LangflowChatInputComponentId");
      }
      get LangflowAllowedFileTypes() { return this._langflowAllowedFileTypes; }
      set LangflowAllowedFileTypes(value) {
        this._langflowAllowedFileTypes = text(value) ||
          ".pdf,.txt,.md,.csv,.docx,.xlsx,.png,.jpg,.jpeg,.gif,.bmp,.webp";
        changed(this, "LangflowAllowedFileTypes");
      }
      get LangflowMaxFileSizeMb() { return this._langflowMaxFileSizeMb; }
      set LangflowMaxFileSizeMb(value) {
        this._langflowMaxFileSizeMb = Math.max(1, Math.min(100, Number(value) || 25));
        changed(this, "LangflowMaxFileSizeMb");
      }
      get Width() { return this._width; }
      set Width(value) { this._width = value || "100%"; this.style.width = this._width; changed(this, "Width"); }
      get Height() { return this._height; }
      set Height(value) { this._height = value || "48px"; this.style.minHeight = this._height; changed(this, "Height"); }
      get IsVisible() { return this._isVisible; }
      set IsVisible(value) { this._isVisible = asBoolean(value); this.style.display = this._isVisible ? "" : "none"; changed(this, "IsVisible"); }
      get IsEnabled() { return this._isEnabled; }
      set IsEnabled(value) { this._isEnabled = asBoolean(value); this.render(); changed(this, "IsEnabled"); }
      get IsReadOnly() { return this._isReadOnly; }
      set IsReadOnly(value) { this._isReadOnly = asBoolean(value); this.render(); changed(this, "IsReadOnly"); }
      get TabIndex() { return this._tabIndex; }
      set TabIndex(value) { this._tabIndex = value == null ? "0" : String(value); this.render(); changed(this, "TabIndex"); }

      connectedCallback() {
        super.connectedCallback();
        this._caseTypeShortcode = caseTypeShortcodeFromUrl(window.location.href);
        document.addEventListener("keydown", this._onDocumentKeyDown);
        this.render();
      }

      disconnectedCallback() {
        document.removeEventListener("keydown", this._onDocumentKeyDown);
        this._closeAssistant(false);
        super.disconnectedCallback();
      }

      attributeChangedCallback(name, oldValue, newValue) {
        super.attributeChangedCallback(name, oldValue, newValue);
        const properties = {
          value: "Value", width: "Width", height: "Height", isvisible: "IsVisible",
          isenabled: "IsEnabled", isreadonly: "IsReadOnly", tabindex: "TabIndex"
        };
        if (properties[name]) this[properties[name]] = newValue;
      }

      listItemsChangedCallback(itemsChangedEventArgs) {
        const items = itemsChangedEventArgs && Array.isArray(itemsChangedEventArgs.NewItems)
          ? itemsChangedEventArgs.NewItems
          : itemsChangedEventArgs;
        this.Suggestions = items;
      }

      render() {
        if (!this.isConnected || this._rendering) return;
        this._rendering = true;
        this.ensureShadow();
        if (!this._stylesReady) this._stylesReady = loadStyles(this, this._shadow);
        this._stylesReady.then(this._draw.bind(this), this._draw.bind(this));
      }

      _draw() {
        for (const child of Array.from(this._shadow.children)) {
          if (!(child.tagName === "STYLE" && child.hasAttribute("data-id"))) child.remove();
        }

        const trigger = document.createElement("button");
        trigger.type = "button";
        trigger.className = "northstar-command__trigger";
        trigger.disabled = !this._isEnabled || this._isReadOnly;
        trigger.tabIndex = Number(this._tabIndex) || 0;
        trigger.setAttribute("aria-haspopup", "dialog");
        trigger.setAttribute("aria-expanded", this._open ? "true" : "false");
        const triggerText = document.createElement("span");
        triggerText.className = "northstar-command__trigger-text";
        triggerText.textContent = this._placeholder;
        const shortcut = document.createElement("kbd");
        shortcut.textContent = /Mac|iPhone|iPad/.test(navigator.platform) ? "⌘ K" : "Ctrl K";
        trigger.append(triggerText, shortcut);
        trigger.addEventListener("click", this._openDialog.bind(this));
        this._shadow.append(trigger);

        if (this._open) this._drawDialog();
        if (this._assistantMessage) {
          const assistantStatus = document.createElement("p");
          assistantStatus.className = "northstar-command__assistant-status";
          assistantStatus.setAttribute("role", this._assistantState === "error" ? "alert" : "status");
          assistantStatus.setAttribute("aria-live", this._assistantState === "error" ? "assertive" : "polite");
          assistantStatus.textContent = this._assistantMessage;
          this._shadow.append(assistantStatus);
        }
        this._hasRendered = true;
        this._rendering = false;
        this.dispatchEvent(new Event("Rendered"));
      }

      _drawDialog() {
        const overlay = document.createElement("div");
        overlay.className = "northstar-command__overlay";
        overlay.addEventListener("mousedown", (event) => {
          if (event.target === overlay) this._closeDialog();
        });
        const dialog = document.createElement("section");
        dialog.className = "northstar-command__dialog";
        dialog.setAttribute("role", "dialog");
        dialog.setAttribute("aria-modal", "true");
        dialog.setAttribute("aria-label", "Search Northstar");

        const search = document.createElement("div");
        search.className = "northstar-command__search";
        search.setAttribute("role", "search");
        const input = document.createElement("input");
        input.type = "search";
        input.value = this._query;
        input.placeholder = this._placeholder;
        input.setAttribute("aria-label", this._placeholder);
        input.setAttribute("aria-controls", "northstar-command-results");
        input.setAttribute("autocomplete", "off");
        input.addEventListener("input", () => {
          this._query = input.value;
          this._activeIndex = 0;
          this.render();
        });
        input.addEventListener("keydown", this._handleInputKeyDown.bind(this));
        const esc = document.createElement("kbd");
        esc.textContent = "Esc";
        search.append(input, esc);
        dialog.append(search);

        const results = document.createElement("div");
        results.id = "northstar-command-results";
        results.className = "northstar-command__results";
        results.setAttribute("role", "listbox");
        const matches = this._matches();
        matches.forEach((item, index) => {
          const option = document.createElement("button");
          option.type = "button";
          option.className = "northstar-command__option";
          option.setAttribute("role", "option");
          option.setAttribute("aria-selected", index === this._activeIndex ? "true" : "false");
          option.dataset.index = String(index);
          const icon = document.createElement("span");
          icon.className = "northstar-command__icon";
          icon.setAttribute("aria-hidden", "true");
          icon.textContent = item.icon.slice(0, 1).toUpperCase();
          const copy = document.createElement("span");
          copy.className = "northstar-command__copy";
          const title = document.createElement("strong");
          title.textContent = item.title;
          const subtitle = document.createElement("small");
          subtitle.textContent = item.subtitle || item.kind;
          copy.append(title, subtitle);
          const kind = document.createElement("span");
          kind.className = "northstar-command__kind";
          kind.textContent = item.kind;
          option.append(icon, copy, kind);
          option.addEventListener("mouseenter", () => {
            this._activeIndex = index;
            results.querySelectorAll("[role=option]").forEach((node, optionIndex) => {
              node.setAttribute("aria-selected", optionIndex === index ? "true" : "false");
            });
          });
          option.addEventListener("click", () => this._select(item));
          results.append(option);
        });
        dialog.append(results);

        const status = document.createElement("p");
        status.className = "northstar-command__status";
        status.setAttribute("role", "status");
        status.setAttribute("aria-live", "polite");
        status.textContent = matches.length + (matches.length === 1 ? " result" : " results") +
          (this._query ? " for " + this._query : "");
        dialog.append(status);
        overlay.append(dialog);
        this._shadow.append(overlay);
        requestAnimationFrame(() => input.focus());
      }

      _matches() {
        const query = this._query.trim().toLocaleLowerCase();
        let matches = this._suggestions.filter((item) => !query ||
          (item.title + " " + item.subtitle + " " + item.kind).toLocaleLowerCase().includes(query)).slice(0, 8);
        if (this._assistantEnabled) {
          const assistant = {
            code: "case-assistant",
            kind: "Assistant",
            title: this._assistantLabel,
            subtitle: this._assistantDescription,
            icon: "spark",
            action: "assistant",
            target: "",
            order: -1
          };
          if (!query || (assistant.title + " " + assistant.subtitle + " " + assistant.kind).toLocaleLowerCase().includes(query)) {
            matches.unshift(assistant);
            matches = matches.slice(0, 8);
          }
        }
        if (query && matches.length === 0) {
          const target = safeTarget(this._searchUrlTemplate.replace("{query}", encodeURIComponent(this._query.trim())));
          if (target) {
            matches = [{
              code: "all-cases-search",
              kind: "Search",
              title: "Search all cases for “" + this._query.trim() + "”",
              subtitle: "Open the native All Cases search",
              icon: "search",
              target: target,
              order: 0
            }];
          }
        }
        return matches;
      }

      _handleDocumentKeyDown(event) {
        if ((event.ctrlKey || event.metaKey) && String(event.key).toLowerCase() === "k") {
          if (!this._isEnabled || this._isReadOnly || !this._isVisible) return;
          event.preventDefault();
          this._openDialog();
        } else if (event.key === "Escape" && this._assistantOverlay) {
          event.preventDefault();
          event.stopPropagation();
          this._closeAssistant(true);
        } else if (event.key === "Escape" && this._open) {
          event.preventDefault();
          this._closeDialog();
        }
      }

      _handleInputKeyDown(event) {
        const matches = this._matches();
        if (event.key === "ArrowDown" && matches.length) {
          event.preventDefault();
          this._activeIndex = (this._activeIndex + 1) % matches.length;
          this.render();
        } else if (event.key === "ArrowUp" && matches.length) {
          event.preventDefault();
          this._activeIndex = (this._activeIndex + matches.length - 1) % matches.length;
          this.render();
        } else if (event.key === "Enter" && matches.length) {
          event.preventDefault();
          this._select(matches[this._activeIndex] || matches[0]);
        } else if (event.key === "Escape") {
          event.preventDefault();
          this._closeDialog();
        }
      }

      _openDialog() {
        if (this._open || !this._isEnabled || this._isReadOnly) return;
        this._restoreFocus = this._shadow.activeElement || document.activeElement;
        this._open = true;
        this._query = "";
        this._activeIndex = 0;
        this.render();
      }

      _closeDialog() {
        if (!this._open) return;
        this._open = false;
        this.render();
        requestAnimationFrame(() => {
          if (this._restoreFocus && typeof this._restoreFocus.focus === "function") this._restoreFocus.focus();
        });
      }

      _select(item) {
        if (item && item.action === "assistant") {
          this._open = false;
          this.render();
          this.dispatchEvent(new Event("OpenAssistant"));
          this._openAssistant();
          return;
        }
        const target = safeTarget(item && item.target);
        if (!target) return;
        this.Value = target;
        this._open = false;
        this.render();
        this.dispatchEvent(new Event("Navigate"));
      }

      _openAssistant() {
        if (!this._assistantEnabled || !this._isEnabled || this._isReadOnly) return;
        if (this._assistantExperience === "command-portal") {
          this._openAssistantPortal();
          return;
        }
        this._closeAssistant(false);
        this._restoreFocus = this;
        this._assistantState = "loading";
        this._assistantMessage = "Opening " + this._assistantLabel + "…";
        this.render();
        loadLangflow(this._langflowScriptUrl).then(() => {
          if (!this.isConnected) return;
          this._createAssistantOverlay();
          const chat = document.createElement("langflow-chat");
          chat.setAttribute("host_url", this._langflowHostUrl);
          chat.setAttribute("flow_id", this._langflowFlowId);
          chat.setAttribute("window_title", this._langflowWindowTitle);
          chat.setAttribute("chat_position", this._langflowChatPosition);
          chat.setAttribute("width", String(this._langflowWidth));
          chat.setAttribute("height", String(this._langflowHeight));
          chat.setAttribute("session_id", this._assistantSessionId());
          chat.setAttribute("start_open", "true");
          chat.dataset.northstarOwner = this.getAttribute("name") || "northstar-command-palette";
          chat.style.setProperty("display", "block", "important");
          chat.style.setProperty("position", "relative", "important");
          chat.style.setProperty("width", "100%", "important");
          chat.style.setProperty("height", "100%", "important");
          chat.style.setProperty("max-width", "100%", "important");
          chat.style.setProperty("max-height", "100%", "important");
          chat.style.setProperty("pointer-events", "auto", "important");
          this._assistantAuthCleanup = observeLangflowAuthentication(
            this, this._langflowHostUrl, this._langflowFlowId);
          this._assistantFrame.append(chat);
          this._langflowElement = chat;
          this._syncAssistantOverlay();
          this._assistantState = "ready";
          this._assistantMessage = this._assistantLabel + " opened.";
          this.render();
        }).catch((error) => {
          this._assistantState = "error";
          this._assistantMessage = error && error.message
            ? error.message
            : "The case assistant could not be opened.";
          this.render();
        });
      }

      _openAssistantPortal() {
        this._closeAssistant(false);
        this._caseTypeShortcode = caseTypeShortcodeFromUrl(window.location.href);
        this._restoreFocus = this;
        this._assistantState = "loading";
        this._assistantMessage = "Opening " + this._assistantLabel + "…";
        this._portalStatus = "Loading conversations…";
        this._createAssistantOverlay(true);
        this._assistantFrame.style.pointerEvents = "auto";
        this._assistantFrame.setAttribute("aria-modal", "true");
        this._renderAssistantPortal();
        this._loadPortalSessions().then(() => {
          if (!this.isConnected || !this._assistantOverlay) return;
          this._assistantState = "ready";
          this._assistantMessage = this._assistantLabel + " opened.";
          this._portalStatus = "";
          this._renderAssistantPortal();
        }).catch((error) => {
          this._assistantState = "error";
          this._portalStatus = error && error.message
            ? error.message
            : "The case assistant could not load conversations.";
          this._renderAssistantPortal();
        });
      }

      _portalEndpoint(path) {
        return this._langflowHostUrl.replace(/\/+$/, "") + path;
      }

      _portalStorageKey() {
        return "northstar.langflow.portal." + this._langflowFlowId;
      }

      _caseTypeShortcodeFromUrl(value) {
        return caseTypeShortcodeFromUrl(value);
      }

      _stripPrototypeCaseContext(value) {
        return stripPrototypeCaseContext(value);
      }

      _readPortalMetadata() {
        try {
          const stored = JSON.parse(window.localStorage.getItem(this._portalStorageKey()) || "{}");
          return {
            activeSessionId: text(stored.activeSessionId),
            sessions: Array.isArray(stored.sessions) ? stored.sessions : []
          };
        } catch (_) {
          return {activeSessionId: "", sessions: []};
        }
      }

      _writePortalMetadata() {
        try {
          window.localStorage.setItem(this._portalStorageKey(), JSON.stringify({
            activeSessionId: this._portalSessionId,
            sessions: this._portalSessions.slice(0, 100).map(function (session) {
              return {
                id: session.id,
                title: session.title,
                createdAt: session.createdAt,
                updatedAt: session.updatedAt
              };
            })
          }));
        } catch (_) {
          // Local metadata is a convenience only; Langflow remains the message store.
        }
      }

      async _loadPortalSessions() {
        const metadata = this._readPortalMetadata();
        let remoteIds = [];
        const response = await fetch(this._portalEndpoint(
          "/api/v1/monitor/messages/sessions?flow_id=" +
          encodeURIComponent(this._langflowFlowId)), {method: "GET"});
        if (!response.ok) {
          throw new Error("Conversations could not be loaded (HTTP " + response.status + ").");
        }
        const body = await response.json();
        if (Array.isArray(body)) remoteIds = body.map(String).filter(Boolean);
        const byId = new Map();
        metadata.sessions.forEach(function (session) {
          if (!session || !text(session.id)) return;
          byId.set(String(session.id), {
            id: String(session.id),
            title: trimTitle(session.title, "Conversation"),
            createdAt: session.createdAt || session.updatedAt || new Date().toISOString(),
            updatedAt: session.updatedAt || session.createdAt || new Date().toISOString()
          });
        });
        remoteIds.forEach(function (id) {
          if (!byId.has(id)) {
            byId.set(id, {
              id: id,
              title: "Conversation " + id.slice(0, 8),
              createdAt: new Date(0).toISOString(),
              updatedAt: new Date(0).toISOString()
            });
          }
        });
        this._portalSessions = Array.from(byId.values()).sort(function (left, right) {
          return String(right.updatedAt).localeCompare(String(left.updatedAt));
        });
        const preferred = metadata.activeSessionId &&
          this._portalSessions.some((session) => session.id === metadata.activeSessionId)
          ? metadata.activeSessionId
          : this._portalSessions.length ? this._portalSessions[0].id : "";
        if (!preferred) {
          this._newPortalSession(false);
          return;
        }
        this._portalSessionId = preferred;
        this._writePortalMetadata();
        await this._loadPortalMessages(preferred);
      }

      _newPortalSession(focusComposer) {
        const now = new Date().toISOString();
        const session = {
          id: this._newSessionId(),
          title: "New conversation",
          createdAt: now,
          updatedAt: now
        };
        this._portalSessions = [session].concat(this._portalSessions);
        this._portalSessionId = session.id;
        this._portalMessages = [];
        this._portalAttachments = [];
        this._portalComposerValue = "";
        this._portalStatus = "";
        this._portalFocusComposer = focusComposer !== false;
        this._writePortalMetadata();
        this._renderAssistantPortal();
      }

      async _selectPortalSession(sessionId) {
        if (!sessionId || sessionId === this._portalSessionId || this._portalSending) return;
        this._portalSessionId = sessionId;
        this._portalMessages = [];
        this._portalAttachments = [];
        this._portalStatus = "Loading conversation…";
        this._portalSidebarOpen = false;
        this._writePortalMetadata();
        this._renderAssistantPortal();
        try {
          await this._loadPortalMessages(sessionId);
          this._portalStatus = "";
        } catch (error) {
          this._portalStatus = error && error.message
            ? error.message
            : "The conversation could not be loaded.";
        }
        this._renderAssistantPortal();
      }

      async _loadPortalMessages(sessionId) {
        const query = new URLSearchParams({
          flow_id: this._langflowFlowId,
          session_id: sessionId,
          order_by: "timestamp",
          order: "ASC",
          limit: "500"
        });
        const response = await fetch(this._portalEndpoint(
          "/api/v1/monitor/messages?" + query.toString()), {method: "GET"});
        if (!response.ok) {
          throw new Error("Conversation history could not be loaded (HTTP " +
            response.status + ").");
        }
        const body = await response.json();
        this._portalMessages = (Array.isArray(body) ? body : []).map(function (message) {
          const sender = text(message.sender).toLocaleLowerCase();
          return {
            id: text(message.id) || "message-" + Math.random().toString(36).slice(2),
            role: sender === "user" ? "user" : "assistant",
            text: sender === "user"
              ? stripPrototypeCaseContext(message.text)
              : message.text == null ? "" : String(message.text),
            timestamp: message.timestamp || "",
            files: normalizePortalFiles(message.files)
          };
        }).filter(function (message) {
          return message.text || message.files.length;
        });
        const firstUser = this._portalMessages.find((message) => message.role === "user" && message.text);
        const session = this._portalSessions.find((item) => item.id === sessionId);
        if (session && firstUser && /^Conversation |^New conversation$/.test(session.title)) {
          session.title = trimTitle(firstUser.text, session.title);
          session.updatedAt = this._portalMessages[this._portalMessages.length - 1].timestamp ||
            session.updatedAt;
          this._writePortalMetadata();
        }
      }

      async _clearPortalSession(removeSession) {
        if (!this._portalSessionId || this._portalSending) return;
        const prompt = removeSession
          ? "Delete this conversation and its message history?"
          : "Clear every message in this conversation?";
        if (typeof window.confirm === "function" && !window.confirm(prompt)) return;
        const sessionId = this._portalSessionId;
        this._portalStatus = removeSession ? "Deleting conversation…" : "Clearing conversation…";
        this._renderAssistantPortal();
        try {
          const response = await fetch(this._portalEndpoint(
            "/api/v1/monitor/messages/session/" + encodeURIComponent(sessionId)), {
            method: "DELETE"
          });
          if (!response.ok && response.status !== 404) {
            throw new Error("Conversation could not be cleared (HTTP " + response.status + ").");
          }
          if (removeSession) {
            this._portalSessions = this._portalSessions.filter((session) => session.id !== sessionId);
            if (this._portalSessions.length) {
              this._portalSessionId = this._portalSessions[0].id;
              await this._loadPortalMessages(this._portalSessionId);
            } else {
              this._newPortalSession(false);
              return;
            }
          } else {
            const session = this._portalSessions.find((item) => item.id === sessionId);
            if (session) {
              session.title = "New conversation";
              session.updatedAt = new Date().toISOString();
            }
            this._portalMessages = [];
          }
          this._portalAttachments = [];
          this._portalStatus = "";
          this._writePortalMetadata();
        } catch (error) {
          this._portalStatus = error && error.message
            ? error.message
            : "The conversation could not be changed.";
        }
        this._renderAssistantPortal();
      }

      _portalAllowedExtensions() {
        return this._langflowAllowedFileTypes.split(",").map(function (value) {
          return text(value).toLocaleLowerCase();
        }).filter(Boolean);
      }

      async _addPortalFiles(fileList) {
        const files = Array.from(fileList || []);
        const allowed = this._portalAllowedExtensions();
        for (const file of files) {
          const dot = file.name.lastIndexOf(".");
          const extension = dot >= 0 ? file.name.slice(dot).toLocaleLowerCase() : "";
          if (allowed.length && !allowed.includes(extension)) {
            this._portalStatus = file.name + " is not an allowed file type.";
            this._renderAssistantPortal();
            continue;
          }
          if (file.size > this._langflowMaxFileSizeMb * 1024 * 1024) {
            this._portalStatus = file.name + " exceeds the " +
              this._langflowMaxFileSizeMb + " MB limit.";
            this._renderAssistantPortal();
            continue;
          }
          const attachment = {
            id: this._newSessionId(),
            name: file.name,
            size: file.size,
            type: file.type || "",
            kind: /^image\/(png|jpeg|gif|bmp|webp)$/i.test(file.type) ? "image" : "document",
            status: "uploading",
            path: ""
          };
          this._portalAttachments.push(attachment);
          this._portalStatus = "Adding " + file.name + "…";
          this._renderAssistantPortal();
          try {
            const form = new FormData();
            form.append("file", file, file.name);
            const uploadPath = attachment.kind === "image"
              ? "/api/v1/files/upload/" + encodeURIComponent(this._langflowFlowId)
              : "/api/v2/files";
            const response = await fetch(this._portalEndpoint(uploadPath), {
              method: "POST",
              body: form
            });
            if (!response.ok) {
              throw new Error("Upload failed with HTTP " + response.status + ".");
            }
            const uploaded = await response.json();
            attachment.path = text(uploaded.path || uploaded.file_path);
            if (!attachment.path) throw new Error("Langflow did not return an uploaded file path.");
            attachment.status = "ready";
            this._portalStatus = "";
          } catch (error) {
            attachment.status = "error";
            attachment.error = error && error.message ? error.message : "Upload failed.";
            this._portalStatus = file.name + ": " + attachment.error;
          }
          this._renderAssistantPortal();
        }
      }

      _removePortalAttachment(attachmentId) {
        if (this._portalSending) return;
        this._portalAttachments = this._portalAttachments.filter(
          (attachment) => attachment.id !== attachmentId);
        this._renderAssistantPortal();
      }

      async _sendPortalMessage(value) {
        if (this._portalSending || !this._portalSessionId) return;
        const message = String(value == null ? this._portalComposerValue : value).trim();
        const ready = this._portalAttachments.filter((attachment) => attachment.status === "ready");
        const documents = ready.filter(
          (attachment) => attachment.kind !== "image" && attachment.path);
        const images = ready.filter(
          (attachment) => attachment.kind === "image" && attachment.path);
        if (!message && ready.length === 0) return;
        if (this._portalAttachments.some((attachment) => attachment.status === "uploading")) {
          this._portalStatus = "Wait for file uploads to finish before sending.";
          this._renderAssistantPortal();
          return;
        }
        if (documents.length && !this._langflowFileComponentId) {
          this._portalStatus =
            "This flow needs a configured Read File component before document attachments can be sent.";
          this._renderAssistantPortal();
          return;
        }
        if (images.length && !this._langflowChatInputComponentId) {
          this._portalStatus =
            "This flow needs a configured Chat Input component before image attachments can be sent.";
          this._renderAssistantPortal();
          return;
        }
        const now = new Date().toISOString();
        this._portalMessages.push({
          id: this._newSessionId(),
          role: "user",
          text: message,
          timestamp: now,
          files: ready.map((attachment) => attachment.name)
        });
        const assistant = {
          id: this._newSessionId(),
          role: "assistant",
          text: "",
          timestamp: now,
          files: [],
          streaming: true
        };
        this._portalMessages.push(assistant);
        const session = this._portalSessions.find(
          (item) => item.id === this._portalSessionId);
        if (session) {
          if (session.title === "New conversation") {
            session.title = trimTitle(message || ready[0].name, "New conversation");
          }
          session.updatedAt = now;
        }
        const payload = {
          input_value: injectPrototypeCaseContext(
            message || "Please review the attached files.",
            this._caseTypeShortcode),
          session_id: this._portalSessionId,
          input_type: "chat",
          output_type: "chat"
        };
        if (documents.length || images.length) {
          payload.tweaks = {};
        }
        if (documents.length) {
          payload.tweaks[this._langflowFileComponentId] = {
            path: documents.map((attachment) => attachment.path)
          };
        }
        if (images.length) {
          const imagePaths = images.map((attachment) => attachment.path);
          payload.tweaks[this._langflowChatInputComponentId] = {
            files: imagePaths.length === 1 ? imagePaths[0] : imagePaths
          };
        }
        this._portalSending = true;
        this._portalComposerValue = "";
        this._portalAttachments = [];
        this._portalStatus = "Assistant is responding…";
        this._portalAbortController = typeof AbortController === "function"
          ? new AbortController()
          : null;
        this._writePortalMetadata();
        this._renderAssistantPortal();
        try {
          const response = await fetch(this._portalEndpoint(
            "/api/v1/run/" + encodeURIComponent(this._langflowFlowId) + "?stream=true"), {
            method: "POST",
            headers: {"Content-Type": "application/json", "Accept": "application/json"},
            body: JSON.stringify(payload),
            signal: this._portalAbortController ? this._portalAbortController.signal : undefined
          });
          if (!response.ok) {
            throw new Error("Langflow returned HTTP " + response.status + ".");
          }
          if (response.body && typeof response.body.getReader === "function") {
            await this._consumePortalStream(response, assistant);
          } else {
            const body = await response.json();
            assistant.text = messageTextFromRun(body);
          }
          if (!assistant.text) assistant.text = "The assistant completed without a text response.";
          assistant.streaming = false;
          assistant.timestamp = new Date().toISOString();
          this._portalStatus = "";
        } catch (error) {
          assistant.streaming = false;
          if (error && error.name === "AbortError") {
            assistant.text = assistant.text || "Response stopped.";
            this._portalStatus = "Response stopped.";
          } else {
            assistant.text = assistant.text || "The assistant could not complete this response.";
            this._portalStatus = error && error.message
              ? error.message
              : "The assistant request failed.";
          }
        } finally {
          this._portalSending = false;
          this._portalAbortController = null;
          if (session) session.updatedAt = new Date().toISOString();
          this._writePortalMetadata();
          this._renderAssistantPortal();
        }
      }

      async _consumePortalStream(response, assistant) {
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";
        while (true) {
          const result = await reader.read();
          buffer += decoder.decode(result.value || new Uint8Array(), {stream: !result.done});
          const lines = buffer.split(/\r?\n/);
          buffer = lines.pop() || "";
          for (const line of lines) this._applyPortalStreamLine(line, assistant);
          this._renderAssistantPortal();
          if (result.done) break;
        }
        if (buffer.trim()) this._applyPortalStreamLine(buffer, assistant);
      }

      _applyPortalStreamLine(line, assistant) {
        let payload;
        try {
          payload = JSON.parse(String(line || "").replace(/^data:\s*/, ""));
        } catch (_) {
          return;
        }
        if (payload.event === "token" && payload.data && payload.data.chunk != null) {
          assistant.text += String(payload.data.chunk);
        } else if (payload.event === "end") {
          const finalText = messageTextFromRun(payload.data);
          if (finalText) assistant.text = finalText;
        } else if (payload.event === "add_message" && payload.data &&
          text(payload.data.sender).toLocaleLowerCase() !== "user") {
          const fullText = payload.data.text == null ? "" : String(payload.data.text);
          if (fullText) assistant.text = fullText;
        } else {
          const fullText = messageTextFromRun(payload);
          if (fullText && !assistant.text) assistant.text = fullText;
        }
      }

      _stopPortalResponse() {
        if (this._portalAbortController) this._portalAbortController.abort();
      }

      _renderAssistantPortal() {
        if (!this._assistantFrame) return;
        Array.from(this._assistantFrame.children).forEach((child) => {
          if (child !== this._assistantCloseButton) child.remove();
        });
        const portal = document.createElement("section");
        portal.className = "northstar-agent-portal";
        portal.dataset.sessionId = this._portalSessionId;
        portal.setAttribute("aria-label", this._langflowWindowTitle);

        const sidebar = document.createElement("aside");
        sidebar.className = "northstar-agent-portal__sidebar";
        if (this._portalSidebarOpen) sidebar.classList.add("is-open");
        sidebar.setAttribute("aria-label", "Conversations");
        const brand = document.createElement("div");
        brand.className = "northstar-agent-portal__brand";
        const mark = document.createElement("span");
        mark.className = "northstar-agent-portal__mark";
        mark.setAttribute("aria-hidden", "true");
        mark.textContent = "✦";
        const brandCopy = document.createElement("span");
        const brandTitle = document.createElement("strong");
        brandTitle.textContent = this._langflowWindowTitle;
        const brandSubtitle = document.createElement("small");
        brandSubtitle.textContent = this._caseTypeShortcode
          ? this._caseTypeShortcode + " case command centre"
          : "Case command centre";
        brandCopy.append(brandTitle, brandSubtitle);
        brand.append(mark, brandCopy);

        const newChat = document.createElement("button");
        newChat.type = "button";
        newChat.className = "northstar-agent-portal__new";
        newChat.disabled = this._portalSending;
        newChat.textContent = "＋ New chat";
        newChat.addEventListener("click", () => this._newPortalSession(true));

        const sessionList = document.createElement("div");
        sessionList.className = "northstar-agent-portal__sessions";
        sessionList.setAttribute("role", "list");
        this._portalSessions.forEach((session) => {
          const button = document.createElement("button");
          button.type = "button";
          button.className = "northstar-agent-portal__session";
          if (session.id === this._portalSessionId) button.classList.add("is-active");
          button.setAttribute("role", "listitem");
          button.disabled = this._portalSending;
          const title = document.createElement("strong");
          title.textContent = session.title;
          const time = document.createElement("small");
          time.textContent = formatPortalTime(session.updatedAt);
          button.append(title, time);
          button.addEventListener("click", () => this._selectPortalSession(session.id));
          sessionList.append(button);
        });

        const sidebarNote = document.createElement("p");
        sidebarNote.className = "northstar-agent-portal__note";
        sidebarNote.textContent =
          "Chat attachments provide assistant context and are not case evidence.";
        sidebar.append(brand, newChat, sessionList, sidebarNote);

        const main = document.createElement("div");
        main.className = "northstar-agent-portal__main";
        const header = document.createElement("header");
        header.className = "northstar-agent-portal__header";
        const menu = document.createElement("button");
        menu.type = "button";
        menu.className = "northstar-agent-portal__menu";
        menu.setAttribute("aria-label", "Show conversations");
        menu.setAttribute("aria-expanded", this._portalSidebarOpen ? "true" : "false");
        menu.textContent = "☰";
        menu.addEventListener("click", () => {
          this._portalSidebarOpen = !this._portalSidebarOpen;
          this._renderAssistantPortal();
        });
        const heading = document.createElement("div");
        const current = this._portalSessions.find(
          (session) => session.id === this._portalSessionId);
        const title = document.createElement("h2");
        title.textContent = current ? current.title : "New conversation";
        const subtitle = document.createElement("p");
        subtitle.textContent = "Ask questions, collect details, and use available case tools.";
        heading.append(title, subtitle);
        const actions = document.createElement("div");
        actions.className = "northstar-agent-portal__actions";
        const clear = document.createElement("button");
        clear.type = "button";
        clear.disabled = this._portalSending;
        clear.textContent = "Clear";
        clear.addEventListener("click", () => this._clearPortalSession(false));
        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "is-danger";
        remove.disabled = this._portalSending;
        remove.textContent = "Delete";
        remove.addEventListener("click", () => this._clearPortalSession(true));
        actions.append(clear, remove);
        header.append(menu, heading, actions);

        const messages = document.createElement("div");
        messages.className = "northstar-agent-portal__messages";
        messages.setAttribute("role", "log");
        messages.setAttribute("aria-live", "polite");
        if (!this._portalMessages.length) {
          const empty = document.createElement("div");
          empty.className = "northstar-agent-portal__empty";
          const emptyMark = document.createElement("span");
          emptyMark.setAttribute("aria-hidden", "true");
          emptyMark.textContent = "✦";
          const emptyTitle = document.createElement("h3");
          emptyTitle.textContent = "How can I help with this case?";
          const emptyCopy = document.createElement("p");
          emptyCopy.textContent =
            "Start a conversation or add supporting files for the assistant to review.";
          empty.append(emptyMark, emptyTitle, emptyCopy);
          messages.append(empty);
        } else {
          this._portalMessages.forEach(function (message) {
            const row = document.createElement("article");
            row.className = "northstar-agent-portal__message is-" + message.role;
            const label = document.createElement("strong");
            label.textContent = message.role === "user" ? "You" : "Case Assistant";
            const content = document.createElement("div");
            content.className = "northstar-agent-portal__message-copy";
            const messageText = message.text || (message.streaming ? "Thinking…" : "");
            if (message.role === "assistant" && message.text) {
              content.classList.add("is-markdown");
              appendMarkdown(content, messageText);
            } else {
              content.textContent = messageText;
            }
            row.append(label, content);
            if (message.files && message.files.length) {
              const files = document.createElement("div");
              files.className = "northstar-agent-portal__message-files";
              message.files.forEach(function (name) {
                const chip = document.createElement("span");
                chip.textContent = "📎 " + name;
                files.append(chip);
              });
              row.append(files);
            }
            messages.append(row);
          });
        }

        const footer = document.createElement("footer");
        footer.className = "northstar-agent-portal__footer";
        const attachmentList = document.createElement("div");
        attachmentList.className = "northstar-agent-portal__attachments";
        this._portalAttachments.forEach((attachment) => {
          const chip = document.createElement("span");
          chip.className = "northstar-agent-portal__attachment is-" + attachment.status;
          const label = document.createElement("span");
          label.textContent = "📎 " + attachment.name +
            (attachment.status === "uploading" ? " · uploading" : "");
          const removeFile = document.createElement("button");
          removeFile.type = "button";
          removeFile.disabled = this._portalSending || attachment.status === "uploading";
          removeFile.setAttribute("aria-label", "Remove " + attachment.name);
          removeFile.textContent = "×";
          removeFile.addEventListener("click", () => this._removePortalAttachment(attachment.id));
          chip.append(label, removeFile);
          attachmentList.append(chip);
        });
        const composer = document.createElement("div");
        composer.className = "northstar-agent-portal__composer";
        const fileInput = document.createElement("input");
        fileInput.type = "file";
        fileInput.multiple = true;
        fileInput.accept = this._langflowAllowedFileTypes;
        fileInput.hidden = true;
        fileInput.addEventListener("change", () => {
          this._addPortalFiles(fileInput.files);
          fileInput.value = "";
        });
        const attach = document.createElement("button");
        attach.type = "button";
        attach.className = "northstar-agent-portal__attach";
        attach.disabled = this._portalSending;
        attach.setAttribute("aria-label", "Add files");
        attach.textContent = "＋";
        attach.addEventListener("click", () => fileInput.click());
        const input = document.createElement("textarea");
        input.rows = 1;
        input.value = this._portalComposerValue;
        input.placeholder = "Message the case assistant";
        input.setAttribute("aria-label", "Message the case assistant");
        input.disabled = this._portalSending;
        input.addEventListener("input", () => { this._portalComposerValue = input.value; });
        input.addEventListener("keydown", (event) => {
          if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            this._portalComposerValue = input.value;
            this._sendPortalMessage(input.value);
          }
        });
        const send = document.createElement("button");
        send.type = "button";
        send.className = "northstar-agent-portal__send";
        send.textContent = this._portalSending ? "Stop" : "Send";
        send.addEventListener("click", () => {
          this._portalComposerValue = input.value;
          if (this._portalSending) this._stopPortalResponse();
          else this._sendPortalMessage(input.value);
        });
        composer.append(fileInput, attach, input, send);
        const status = document.createElement("p");
        status.className = "northstar-agent-portal__status";
        status.setAttribute("role", this._assistantState === "error" ? "alert" : "status");
        status.textContent = this._portalStatus;
        footer.append(attachmentList, composer, status);

        main.append(header, messages, footer);
        portal.append(sidebar, main);
        portal.addEventListener("dragover", (event) => {
          event.preventDefault();
          portal.classList.add("is-dragging");
        });
        portal.addEventListener("dragleave", () => portal.classList.remove("is-dragging"));
        portal.addEventListener("drop", (event) => {
          event.preventDefault();
          portal.classList.remove("is-dragging");
          if (!this._portalSending) this._addPortalFiles(event.dataTransfer.files);
        });
        portal.addEventListener("keydown", (event) => {
          if (event.key !== "Tab") return;
          const focusable = Array.from(portal.querySelectorAll(
            "button:not([disabled]),textarea:not([disabled]),input:not([disabled])"
          )).filter((node) => !node.hidden);
          if (!focusable.length) return;
          const first = focusable[0];
          const last = focusable[focusable.length - 1];
          if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
          } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
          }
        });
        this._assistantFrame.append(portal);
        this._syncAssistantOverlay();
        requestAnimationFrame(() => {
          messages.scrollTop = messages.scrollHeight;
          if (this._portalFocusComposer) {
            this._portalFocusComposer = false;
            input.focus();
          }
        });
      }

      _createAssistantOverlay(portalMode) {
        this._closeAssistant(false);
        const overlay = document.createElement("div");
        overlay.className = "northstar-agent-overlay";
        overlay.dataset.northstarOwner = this.getAttribute("name") || "northstar-command-palette";
        overlay.dataset.authenticationMode = this._langflowAuthenticationMode;
        overlay.setAttribute("role", "region");
        overlay.setAttribute("aria-label", this._langflowWindowTitle);
        Object.assign(overlay.style, {
          position: "fixed",
          inset: "0",
          width: "100vw",
          height: "100vh",
          maxWidth: "100vw",
          maxHeight: "100vh",
          margin: "0",
          padding: "0",
          overflow: "hidden",
          pointerEvents: "none",
          zIndex: "9500",
          contain: "layout style paint"
        });

        const frame = document.createElement("div");
        frame.className = "northstar-agent-overlay__frame" +
          (portalMode ? " northstar-agent-overlay__frame--portal" : "");
        frame.setAttribute("role", "dialog");
        frame.setAttribute("aria-modal", "false");
        frame.setAttribute("aria-label", this._langflowWindowTitle);
        Object.assign(frame.style, {
          position: "absolute",
          overflow: "visible",
          pointerEvents: "none",
          maxWidth: "calc(100vw - 24px)",
          maxHeight: "calc(100vh - 24px)"
        });

        const close = document.createElement("button");
        close.type = "button";
        close.className = "northstar-agent-overlay__close";
        close.setAttribute("aria-label", "Close " + this._langflowWindowTitle);
        close.textContent = "Close";
        Object.assign(close.style, {
          position: "absolute",
          top: "8px",
          right: "8px",
          zIndex: "2",
          border: "1px solid #d0d5dd",
          borderRadius: "8px",
          background: "#ffffff",
          color: "#344054",
          font: "600 12px/1 Segoe UI, sans-serif",
          padding: "8px 10px",
          boxShadow: "0 2px 8px rgba(16,24,40,.14)",
          cursor: "pointer",
          pointerEvents: "auto"
        });
        close.addEventListener("click", () => this._closeAssistant(true));
        frame.append(close);
        overlay.append(frame);
        document.body.append(overlay);
        if (portalMode) loadStyles(this, overlay);

        this._assistantOverlay = overlay;
        this._assistantFrame = frame;
        this._assistantCloseButton = close;
        window.addEventListener("resize", this._onAssistantResize);
        this._assistantModalObserver = new MutationObserver(
          this._syncAssistantModalState.bind(this));
        this._assistantModalObserver.observe(document.body, {
          subtree: true, childList: true, attributes: true,
          attributeFilter: ["class", "style", "aria-hidden"]
        });
        this._syncAssistantModalState();
      }

      _syncAssistantOverlay() {
        if (!this._assistantFrame) return;
        const portalMobile = this._assistantExperience === "command-portal" &&
          window.innerWidth <= 620;
        const safeWidth = portalMobile ? window.innerWidth :
          Math.max(280, Math.min(this._langflowWidth, window.innerWidth - 24));
        const safeHeight = portalMobile ? window.innerHeight :
          Math.max(360, Math.min(this._langflowHeight, window.innerHeight - 24));
        this._assistantFrame.style.width = safeWidth + "px";
        this._assistantFrame.style.height = safeHeight + "px";
        this._assistantFrame.style.removeProperty("top");
        this._assistantFrame.style.removeProperty("right");
        this._assistantFrame.style.removeProperty("bottom");
        this._assistantFrame.style.removeProperty("left");
        this._assistantFrame.style.removeProperty("transform");
        const vertical = this._langflowChatPosition.indexOf("top") === 0 ? "top" :
          this._langflowChatPosition.indexOf("center") === 0 ? "center" : "bottom";
        const horizontal = this._langflowChatPosition.indexOf("left") >= 0 ? "left" :
          this._langflowChatPosition.indexOf("center") >= 0 ? "center" : "right";
        if (portalMobile) {
          this._assistantFrame.style.top = "0";
          this._assistantFrame.style.left = "0";
        } else if (vertical === "top") this._assistantFrame.style.top = "12px";
        else if (vertical === "bottom") this._assistantFrame.style.bottom = "12px";
        else this._assistantFrame.style.top = "50%";
        if (!portalMobile) {
          if (horizontal === "left") this._assistantFrame.style.left = "12px";
          else if (horizontal === "right") this._assistantFrame.style.right = "12px";
          else this._assistantFrame.style.left = "50%";
        }
        const transforms = [];
        if (horizontal === "center") transforms.push("translateX(-50%)");
        if (vertical === "center") transforms.push("translateY(-50%)");
        if (transforms.length) this._assistantFrame.style.transform = transforms.join(" ");
        if (this._langflowElement) {
          this._langflowElement.setAttribute("width", String(safeWidth));
          this._langflowElement.setAttribute("height", String(safeHeight));
        }
      }

      _syncAssistantModalState() {
        if (!this._assistantOverlay || !this._assistantFrame) return;
        const visible = function (node) {
          if (!node || !node.isConnected) return false;
          const style = getComputedStyle(node);
          return style.display !== "none" && style.visibility !== "hidden" &&
            node.getAttribute("aria-hidden") !== "true" &&
            !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
        };
        const criticalModal = Array.from(document.querySelectorAll(
          ".popup,.message-box,.ui-dialog,.k2-modal,[data-k2-modal]"
        )).find(function (node) {
          return !node.closest(".northstar-agent-overlay") && visible(node);
        });
        this._assistantFrame.style.visibility = criticalModal ? "hidden" : "visible";
        this._assistantFrame.style.pointerEvents = "none";
        const hidden = criticalModal ? "true" : "false";
        if (this._assistantOverlay.getAttribute("aria-hidden") !== hidden) {
          this._assistantOverlay.setAttribute("aria-hidden", hidden);
        }
      }

      _showAssistantAuthenticationError(status) {
        if (!this._assistantFrame) return;
        if (this._langflowElement) this._langflowElement.remove();
        this._langflowElement = null;
        const prior = this._assistantFrame.querySelector(".northstar-agent-overlay__error");
        if (prior) prior.remove();
        const message = document.createElement("div");
        message.className = "northstar-agent-overlay__error";
        message.setAttribute("role", "alert");
        message.setAttribute("aria-live", "assertive");
        message.tabIndex = -1;
        Object.assign(message.style, {
          boxSizing: "border-box",
          width: "100%",
          maxWidth: "420px",
          margin: "56px auto 0",
          border: "1px solid #fda29b",
          borderRadius: "12px",
          background: "#fff",
          color: "#912018",
          padding: "18px",
          boxShadow: "0 18px 48px rgba(16,24,40,.22)",
          pointerEvents: "auto"
        });
        message.textContent = "Agent unavailable — authentication required. The Langflow server returned HTTP " +
          status + ".";
        this._assistantFrame.append(message);
        this._assistantState = "error";
        this._assistantMessage = "Agent unavailable — authentication required.";
        this.render();
        message.focus();
      }

      _closeAssistant(returnFocus) {
        window.removeEventListener("resize", this._onAssistantResize);
        if (this._portalAbortController) this._portalAbortController.abort();
        if (this._assistantModalObserver) this._assistantModalObserver.disconnect();
        if (this._assistantAuthCleanup) this._assistantAuthCleanup();
        this._assistantModalObserver = null;
        this._assistantAuthCleanup = null;
        if (this._assistantOverlay) this._assistantOverlay.remove();
        else if (this._langflowElement) this._langflowElement.remove();
        this._assistantOverlay = null;
        this._assistantFrame = null;
        this._assistantCloseButton = null;
        this._langflowElement = null;
        this._portalAbortController = null;
        this._portalSending = false;
        this._portalSidebarOpen = false;
        if (returnFocus) {
          requestAnimationFrame(() => {
            const trigger = this._shadow &&
              this._shadow.querySelector(".northstar-command__trigger");
            if (trigger && typeof trigger.focus === "function") trigger.focus();
          });
        }
      }

      _assistantSessionId() {
        const key = "northstar.langflow." + this._langflowFlowId;
        try {
          let value = window.sessionStorage.getItem(key);
          if (!value) {
            value = this._newSessionId();
            window.sessionStorage.setItem(key, value);
          }
          return value;
        } catch (_) {
          if (!this._fallbackSessionId) this._fallbackSessionId = this._newSessionId();
          return this._fallbackSessionId;
        }
      }

      _newSessionId() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
          return window.crypto.randomUUID();
        }
        return "northstar-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2);
      }
    });
  }
}());
