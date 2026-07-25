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

  if (!window.customElements.get("northstar-command-palette")) {
    window.customElements.define("northstar-command-palette", class NorthstarCommandPalette extends K2BaseControl {
      constructor() {
        super();
        this._value = "";
        this._suggestions = [];
        this._searchUrlTemplate = "/Runtime/Runtime/Form/All%20Cases?q={query}";
        this._placeholder = "Search cases, work and reports";
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
      }

      static get observedAttributes() {
        return ["controlstyle", "name", "controltype", "value", "width", "height", "isvisible", "isenabled", "isreadonly", "tabindex"];
      }

      get Value() { return this._value; }
      set Value(value) { this._value = text(value); changed(this, "Value"); }
      get Suggestions() { return this._suggestions; }
      set Suggestions(value) { this._suggestions = normalize(value); this.render(); changed(this, "Suggestions"); }
      get SearchUrlTemplate() { return this._searchUrlTemplate; }
      set SearchUrlTemplate(value) { this._searchUrlTemplate = text(value) || this._searchUrlTemplate; changed(this, "SearchUrlTemplate"); }
      get Placeholder() { return this._placeholder; }
      set Placeholder(value) { this._placeholder = text(value) || "Search cases, work and reports"; this.render(); changed(this, "Placeholder"); }
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
        document.addEventListener("keydown", this._onDocumentKeyDown);
        this.render();
      }

      disconnectedCallback() {
        document.removeEventListener("keydown", this._onDocumentKeyDown);
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

      listItemsChangedCallback(items) {
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
        const target = safeTarget(item && item.target);
        if (!target) return;
        this.Value = target;
        this._open = false;
        this.render();
        this.dispatchEvent(new Event("Navigate"));
      }
    });
  }
}());
