(function () {
  "use strict";

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

  function northstarDesignChanged(control, property) {
    if (window.K2 && typeof window.K2.RaisePropertyChanged === "function") {
      window.K2.RaisePropertyChanged(control, property);
    }
  }

  if (!window.customElements.get("northstar-case-homepage")) {
    window.customElements.define("northstar-case-homepage", class NorthstarCaseHomepageDesign extends K2BaseControl {
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
        this._rendering = false;
      }

      get Value() { return this._value; }
      set Value(value) { this._value = value == null ? "" : String(value); northstarDesignChanged(this, "Value"); }
      get Data() { return this._data; }
      set Data(value) { this._data = value == null ? "" : String(value); northstarDesignChanged(this, "Data"); }
      get ApplicationName() { return this._applicationName; }
      set ApplicationName(value) { this._applicationName = value || "Northstar"; this.render(); northstarDesignChanged(this, "ApplicationName"); }
      get ApplicationSubtitle() { return this._applicationSubtitle; }
      set ApplicationSubtitle(value) { this._applicationSubtitle = value || "Quality operations"; northstarDesignChanged(this, "ApplicationSubtitle"); }
      get UserName() { return this._userName; }
      set UserName(value) { this._userName = value || "Alex Morgan"; northstarDesignChanged(this, "UserName"); }
      get UserRole() { return this._userRole; }
      set UserRole(value) { this._userRole = value || "Quality manager"; northstarDesignChanged(this, "UserRole"); }
      get Greeting() { return this._greeting; }
      set Greeting(value) { this._greeting = value || "Good morning, Alex"; this.render(); northstarDesignChanged(this, "Greeting"); }
      get UseFullViewport() { return this._useFullViewport; }
      set UseFullViewport(value) { this._useFullViewport = value === true || value === "true"; northstarDesignChanged(this, "UseFullViewport"); }
      get Width() { return this._width; }
      set Width(value) { this._width = value || "100%"; this.style.width = this._width; northstarDesignChanged(this, "Width"); }
      get Height() { return this._height; }
      set Height(value) { this._height = value || "1000px"; northstarDesignChanged(this, "Height"); }
      get IsVisible() { return this._isVisible; }
      set IsVisible(value) { this._isVisible = value === true || value === "true"; this.style.display = this._isVisible ? "" : "none"; northstarDesignChanged(this, "IsVisible"); }
      get IsEnabled() { return this._isEnabled; }
      set IsEnabled(value) { this._isEnabled = value === true || value === "true"; northstarDesignChanged(this, "IsEnabled"); }
      get IsReadOnly() { return this._isReadOnly; }
      set IsReadOnly(value) { this._isReadOnly = value === true || value === "true"; northstarDesignChanged(this, "IsReadOnly"); }
      get TabIndex() { return this._tabIndex; }
      set TabIndex(value) { this._tabIndex = value == null ? "0" : String(value); northstarDesignChanged(this, "TabIndex"); }

      listConfigChangedCallback() {}
      listItemsChangedCallback(args) {
        this._dataItems = args && Array.isArray(args.NewItems) ? args.NewItems : [];
        northstarDesignChanged(this, "Data");
      }

      connectedCallback() {
        super.connectedCallback();
        this.render();
      }

      render() {
        if (!this.isConnected || this._rendering) return;
        this._rendering = true;
        this.ensureShadow();
        const draw = () => {
          const existing = this._shadow.querySelector(".northstar-design");
          if (existing) existing.remove();
          const surface = document.createElement("div");
          surface.className = "northstar-design";
          surface.innerHTML =
            '<aside class="northstar-design__nav"><div class="northstar-design__brand"></div>' +
            "<span>⌂ Command centre</span><span>✓ My work</span><span>▤ Cases</span><span>＋ New case</span>" +
            '</aside><main class="northstar-design__main"><h2></h2><p>Modern K2 Web Component · runtime data binds through one governed projection.</p>' +
            '<div class="northstar-design__metrics">' +
            '<div class="northstar-design__metric">Open cases<b>128</b></div>' +
            '<div class="northstar-design__metric">SLA at risk<b>12</b></div>' +
            '<div class="northstar-design__metric">Overdue actions<b>7</b></div>' +
            '<div class="northstar-design__metric">First-pass yield<b>94.2%</b></div>' +
            "</div></main>";
          surface.querySelector(".northstar-design__brand").textContent = this._applicationName;
          surface.querySelector("h2").textContent = this._greeting;
          this._shadow.append(surface);
          this._hasRendered = true;
          this._rendering = false;
          this.dispatchEvent(new Event("Rendered"));
        };
        northstarLoadStyles(this, this._shadow).then(draw, draw);
      }
    });
  }
}());
