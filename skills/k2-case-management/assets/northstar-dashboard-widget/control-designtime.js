(function () {
  "use strict";

  function changed(control, property) {
    if (window.K2 && typeof window.K2.RaisePropertyChanged === "function") {
      window.K2.RaisePropertyChanged(control, property);
    }
  }

  function loadStyles(control, parent) {
    if (!control.ControlType) control.ControlType = "northstar-dashboard-widget";
    if (!Array.isArray(control.DesigntimeStyleFileNames) || control.DesigntimeStyleFileNames.length === 0) {
      control.DesigntimeStyleFileNames = ["control-designtime.css"];
    }
    if (!Array.isArray(control.RuntimeStyleFileNames)) control.RuntimeStyleFileNames = [];
    const styles = window.SourceCode && window.SourceCode.Forms && window.SourceCode.Forms.ControlStyles;
    return styles && typeof styles.loadStyleResources === "function"
      ? Promise.resolve(styles.loadStyleResources(control, parent))
      : Promise.resolve();
  }

  if (!window.customElements.get("northstar-dashboard-widget")) {
    window.customElements.define("northstar-dashboard-widget", class NorthstarDashboardWidgetDesign extends K2BaseControl {
      constructor() {
        super();
        this._value = "";
        this._data = [];
        this._variant = "trend";
        this._heading = "Dashboard insight";
        this._subtitle = "";
        this._actionLabel = "";
        this._actionTarget = "";
        this._emptyMessage = "No data in this period.";
        this._width = "100%";
        this._height = "260px";
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
      get Data() { return this._data; }
      set Data(value) { this._data = value; changed(this, "Data"); }
      get Variant() { return this._variant; }
      set Variant(value) { this._variant = value || "trend"; this.render(); changed(this, "Variant"); }
      get Heading() { return this._heading; }
      set Heading(value) { this._heading = value || "Dashboard insight"; this.render(); changed(this, "Heading"); }
      get Subtitle() { return this._subtitle; }
      set Subtitle(value) { this._subtitle = value || ""; this.render(); changed(this, "Subtitle"); }
      get ActionLabel() { return this._actionLabel; }
      set ActionLabel(value) { this._actionLabel = value || ""; this.render(); changed(this, "ActionLabel"); }
      get ActionTarget() { return this._actionTarget; }
      set ActionTarget(value) { this._actionTarget = value || ""; changed(this, "ActionTarget"); }
      get EmptyMessage() { return this._emptyMessage; }
      set EmptyMessage(value) { this._emptyMessage = value || "No data in this period."; changed(this, "EmptyMessage"); }
      get Width() { return this._width; }
      set Width(value) { this._width = value || "100%"; this.style.width = this._width; changed(this, "Width"); }
      get Height() { return this._height; }
      set Height(value) { this._height = value || "260px"; this.style.minHeight = this._height; changed(this, "Height"); }
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
      listItemsChangedCallback(itemsChangedEventArgs) {
        this.Data = itemsChangedEventArgs && Array.isArray(itemsChangedEventArgs.NewItems)
          ? itemsChangedEventArgs.NewItems
          : itemsChangedEventArgs;
      }
      render() {
        if (!this.isConnected || this._rendering) return;
        this._rendering = true;
        this.ensureShadow();
        const draw = () => {
          for (const child of Array.from(this._shadow.children)) {
            if (!(child.tagName === "STYLE" && child.hasAttribute("data-id"))) child.remove();
          }
          const preview = document.createElement("section");
          preview.setAttribute("aria-label", "Northstar Dashboard Widget design preview");
          const heading = document.createElement("strong");
          heading.textContent = this._heading;
          const kind = document.createElement("span");
          kind.textContent = (this._subtitle ? this._subtitle + " · " : "") + this._variant + " · governed list binding";
          const bars = document.createElement("div");
          bars.className = "northstar-dashboard-preview";
          for (const width of ["78%", "52%", "67%"]) {
            const bar = document.createElement("i");
            bar.style.width = width;
            bars.append(bar);
          }
          preview.append(heading, kind, bars);
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
