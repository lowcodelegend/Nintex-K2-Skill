(function () {
  "use strict";

  const NORTHSTAR_DEFAULTS = Object.freeze({
    insight: {
      title: "Three related defects may share a machining cause",
      text: "Cases from Apex Precision Metals reference the same cell, alloy batch, and surface condition. Estimated exposure: 1,840 units.",
      action: "Review cluster →",
      route: "case-workspace",
      key: "SNC-2026-0148"
    },
    metrics: [
      {id: "open-cases", label: "Open cases", value: "128", delta: "↓ 8.6% vs last month", icon: "▤", tone: "positive"},
      {id: "sla-at-risk", label: "SLA at risk", value: "12", delta: "↑ 3 need intervention", icon: "◷", tone: "critical"},
      {id: "overdue-actions", label: "Overdue actions", value: "7", delta: "2 supplier-owned", icon: "↗", tone: "critical"},
      {id: "first-pass-yield", label: "First-pass yield", value: "94.2%", delta: "↑ 1.7 pts", icon: "◎", tone: "positive"}
    ],
    attention: [
      {id: "SNC-2026-0148", title: "Surface pitting on actuator housing", due: "Breached by 4h", tone: "critical"},
      {id: "SNC-2026-0146", title: "Incorrect hardness certificate", due: "2h remaining", tone: "warning"},
      {id: "SNC-2026-0141", title: "Packaging seal integrity failure", due: "At risk · 7h", tone: "warning"}
    ],
    stages: [
      {label: "Validate", value: "18", percent: 48},
      {label: "Contain", value: "11", percent: 30},
      {label: "Investigate", value: "27", percent: 74},
      {label: "Review", value: "9", percent: 24},
      {label: "Corrective action", value: "36", percent: 96}
    ],
    suppliers: [
      {name: "Apex Precision Metals", signal: "Risk rising · 6 cases", score: "62", tone: "breach"},
      {name: "Orion Forge Ltd", signal: "Stable · 3 cases", score: "78", tone: "risk"},
      {name: "Nexus Polymers", signal: "Improving · 2 cases", score: "91", tone: "good"}
    ]
  });

  function northstarChanged(control, property) {
    if (window.K2 && typeof window.K2.RaisePropertyChanged === "function") {
      window.K2.RaisePropertyChanged(control, property);
    }
  }

  function northstarEvent(control, name) {
    control.dispatchEvent(new Event(name));
  }

  function northstarEnsureResourceMetadata(control) {
    if (!control.ControlType) control.ControlType = "northstar-case-homepage";
    if (!Array.isArray(control.RuntimeScriptFileNames) || control.RuntimeScriptFileNames.length === 0) {
      control.RuntimeScriptFileNames = ["northstar-runtime.js"];
    }
    if (!Array.isArray(control.DesigntimeScriptFileNames) || control.DesigntimeScriptFileNames.length === 0) {
      control.DesigntimeScriptFileNames = ["northstar-designtime.js"];
    }
    if (!Array.isArray(control.RuntimeStyleFileNames) || control.RuntimeStyleFileNames.length === 0) {
      control.RuntimeStyleFileNames = [
        "northstar-fonts.css",
        "northstar-prototype.css",
        "northstar-host.css"
      ];
    }
    if (!Array.isArray(control.DesigntimeStyleFileNames) || control.DesigntimeStyleFileNames.length === 0) {
      control.DesigntimeStyleFileNames = ["northstar-designtime.css"];
    }
  }

  function northstarLoadStyles(control, shadowRoot) {
    northstarEnsureResourceMetadata(control);
    const styles = window.SourceCode && window.SourceCode.Forms && window.SourceCode.Forms.ControlStyles;
    if (styles && typeof styles.loadStyleResources === "function") {
      return Promise.resolve(styles.loadStyleResources(control, shadowRoot));
    }
    if (window.K2 && typeof window.K2.LoadControlStyleResources === "function") {
      return Promise.resolve(window.K2.LoadControlStyleResources(control, shadowRoot));
    }
    return Promise.resolve();
  }

  function northstarText(value, fallback) {
    return value === undefined || value === null || value === "" ? fallback : String(value);
  }

  function northstarNumber(value, fallback, minimum, maximum) {
    const number = Number(value);
    if (!Number.isFinite(number)) return fallback;
    return Math.min(maximum, Math.max(minimum, number));
  }

  function northstarInitials(name) {
    return String(name || "")
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part.charAt(0).toUpperCase())
      .join("") || "NS";
  }

  function northstarPick(item, names, fallback) {
    for (const name of names) {
      if (Object.prototype.hasOwnProperty.call(item, name) && item[name] !== null && item[name] !== "") {
        return item[name];
      }
    }
    return fallback;
  }

  function northstarMergeData(items) {
    const result = {
      insight: Object.assign({}, NORTHSTAR_DEFAULTS.insight),
      metrics: NORTHSTAR_DEFAULTS.metrics.map((item) => Object.assign({}, item)),
      attention: NORTHSTAR_DEFAULTS.attention.map((item) => Object.assign({}, item)),
      stages: NORTHSTAR_DEFAULTS.stages.map((item) => Object.assign({}, item)),
      suppliers: NORTHSTAR_DEFAULTS.suppliers.map((item) => Object.assign({}, item))
    };
    if (!Array.isArray(items)) return result;

    for (const raw of items) {
      if (!raw || typeof raw !== "object") continue;
      const kind = northstarText(northstarPick(raw, ["kind", "Kind", "RecordKind"], ""), "").toLowerCase();
      const id = northstarText(northstarPick(raw, ["id", "Id", "Code"], ""), "");
      if (kind === "insight") {
        result.insight = {
          title: northstarText(northstarPick(raw, ["title", "Title"], null), result.insight.title),
          text: northstarText(northstarPick(raw, ["text", "Text", "Description"], null), result.insight.text),
          action: northstarText(northstarPick(raw, ["action", "Action", "ActionLabel"], null), result.insight.action),
          route: northstarText(northstarPick(raw, ["route", "Route"], null), result.insight.route),
          key: northstarText(northstarPick(raw, ["key", "Key", "RecordKey"], null), result.insight.key)
        };
      } else if (kind === "metric") {
        const match = result.metrics.find((item) => item.id === id);
        if (!match) continue;
        match.label = northstarText(northstarPick(raw, ["label", "Label"], null), match.label);
        match.value = northstarText(northstarPick(raw, ["value", "Value"], null), match.value);
        match.delta = northstarText(northstarPick(raw, ["delta", "Delta"], null), match.delta);
        match.icon = northstarText(northstarPick(raw, ["icon", "Icon"], null), match.icon);
        match.tone = northstarText(northstarPick(raw, ["tone", "Tone"], null), match.tone);
      } else if (kind === "attention") {
        const index = result.attention.findIndex((item) => item.id === id);
        const value = {
          id: id || northstarText(northstarPick(raw, ["key", "Key"], null), "Case"),
          title: northstarText(northstarPick(raw, ["title", "Title"], null), "Case requiring attention"),
          due: northstarText(northstarPick(raw, ["due", "Due", "SLA"], null), "Needs review"),
          tone: northstarText(northstarPick(raw, ["tone", "Tone"], null), "warning")
        };
        if (index >= 0) result.attention[index] = value;
        else result.attention.push(value);
      } else if (kind === "stage") {
        const label = northstarText(northstarPick(raw, ["label", "Label", "Stage"], null), id);
        const match = result.stages.find((item) => item.label.toLowerCase() === label.toLowerCase());
        const value = {
          label,
          value: northstarText(northstarPick(raw, ["value", "Value", "Count"], null), "0"),
          percent: northstarNumber(northstarPick(raw, ["percent", "Percent"], 0), 0, 0, 100)
        };
        if (match) Object.assign(match, value);
        else result.stages.push(value);
      } else if (kind === "supplier") {
        const name = northstarText(northstarPick(raw, ["name", "Name", "Supplier"], null), id);
        const match = result.suppliers.find((item) => item.name.toLowerCase() === name.toLowerCase());
        const value = {
          name,
          signal: northstarText(northstarPick(raw, ["signal", "Signal"], null), "No signal"),
          score: northstarText(northstarPick(raw, ["score", "Score"], null), "0"),
          tone: northstarText(northstarPick(raw, ["tone", "Tone"], null), "risk")
        };
        if (match) Object.assign(match, value);
        else result.suppliers.push(value);
      }
    }
    return result;
  }

  if (!window.customElements.get("northstar-case-homepage")) {
    window.customElements.define("northstar-case-homepage", class NorthstarCaseHomepage extends K2BaseControl {
      constructor() {
        super();
        this._value = "";
        this._data = "";
        this._applicationName = "Northstar";
        this._applicationSubtitle = "Quality operations";
        this._userName = "Alex Morgan";
        this._userRole = "Quality manager";
        this._greeting = "Good morning, Alex";
        this._useFullViewport = true;
        this._width = "100%";
        this._height = "1000px";
        this._isVisible = true;
        this._isEnabled = true;
        this._isReadOnly = false;
        this._tabIndex = "0";
        this._dataItems = [];
        this._viewModel = northstarMergeData([]);
        this._rendering = false;
        this._stylesReady = null;
        this._toastTimer = null;
        this._pendingError = "";
        this._documentKeyHandler = (event) => this._onDocumentKey(event);
      }

      static get observedAttributes() {
        return [
          "controlstyle", "name", "controltype",
          "value", "width", "height", "isvisible", "isenabled", "isreadonly", "tabindex", "usefullviewport"
        ];
      }

      get Value() { return this._value; }
      set Value(value) {
        const next = value == null ? "" : String(value);
        if (next === this._value) return;
        this._value = next;
        northstarChanged(this, "Value");
      }
      get Data() { return this._data; }
      set Data(value) {
        this._data = value == null ? "" : String(value);
        if (this._data.trim()) {
          try {
            const parsed = JSON.parse(this._data);
            if (Array.isArray(parsed)) {
              this._dataItems = parsed;
              this._viewModel = northstarMergeData(parsed);
              this.render();
            }
          } catch (error) {
            this._showError("Dashboard data could not be read.");
          }
        }
        northstarChanged(this, "Data");
      }
      get ApplicationName() { return this._applicationName; }
      set ApplicationName(value) { this._applicationName = value || "Northstar"; this.render(); northstarChanged(this, "ApplicationName"); }
      get ApplicationSubtitle() { return this._applicationSubtitle; }
      set ApplicationSubtitle(value) { this._applicationSubtitle = value || "Quality operations"; this.render(); northstarChanged(this, "ApplicationSubtitle"); }
      get UserName() { return this._userName; }
      set UserName(value) { this._userName = value || "Alex Morgan"; this.render(); northstarChanged(this, "UserName"); }
      get UserRole() { return this._userRole; }
      set UserRole(value) { this._userRole = value || "Quality manager"; this.render(); northstarChanged(this, "UserRole"); }
      get Greeting() { return this._greeting; }
      set Greeting(value) { this._greeting = value || "Good morning, Alex"; this.render(); northstarChanged(this, "Greeting"); }
      get UseFullViewport() { return this._useFullViewport; }
      set UseFullViewport(value) {
        this._useFullViewport = value === true || value === "true";
        this.setAttribute("data-full-viewport", this._useFullViewport ? "true" : "false");
        northstarChanged(this, "UseFullViewport");
      }
      get Width() { return this._width; }
      set Width(value) {
        this._width = value || "100%";
        this.style.width = /^\d+$/.test(String(this._width)) ? `${this._width}px` : String(this._width);
        northstarChanged(this, "Width");
      }
      get Height() { return this._height; }
      set Height(value) {
        this._height = value || "1000px";
        this.style.minHeight = /^\d+$/.test(String(this._height)) ? `${this._height}px` : String(this._height);
        northstarChanged(this, "Height");
      }
      get IsVisible() { return this._isVisible; }
      set IsVisible(value) {
        this._isVisible = value === true || value === "true";
        this.setAttribute("data-hidden", this._isVisible ? "false" : "true");
        northstarChanged(this, "IsVisible");
      }
      get IsEnabled() { return this._isEnabled; }
      set IsEnabled(value) {
        this._isEnabled = value === true || value === "true";
        this.setAttribute("data-disabled", this._isEnabled ? "false" : "true");
        this._syncInteractivity();
        northstarChanged(this, "IsEnabled");
      }
      get IsReadOnly() { return this._isReadOnly; }
      set IsReadOnly(value) {
        this._isReadOnly = value === true || value === "true";
        this.setAttribute("data-readonly", this._isReadOnly ? "true" : "false");
        this._syncInteractivity();
        northstarChanged(this, "IsReadOnly");
      }
      get TabIndex() { return this._tabIndex; }
      set TabIndex(value) {
        this._tabIndex = value == null ? "0" : String(value);
        const main = this._shadow && this._shadow.getElementById("main");
        if (main) main.tabIndex = Number(this._tabIndex) || 0;
        northstarChanged(this, "TabIndex");
      }

      attributeChangedCallback(name, oldValue, newValue) {
        if (oldValue === newValue) return;
        if (name === "controlstyle" || name === "name" || name === "controltype") {
          super.attributeChangedCallback(name, oldValue, newValue);
          return;
        }
        const map = {
          value: "Value",
          width: "Width",
          height: "Height",
          isvisible: "IsVisible",
          isenabled: "IsEnabled",
          isreadonly: "IsReadOnly",
          tabindex: "TabIndex",
          usefullviewport: "UseFullViewport"
        };
        const property = map[name.toLowerCase()];
        if (property) this[property] = newValue;
      }

      listConfigChangedCallback() {}

      listItemsChangedCallback(args) {
        const items = args && Array.isArray(args.NewItems) ? args.NewItems : [];
        this._dataItems = items;
        this._viewModel = northstarMergeData(items);
        try { this._data = JSON.stringify(items); } catch (error) { this._data = ""; }
        this.render();
        northstarChanged(this, "Data");
      }

      connectedCallback() {
        if (this._isConnected) return;
        super.connectedCallback();
        document.addEventListener("keydown", this._documentKeyHandler);
        this.setAttribute("data-full-viewport", this._useFullViewport ? "true" : "false");
        this.render();
      }

      disconnectedCallback() {
        document.removeEventListener("keydown", this._documentKeyHandler);
        clearTimeout(this._toastTimer);
        super.disconnectedCallback();
      }

      render() {
        if (!this.isConnected || this._rendering) return;
        this._rendering = true;
        this.ensureShadow();
        if (!this._stylesReady) {
          this._stylesReady = northstarLoadStyles(this, this._shadow);
        }
        this._stylesReady.then(
          () => this._draw(),
          () => this._draw()
        );
      }

      _draw() {
        for (const child of Array.from(this._shadow.children)) {
          if (!(child.tagName === "STYLE" && child.hasAttribute("data-id"))) child.remove();
        }
        const surface = document.createElement("div");
        surface.className = "northstar-surface";
        surface.innerHTML = `
          <a class="skip-link" href="#main">Skip to content</a>
          <div class="app-shell">
            <aside class="sidebar" id="sidebar" aria-label="Primary navigation">
              <button class="brand" type="button" data-route="command" aria-label="Northstar home">
                <span class="brand-mark" aria-hidden="true">N</span>
                <span><b data-field="application-name"></b><small data-field="application-subtitle"></small></span>
              </button>
              <nav id="primaryNav">
                <button class="nav-item active" type="button" data-route="command"><span class="nav-icon" aria-hidden="true">⌂</span><span>Command centre</span></button>
                <button class="nav-item" type="button" data-route="my-work"><span class="nav-icon" aria-hidden="true">✓</span><span>My work</span><span class="nav-count">8</span></button>
                <button class="nav-item" type="button" data-route="case-search"><span class="nav-icon" aria-hidden="true">▤</span><span>All cases</span></button>
                <button class="nav-item" type="button" data-route="corrective-actions"><span class="nav-icon" aria-hidden="true">↗</span><span>Corrective actions</span><span class="nav-count">5</span></button>
                <button class="nav-item" type="button" data-route="reports"><span class="nav-icon" aria-hidden="true">◫</span><span>Insights & reports</span></button>
                <div class="nav-label">Management</div>
                <button class="nav-item" type="button" data-route="supplier-performance"><span class="nav-icon" aria-hidden="true">◇</span><span>Suppliers</span></button>
                <button class="nav-item" type="button" data-route="administration"><span class="nav-icon" aria-hidden="true">⚙</span><span>Configuration</span></button>
              </nav>
              <div class="sidebar-foot">
                <button class="mini-profile" type="button" data-route="profile">
                  <span class="avatar" data-field="user-initials"></span>
                  <span><b data-field="user-name"></b><small data-field="user-role"></small></span><span aria-hidden="true">•••</span>
                </button>
              </div>
            </aside>
            <div class="app-frame">
              <header class="topbar">
                <button class="icon-button mobile-only" type="button" id="menuButton" aria-label="Open menu">☰</button>
                <button class="search-trigger" type="button" id="searchButton"><span aria-hidden="true">⌕</span><span>Search cases, suppliers, lots…</span><kbd>Ctrl K</kbd></button>
                <div class="top-actions">
                  <button class="icon-button" type="button" data-toast-message="You’re all caught up" aria-label="Notifications">♢<span class="notification-dot"></span></button>
                  <button class="button primary compact" type="button" data-create-case><span aria-hidden="true">＋</span> New case</button>
                </div>
              </header>
              <main id="main" tabindex="-1">
                <div class="page fade-in">
                  <header class="page-head">
                    <div>
                      <p class="eyebrow">Wednesday, 22 July</p>
                      <h1 data-field="greeting"></h1>
                      <p class="page-sub">Here is what changed, what needs attention, and where quality is trending.</p>
                    </div>
                    <div class="date-filter">
                      <div class="segmented" aria-label="Reporting period">
                        <button type="button" data-period="7d">7d</button>
                        <button type="button" class="active" data-period="30d">30d</button>
                        <button type="button" data-period="90d">90d</button>
                      </div>
                      <button class="button" type="button" data-route="export-brief">Export brief</button>
                    </div>
                  </header>
                  <section class="insight-strip" aria-labelledby="northstar-insight-title">
                    <div class="insight-icon" aria-hidden="true">✦</div>
                    <div><h2 id="northstar-insight-title" data-field="insight-title"></h2><p data-field="insight-text"></p></div>
                    <button class="button" type="button" data-insight-action></button>
                  </section>
                  <section class="kpi-grid" aria-label="Quality measures" data-region="metrics"></section>
                  <section class="dashboard-grid">
                    <article class="panel">
                      <div class="panel-head">
                        <div><h2>Nonconformance trend</h2><p>Opened and resolved cases · 30 days</p></div>
                        <div class="legend"><span><i style="background:#6257d9"></i>Opened</span><span><i style="background:#39b9a8"></i>Resolved</span></div>
                      </div>
                      <div class="chart-wrap">
                        <svg class="trend-chart" viewBox="0 0 700 240" role="img" aria-label="Opened cases rose early in July then declined">
                          <defs><linearGradient id="area" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#6257d9" stop-opacity=".25"/><stop offset="1" stop-color="#6257d9" stop-opacity="0"/></linearGradient></defs>
                          <line class="grid-line" x1="35" x2="680" y1="40" y2="40"/><line class="grid-line" x1="35" x2="680" y1="90" y2="90"/>
                          <line class="grid-line" x1="35" x2="680" y1="140" y2="140"/><line class="grid-line" x1="35" x2="680" y1="190" y2="190"/>
                          <path class="trend-fill" d="M35 170 C100 165 120 90 185 110 S270 145 330 82 S445 55 500 100 S600 145 680 90 L680 210 L35 210Z"/>
                          <path class="trend-line" d="M35 170 C100 165 120 90 185 110 S270 145 330 82 S445 55 500 100 S600 145 680 90"/>
                          <text class="axis-label" x="35" y="232">Jul 1</text><text class="axis-label" x="185" y="232">Jul 7</text>
                          <text class="axis-label" x="330" y="232">Jul 13</text><text class="axis-label" x="500" y="232">Jul 19</text>
                          <text class="axis-label" x="650" y="232">Jul 25</text><circle class="trend-dot" cx="500" cy="100" r="5"/>
                        </svg>
                      </div>
                    </article>
                    <aside class="panel">
                      <div class="panel-head"><div><h2>Attention now</h2><p>Ranked by risk, impact and SLA</p></div><button class="link-button" type="button" data-route="my-work">View all</button></div>
                      <div class="work-list" data-region="attention"></div>
                    </aside>
                    <article class="panel">
                      <div class="panel-head"><div><h2>Where work is accumulating</h2><p>Active cases by lifecycle stage</p></div></div>
                      <div class="stage-bars" data-region="stages"></div>
                    </article>
                    <article class="panel">
                      <div class="panel-head"><div><h2>Supplier signal</h2><p>Risk-adjusted rolling 90 days</p></div><button class="link-button" type="button" data-route="supplier-performance">Open scorecards</button></div>
                      <div class="work-list" data-region="suppliers"></div>
                    </article>
                  </section>
                </div>
              </main>
            </div>
          </div>
          <div class="scrim" id="scrim"></div>
          <dialog class="command-dialog" id="commandDialog" aria-labelledby="command-title">
            <div class="command-head"><span aria-hidden="true">⌕</span><input id="commandInput" aria-label="Search" placeholder="Search or jump to…" autocomplete="off"><kbd>Esc</kbd></div>
            <div class="command-body" id="commandResults"><div class="command-group" id="command-title">Suggested</div></div>
            <footer><span>↑↓ Navigate</span><span>↵ Open</span><span>K2 governed data</span></footer>
          </dialog>
          <div class="toast" id="toast" role="status" aria-live="polite"></div>
          <div class="visually-hidden" id="northstarError" role="alert" aria-live="assertive"></div>`;
        this._shadow.append(surface);
        this._surface = surface;
        this._applyText();
        this._renderMetrics();
        this._renderAttention();
        this._renderStages();
        this._renderSuppliers();
        this._bind();
        this._syncInteractivity();
        if (this._pendingError) this._showError(this._pendingError);
        this._hasRendered = true;
        this._rendering = false;
        this.dispatchEvent(new Event("Rendered"));
      }

      _applyText() {
        const fields = {
          "application-name": this._applicationName,
          "application-subtitle": this._applicationSubtitle,
          "user-name": this._userName,
          "user-role": this._userRole,
          "user-initials": northstarInitials(this._userName),
          greeting: this._greeting,
          "insight-title": this._viewModel.insight.title,
          "insight-text": this._viewModel.insight.text
        };
        for (const [name, value] of Object.entries(fields)) {
          const target = this._shadow.querySelector(`[data-field="${name}"]`);
          if (target) target.textContent = value;
        }
        const brand = this._shadow.querySelector(".brand");
        if (brand) brand.setAttribute("aria-label", `${this._applicationName} home`);
        const insightAction = this._shadow.querySelector("[data-insight-action]");
        if (insightAction) insightAction.textContent = this._viewModel.insight.action;
      }

      _renderMetrics() {
        const region = this._shadow.querySelector('[data-region="metrics"]');
        if (!region) return;
        region.replaceChildren();
        for (const item of this._viewModel.metrics.slice(0, 4)) {
          const button = document.createElement("button");
          button.type = "button";
          button.className = "kpi";
          button.dataset.route = "case-search";
          button.dataset.filter = item.id;
          const top = document.createElement("div");
          top.className = "kpi-top";
          const label = document.createElement("span");
          label.textContent = item.label;
          const icon = document.createElement("span");
          icon.className = "kpi-icon";
          icon.setAttribute("aria-hidden", "true");
          icon.textContent = item.icon;
          top.append(label, icon);
          const value = document.createElement("div");
          value.className = "kpi-value";
          value.textContent = item.value;
          const delta = document.createElement("div");
          delta.className = item.tone === "critical" ? "delta bad" : "delta";
          delta.textContent = item.delta;
          button.append(top, value, delta);
          region.append(button);
        }
      }

      _renderAttention() {
        const region = this._shadow.querySelector('[data-region="attention"]');
        if (!region) return;
        region.replaceChildren();
        for (const item of this._viewModel.attention.slice(0, 3)) {
          const button = document.createElement("button");
          button.type = "button";
          button.className = "work-card";
          button.dataset.route = "case-workspace";
          button.dataset.key = item.id;
          const rail = document.createElement("i");
          rail.className = item.tone === "critical" ? "risk-rail critical" : "risk-rail";
          rail.setAttribute("aria-hidden", "true");
          const text = document.createElement("div");
          const heading = document.createElement("h3");
          heading.textContent = item.id;
          const description = document.createElement("p");
          description.textContent = item.title;
          text.append(heading, description);
          const due = document.createElement("span");
          due.className = "due";
          due.textContent = item.due;
          button.append(rail, text, due);
          region.append(button);
        }
      }

      _renderStages() {
        const region = this._shadow.querySelector('[data-region="stages"]');
        if (!region) return;
        region.replaceChildren();
        for (const item of this._viewModel.stages.slice(0, 8)) {
          const row = document.createElement("div");
          row.className = "stage-row";
          const label = document.createElement("span");
          label.textContent = item.label;
          const bar = document.createElement("div");
          bar.className = "bar";
          const fill = document.createElement("i");
          fill.style.width = `${northstarNumber(item.percent, 0, 0, 100)}%`;
          bar.append(fill);
          const value = document.createElement("b");
          value.textContent = item.value;
          row.append(label, bar, value);
          region.append(row);
        }
      }

      _renderSuppliers() {
        const region = this._shadow.querySelector('[data-region="suppliers"]');
        if (!region) return;
        region.replaceChildren();
        for (const item of this._viewModel.suppliers.slice(0, 3)) {
          const row = document.createElement("div");
          row.className = "work-card";
          const avatar = document.createElement("span");
          avatar.className = "avatar";
          avatar.textContent = northstarInitials(item.name);
          const text = document.createElement("div");
          const heading = document.createElement("h3");
          heading.textContent = item.name;
          const signal = document.createElement("p");
          signal.textContent = item.signal;
          text.append(heading, signal);
          const score = document.createElement("span");
          score.className = `status ${item.tone}`;
          score.textContent = `Score ${item.score}`;
          row.append(avatar, text, score);
          region.append(row);
        }
      }

      _bind() {
        for (const element of this._shadow.querySelectorAll("[data-route]")) {
          element.addEventListener("click", () => {
            if (!this._interactive()) return;
            this._navigate(element.dataset.route, element.dataset.key || "", element.dataset.filter || "");
          });
        }
        for (const element of this._shadow.querySelectorAll("[data-create-case]")) {
          element.addEventListener("click", () => this._createCase());
        }
        const search = this._shadow.getElementById("searchButton");
        if (search) search.addEventListener("click", () => this._openSearch());
        const input = this._shadow.getElementById("commandInput");
        if (input) input.addEventListener("input", () => this._renderCommands(input.value));
        const menu = this._shadow.getElementById("menuButton");
        const sidebar = this._shadow.getElementById("sidebar");
        const scrim = this._shadow.getElementById("scrim");
        if (menu && sidebar && scrim) menu.addEventListener("click", () => {
          if (!this._interactive()) return;
          sidebar.classList.add("open");
          scrim.classList.add("open");
        });
        if (scrim && sidebar) scrim.addEventListener("click", () => {
          sidebar.classList.remove("open");
          scrim.classList.remove("open");
        });
        for (const element of this._shadow.querySelectorAll("[data-toast-message]")) {
          element.addEventListener("click", () => this._toast(element.dataset.toastMessage));
        }
        for (const element of this._shadow.querySelectorAll("[data-period]")) {
          element.addEventListener("click", () => {
            if (!this._interactive()) return;
            for (const peer of this._shadow.querySelectorAll("[data-period]")) peer.classList.remove("active");
            element.classList.add("active");
            this._setCommand({command: "refresh", period: element.dataset.period});
            northstarEvent(this, "RefreshRequested");
          });
        }
        const insight = this._shadow.querySelector("[data-insight-action]");
        if (insight) insight.addEventListener("click", () => {
          if (!this._interactive()) return;
          this._navigate(this._viewModel.insight.route, this._viewModel.insight.key, "insight");
        });
      }

      _interactive() { return this._isEnabled && !this._isReadOnly; }

      _syncInteractivity() {
        if (!this._shadow) return;
        const disabled = !this._interactive();
        for (const element of this._shadow.querySelectorAll("button,input")) {
          element.disabled = disabled;
          element.setAttribute("aria-disabled", disabled ? "true" : "false");
        }
      }

      _setCommand(command) {
        const json = JSON.stringify(command);
        this.Value = json.length <= 255 ? json : JSON.stringify({command: command.command || "navigate", route: command.route || ""});
      }

      _navigate(route, key, filter) {
        this._setCommand({command: "navigate", route, key, filter});
        northstarEvent(this, "Navigate");
        const sidebar = this._shadow.getElementById("sidebar");
        const scrim = this._shadow.getElementById("scrim");
        if (sidebar) sidebar.classList.remove("open");
        if (scrim) scrim.classList.remove("open");
      }

      _createCase() {
        if (!this._interactive()) return;
        this._setCommand({command: "create", route: "case-initiation"});
        northstarEvent(this, "CreateCase");
      }

      _openSearch() {
        if (!this._interactive()) return;
        const dialog = this._shadow.getElementById("commandDialog");
        const input = this._shadow.getElementById("commandInput");
        if (!dialog || typeof dialog.showModal !== "function") {
          this._setCommand({command: "search"});
          northstarEvent(this, "Search");
          return;
        }
        this._renderCommands("");
        if (!dialog.open) dialog.showModal();
        setTimeout(() => input && input.focus(), 30);
        this._setCommand({command: "search"});
        northstarEvent(this, "Search");
      }

      _renderCommands(query) {
        const results = this._shadow.getElementById("commandResults");
        if (!results) return;
        const commands = [
          {icon: "⌂", title: "Command centre", subtitle: "Operational overview", route: "command"},
          {icon: "✓", title: "My work", subtitle: "8 items need attention", route: "my-work"},
          {icon: "＋", title: "Report a nonconformance", subtitle: "Start a guided intake", route: "case-initiation", create: true},
          {icon: "▤", title: "SNC-2026-0148", subtitle: "Surface pitting on actuator housing", route: "case-workspace", key: "SNC-2026-0148"},
          {icon: "◫", title: "Insights & reports", subtitle: "Quality performance and governance", route: "reports"}
        ];
        const normalized = String(query || "").trim().toLowerCase();
        const hits = commands.filter((item) => `${item.title} ${item.subtitle}`.toLowerCase().includes(normalized));
        results.replaceChildren();
        const label = document.createElement("div");
        label.className = "command-group";
        label.textContent = normalized ? "Results" : "Suggested";
        results.append(label);
        for (const item of hits) {
          const button = document.createElement("button");
          button.type = "button";
          button.className = "command-item";
          const icon = document.createElement("i");
          icon.textContent = item.icon;
          const content = document.createElement("span");
          const title = document.createElement("b");
          title.textContent = item.title;
          const subtitle = document.createElement("small");
          subtitle.textContent = item.subtitle;
          content.append(title, subtitle);
          button.append(icon, content);
          button.addEventListener("click", () => {
            const dialog = this._shadow.getElementById("commandDialog");
            if (dialog && dialog.open) dialog.close();
            if (item.create) this._createCase();
            else this._navigate(item.route, item.key || "", "command");
          });
          results.append(button);
        }
      }

      _onDocumentKey(event) {
        if (!this.isConnected || !this._interactive()) return;
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
          event.preventDefault();
          this._openSearch();
        }
      }

      _toast(message) {
        const toast = this._shadow.getElementById("toast");
        if (!toast) return;
        toast.textContent = message;
        toast.classList.add("show");
        clearTimeout(this._toastTimer);
        this._toastTimer = setTimeout(() => toast.classList.remove("show"), 2300);
      }

      _showError(message) {
        this._pendingError = message || "";
        const error = this._shadow && this._shadow.getElementById("northstarError");
        if (error) error.textContent = this._pendingError;
      }

      execute(objInfo) {
        const method = northstarText(
          objInfo && (objInfo.methodName || objInfo.MethodName || objInfo.name || objInfo.Name),
          ""
        );
        if (method === "refresh") {
          this._setCommand({command: "refresh"});
          northstarEvent(this, "RefreshRequested");
        } else if (method === "openSearch") {
          this._openSearch();
        } else if (method === "focusMain") {
          const main = this._shadow && this._shadow.getElementById("main");
          if (main) main.focus();
        } else {
          this._showError("The requested control method is not supported.");
        }
      }
    });
  }
}());
