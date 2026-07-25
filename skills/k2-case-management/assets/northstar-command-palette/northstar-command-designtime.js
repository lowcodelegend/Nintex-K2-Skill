(function () {
  "use strict";

  function changed(control, property) {
    if (window.K2 && typeof window.K2.RaisePropertyChanged === "function") {
      window.K2.RaisePropertyChanged(control, property);
    }
  }

  function loadStyles(control, parent) {
    if (!control.ControlType) control.ControlType = "northstar-command-palette";
    if (!Array.isArray(control.DesigntimeStyleFileNames) || control.DesigntimeStyleFileNames.length === 0) {
      control.DesigntimeStyleFileNames = ["northstar-command-designtime.css"];
    }
    if (!Array.isArray(control.RuntimeStyleFileNames)) {
      control.RuntimeStyleFileNames = ["northstar-command-runtime.css"];
    }
    const styles = window.SourceCode && window.SourceCode.Forms && window.SourceCode.Forms.ControlStyles;
    return styles && typeof styles.loadStyleResources === "function"
      ? Promise.resolve(styles.loadStyleResources(control, parent))
      : Promise.resolve();
  }

  if (!window.customElements.get("northstar-command-palette")) {
    window.customElements.define("northstar-command-palette", class NorthstarCommandPaletteDesign extends K2BaseControl {
      constructor() {
        super();
        this._value = "";
        this._suggestions = [];
        this._searchUrlTemplate = "";
        this._placeholder = "Search cases, work and reports";
        this._width = "100%";
        this._height = "48px";
        this._isVisible = true;
        this._isEnabled = true;
        this._isReadOnly = false;
        this._tabIndex = "0";
        this._rendering = false;
      }
      static get observedAttributes() {
        return ["controlstyle", "name", "controltype", "value", "width", "height", "isvisible", "isenabled", "isreadonly", "tabindex"];
      }
      get Value() { return this._value; }
      set Value(value) { this._value = value == null ? "" : String(value); changed(this, "Value"); }
      get Suggestions() { return this._suggestions; }
      set Suggestions(value) { this._suggestions = value || []; changed(this, "Suggestions"); }
      get SearchUrlTemplate() { return this._searchUrlTemplate; }
      set SearchUrlTemplate(value) { this._searchUrlTemplate = value == null ? "" : String(value); changed(this, "SearchUrlTemplate"); }
      get Placeholder() { return this._placeholder; }
      set Placeholder(value) { this._placeholder = value || "Search cases, work and reports"; this.render(); changed(this, "Placeholder"); }
      get Width() { return this._width; }
      set Width(value) { this._width = value || "100%"; this.style.width = this._width; changed(this, "Width"); }
      get Height() { return this._height; }
      set Height(value) { this._height = value || "48px"; this.style.minHeight = this._height; changed(this, "Height"); }
      get IsVisible() { return this._isVisible; }
      set IsVisible(value) { this._isVisible = value === true || value === "true"; this.style.display = this._isVisible ? "" : "none"; changed(this, "IsVisible"); }
      get IsEnabled() { return this._isEnabled; }
      set IsEnabled(value) { this._isEnabled = value === true || value === "true"; changed(this, "IsEnabled"); }
      get IsReadOnly() { return this._isReadOnly; }
      set IsReadOnly(value) { this._isReadOnly = value === true || value === "true"; changed(this, "IsReadOnly"); }
      get TabIndex() { return this._tabIndex; }
      set TabIndex(value) { this._tabIndex = value == null ? "0" : String(value); changed(this, "TabIndex"); }
      connectedCallback() { super.connectedCallback(); this.render(); }
      disconnectedCallback() { super.disconnectedCallback(); }
      attributeChangedCallback(name, oldValue, newValue) {
        super.attributeChangedCallback(name, oldValue, newValue);
        const properties = {value: "Value", width: "Width", height: "Height", isvisible: "IsVisible", isenabled: "IsEnabled", isreadonly: "IsReadOnly", tabindex: "TabIndex"};
        if (properties[name]) this[properties[name]] = newValue;
      }
      listItemsChangedCallback(items) { this.Suggestions = items; }
      render() {
        if (!this.isConnected || this._rendering) return;
        this._rendering = true;
        this.ensureShadow();
        const draw = () => {
          for (const child of Array.from(this._shadow.children)) {
            if (!(child.tagName === "STYLE" && child.hasAttribute("data-id"))) child.remove();
          }
          const preview = document.createElement("div");
          preview.className = "northstar-command-design";
          const label = document.createElement("span");
          label.textContent = this._placeholder;
          const shortcut = document.createElement("kbd");
          shortcut.textContent = "Ctrl K";
          preview.append(label, shortcut);
          this._shadow.append(preview);
          this._hasRendered = true;
          this._rendering = false;
          this.dispatchEvent(new Event("Rendered"));
        };
        loadStyles(this, this._shadow).then(draw, draw);
      }
    });
  }
}());
