(function () {
  "use strict";

  function starterChanged(control, property) {
    if (window.K2 && typeof window.K2.RaisePropertyChanged === "function") {
      window.K2.RaisePropertyChanged(control, property);
    }
  }

  function starterLoadStyles(control, parent) {
    if (!control.ControlType) control.ControlType = "{{TAG_NAME}}";
    if (!Array.isArray(control.DesigntimeStyleFileNames) || control.DesigntimeStyleFileNames.length === 0) {
      control.DesigntimeStyleFileNames = ["control-designtime.css"];
    }
    if (!Array.isArray(control.RuntimeStyleFileNames)) control.RuntimeStyleFileNames = [];
    const styles = window.SourceCode && window.SourceCode.Forms && window.SourceCode.Forms.ControlStyles;
    return styles && typeof styles.loadStyleResources === "function"
      ? Promise.resolve(styles.loadStyleResources(control, parent))
      : Promise.resolve();
  }

  if (!window.customElements.get("{{TAG_NAME}}")) {
    window.customElements.define("{{TAG_NAME}}", class StarterDesignControl extends K2BaseControl {
      constructor() {
        super();
        this._value = "";
        this._width = "100%";
        this._height = "64px";
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
      set Value(value) { this._value = value == null ? "" : String(value); this.render(); starterChanged(this, "Value"); }
      get Width() { return this._width; }
      set Width(value) { this._width = value || "100%"; this.style.width = this._width; starterChanged(this, "Width"); }
      get Height() { return this._height; }
      set Height(value) { this._height = value || "64px"; this.style.height = this._height; starterChanged(this, "Height"); }
      get IsVisible() { return this._isVisible; }
      set IsVisible(value) { this._isVisible = value === true || value === "true"; this.style.display = this._isVisible ? "" : "none"; starterChanged(this, "IsVisible"); }
      get IsEnabled() { return this._isEnabled; }
      set IsEnabled(value) { this._isEnabled = value === true || value === "true"; starterChanged(this, "IsEnabled"); }
      get IsReadOnly() { return this._isReadOnly; }
      set IsReadOnly(value) { this._isReadOnly = value === true || value === "true"; starterChanged(this, "IsReadOnly"); }
      get TabIndex() { return this._tabIndex; }
      set TabIndex(value) { this._tabIndex = value == null ? "0" : String(value); starterChanged(this, "TabIndex"); }
      connectedCallback() { super.connectedCallback(); this.render(); }
      disconnectedCallback() { super.disconnectedCallback(); }
      attributeChangedCallback(name, oldValue, newValue) {
        super.attributeChangedCallback(name, oldValue, newValue);
        const properties = {value: "Value", width: "Width", height: "Height", isvisible: "IsVisible", isenabled: "IsEnabled", isreadonly: "IsReadOnly", tabindex: "TabIndex"};
        if (properties[name]) this[properties[name]] = newValue;
      }
      render() {
        if (!this.isConnected || this._rendering) return;
        this._rendering = true;
        this.ensureShadow();
        const draw = () => {
          for (const child of Array.from(this._shadow.children)) {
            if (!(child.tagName === "STYLE" && child.hasAttribute("data-id"))) child.remove();
          }
          const preview = document.createElement("div");
          preview.textContent = this._value || "{{DISPLAY_NAME}}";
          this._shadow.append(preview);
          this._hasRendered = true;
          this._rendering = false;
          this.dispatchEvent(new Event("Rendered"));
        };
        starterLoadStyles(this, this._shadow).then(draw, draw);
      }
    });
  }
}());
