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
        this._assistantEnabled = false;
        this._assistantExperience = "embedded-widget";
        this._assistantLabel = "Ask Case Assistant";
        this._assistantDescription = "Ask questions and take supported case actions";
        this._langflowHostUrl = "";
        this._langflowFlowId = "";
        this._langflowScriptUrl = "https://cdn.jsdelivr.net/gh/langflow-ai/langflow-embedded-chat@v1.0.8/dist/build/static/js/bundle.min.js";
        this._langflowAuthenticationMode = "server-proxy";
        this._langflowWindowTitle = "Case Assistant";
        this._langflowChatPosition = "bottom-right";
        this._langflowWidth = 420;
        this._langflowHeight = 640;
        this._langflowFileComponentId = "";
        this._langflowChatInputComponentId = "";
        this._langflowAllowedFileTypes = ".pdf,.txt,.md,.csv,.docx,.xlsx,.png,.jpg,.jpeg,.gif,.bmp,.webp";
        this._langflowMaxFileSizeMb = 25;
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
      get AssistantEnabled() { return this._assistantEnabled; }
      set AssistantEnabled(value) { this._assistantEnabled = value === true || value === "true"; this.render(); changed(this, "AssistantEnabled"); }
      get AssistantExperience() { return this._assistantExperience; }
      set AssistantExperience(value) {
        this._assistantExperience = value === "command-portal" ? value : "embedded-widget";
        this.render();
        changed(this, "AssistantExperience");
      }
      get AssistantLabel() { return this._assistantLabel; }
      set AssistantLabel(value) { this._assistantLabel = value || "Ask Case Assistant"; this.render(); changed(this, "AssistantLabel"); }
      get AssistantDescription() { return this._assistantDescription; }
      set AssistantDescription(value) { this._assistantDescription = value || "Ask questions and take supported case actions"; changed(this, "AssistantDescription"); }
      get LangflowHostUrl() { return this._langflowHostUrl; }
      set LangflowHostUrl(value) { this._langflowHostUrl = value == null ? "" : String(value); changed(this, "LangflowHostUrl"); }
      get LangflowFlowId() { return this._langflowFlowId; }
      set LangflowFlowId(value) { this._langflowFlowId = value == null ? "" : String(value); changed(this, "LangflowFlowId"); }
      get LangflowScriptUrl() { return this._langflowScriptUrl; }
      set LangflowScriptUrl(value) { this._langflowScriptUrl = value == null ? "" : String(value); changed(this, "LangflowScriptUrl"); }
      get LangflowAuthenticationMode() { return this._langflowAuthenticationMode; }
      set LangflowAuthenticationMode(value) {
        this._langflowAuthenticationMode = value === "server-open-alpha" ? value : "server-proxy";
        changed(this, "LangflowAuthenticationMode");
      }
      get LangflowWindowTitle() { return this._langflowWindowTitle; }
      set LangflowWindowTitle(value) { this._langflowWindowTitle = value || "Case Assistant"; changed(this, "LangflowWindowTitle"); }
      get LangflowChatPosition() { return this._langflowChatPosition; }
      set LangflowChatPosition(value) { this._langflowChatPosition = value || "bottom-right"; changed(this, "LangflowChatPosition"); }
      get LangflowWidth() { return this._langflowWidth; }
      set LangflowWidth(value) { this._langflowWidth = Number(value) || 420; changed(this, "LangflowWidth"); }
      get LangflowHeight() { return this._langflowHeight; }
      set LangflowHeight(value) { this._langflowHeight = Number(value) || 640; changed(this, "LangflowHeight"); }
      get LangflowFileComponentId() { return this._langflowFileComponentId; }
      set LangflowFileComponentId(value) { this._langflowFileComponentId = value == null ? "" : String(value); changed(this, "LangflowFileComponentId"); }
      get LangflowChatInputComponentId() { return this._langflowChatInputComponentId; }
      set LangflowChatInputComponentId(value) { this._langflowChatInputComponentId = value == null ? "" : String(value); changed(this, "LangflowChatInputComponentId"); }
      get LangflowAllowedFileTypes() { return this._langflowAllowedFileTypes; }
      set LangflowAllowedFileTypes(value) { this._langflowAllowedFileTypes = value == null ? "" : String(value); changed(this, "LangflowAllowedFileTypes"); }
      get LangflowMaxFileSizeMb() { return this._langflowMaxFileSizeMb; }
      set LangflowMaxFileSizeMb(value) { this._langflowMaxFileSizeMb = Number(value) || 25; changed(this, "LangflowMaxFileSizeMb"); }
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
      listItemsChangedCallback(itemsChangedEventArgs) {
        this.Suggestions = itemsChangedEventArgs && Array.isArray(itemsChangedEventArgs.NewItems)
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
          const preview = document.createElement("div");
          preview.className = "northstar-command-design";
          const label = document.createElement("span");
          label.textContent = this._placeholder;
          const shortcut = document.createElement("kbd");
          shortcut.textContent = this._assistantEnabled
            ? "Ctrl K · " + (this._assistantExperience === "command-portal" ? "Chat portal" : "Assistant")
            : "Ctrl K";
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
