(function () {
  "use strict";

  const SVG_NS = "http://www.w3.org/2000/svg";
  const VALID_VARIANTS = ["trend", "attention", "stage", "supplier"];

  function changed(control, property) {
    if (window.K2 && typeof window.K2.RaisePropertyChanged === "function") {
      window.K2.RaisePropertyChanged(control, property);
    }
  }

  function loadStyles(control, parent) {
    if (!control.ControlType) control.ControlType = "northstar-dashboard-widget";
    if (!Array.isArray(control.RuntimeStyleFileNames) || control.RuntimeStyleFileNames.length === 0) {
      control.RuntimeStyleFileNames = ["control-runtime.css"];
    }
    if (!Array.isArray(control.DesigntimeStyleFileNames)) {
      control.DesigntimeStyleFileNames = ["control-designtime.css"];
    }
    const styles = window.SourceCode && window.SourceCode.Forms && window.SourceCode.Forms.ControlStyles;
    return styles && typeof styles.loadStyleResources === "function"
      ? Promise.resolve(styles.loadStyleResources(control, parent))
      : Promise.resolve();
  }

  function text(value) {
    return value == null ? "" : String(value).trim();
  }

  function asBoolean(value) {
    return value === true || String(value).toLowerCase() === "true";
  }

  function number(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
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
    return items.slice(0, 100).filter(function (item) {
      return item && typeof item === "object";
    });
  }

  function element(tag, className, value) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (value != null) node.textContent = String(value);
    return node;
  }

  function svgElement(tag, attributes) {
    const node = document.createElementNS(SVG_NS, tag);
    Object.keys(attributes || {}).forEach(function (name) {
      node.setAttribute(name, String(attributes[name]));
    });
    return node;
  }

  function formatCount(value) {
    return new Intl.NumberFormat(undefined, {maximumFractionDigits: 1}).format(number(value));
  }

  if (!window.customElements.get("northstar-dashboard-widget")) {
    window.customElements.define("northstar-dashboard-widget", class NorthstarDashboardWidget extends K2BaseControl {
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
        this._stylesReady = null;
        this._showDataTable = false;
      }

      static get observedAttributes() {
        return ["controlstyle", "name", "controltype", "value", "width", "height", "isvisible", "isenabled", "isreadonly", "tabindex"];
      }

      get Value() { return this._value; }
      set Value(value) { this._value = text(value); changed(this, "Value"); }
      get Data() { return this._data; }
      set Data(value) {
        this._data = normalize(value);
        this.render();
        changed(this, "Data");
      }
      get Variant() { return this._variant; }
      set Variant(value) {
        const candidate = text(value).toLowerCase();
        this._variant = VALID_VARIANTS.includes(candidate) ? candidate : "trend";
        this.render();
        changed(this, "Variant");
      }
      get Heading() { return this._heading; }
      set Heading(value) { this._heading = text(value) || "Dashboard insight"; this.render(); changed(this, "Heading"); }
      get Subtitle() { return this._subtitle; }
      set Subtitle(value) { this._subtitle = text(value); this.render(); changed(this, "Subtitle"); }
      get ActionLabel() { return this._actionLabel; }
      set ActionLabel(value) { this._actionLabel = text(value); this.render(); changed(this, "ActionLabel"); }
      get ActionTarget() { return this._actionTarget; }
      set ActionTarget(value) { this._actionTarget = text(value); this.render(); changed(this, "ActionTarget"); }
      get EmptyMessage() { return this._emptyMessage; }
      set EmptyMessage(value) { this._emptyMessage = text(value) || "No data in this period."; this.render(); changed(this, "EmptyMessage"); }
      get Width() { return this._width; }
      set Width(value) { this._width = value || "100%"; this.style.width = this._width; changed(this, "Width"); }
      get Height() { return this._height; }
      set Height(value) { this._height = value || "260px"; this.style.minHeight = this._height; changed(this, "Height"); }
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
        this.render();
      }

      disconnectedCallback() {
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
        this.Data = items;
      }

      render() {
        if (!this.isConnected || this._rendering) return;
        this._rendering = true;
        this.ensureShadow();
        if (!this._stylesReady) this._stylesReady = loadStyles(this, this._shadow);
        this._stylesReady.then(this._draw.bind(this), this._draw.bind(this));
      }

      _clear() {
        for (const child of Array.from(this._shadow.children)) {
          if (!(child.tagName === "STYLE" && child.hasAttribute("data-id"))) child.remove();
        }
      }

      _draw() {
        this._clear();
        const section = element("section", "northstar-widget northstar-widget--" + this._variant);
        section.setAttribute("aria-label", this._heading);
        const header = element("header", "northstar-widget__header");
        const headingCopy = element("div", "northstar-widget__heading-copy");
        headingCopy.append(element("h2", "northstar-widget__heading", this._heading));
        if (this._subtitle) headingCopy.append(element("p", "northstar-widget__subtitle", this._subtitle));
        header.append(headingCopy);
        if (this._actionLabel && this._variant !== "trend") header.append(this._actionButton());
        section.append(header);

        if (!this._data.length) {
          const empty = element("p", "northstar-widget__empty", this._emptyMessage);
          empty.setAttribute("role", "status");
          section.append(empty);
        } else if (this._variant === "trend") {
          this._drawTrend(section);
        } else if (this._variant === "attention") {
          this._drawAttention(section);
        } else if (this._variant === "stage") {
          this._drawStage(section);
        } else {
          this._drawSupplier(section);
        }
        if (this._actionLabel && this._variant === "trend") section.append(this._actionButton());

        this._shadow.append(section);
        this._hasRendered = true;
        this._rendering = false;
        this.dispatchEvent(new Event("Rendered"));
      }

      _drawTrend(section) {
        const data = this._data.map(function (item, index) {
          return {
            label: text(pick(item, ["PeriodLabel", "periodLabel", "Label", "label"])) || String(index + 1),
            opened: number(pick(item, ["OpenedCount", "openedCount", "OpenCount", "openCount", "CaseCount"])),
            resolved: number(pick(item, ["ResolvedCount", "resolvedCount", "ClosedCount", "closedCount"]))
          };
        });
        const max = Math.max(1, ...data.map(function (item) { return Math.max(item.opened, item.resolved); }));
        const width = 640;
        const height = 210;
        const left = 30;
        const top = 12;
        const right = 10;
        const bottom = 32;
        const plotWidth = width - left - right;
        const plotHeight = height - top - bottom;
        const x = function (index) { return left + (data.length === 1 ? plotWidth / 2 : index * plotWidth / (data.length - 1)); };
        const y = function (value) { return top + plotHeight - (value / max) * plotHeight; };
        const coordinates = function (key) {
          return data.map(function (item, index) { return [x(index), y(item[key])]; });
        };
        const smoothPath = function (key) {
          const points = coordinates(key);
          if (!points.length) return "";
          let path = "M " + points[0][0] + " " + points[0][1];
          for (let index = 1; index < points.length; index += 1) {
            const previous = points[index - 1];
            const current = points[index];
            const middle = (previous[0] + current[0]) / 2;
            path += " C " + middle + " " + previous[1] + ", " + middle + " " + current[1] + ", " + current[0] + " " + current[1];
          }
          return path;
        };

        const legend = element("div", "northstar-widget__legend");
        [["opened", "Opened"], ["resolved", "Resolved"]].forEach(function (entry) {
          const item = element("span", "northstar-widget__legend-item");
          item.append(element("i", "northstar-widget__legend-swatch northstar-widget__legend-swatch--" + entry[0]), document.createTextNode(entry[1]));
          legend.append(item);
        });
        section.append(legend);

        const figure = element("figure", "northstar-widget__figure");
        const svg = svgElement("svg", {viewBox: "0 0 " + width + " " + height, role: "img", "aria-labelledby": this.Name + "-trend-title"});
        const title = svgElement("title", {id: this.Name + "-trend-title"});
        title.textContent = this._heading + ". Opened and resolved cases by period.";
        svg.append(title);
        for (let tick = 0; tick <= 4; tick += 1) {
          const tickY = top + tick * plotHeight / 4;
          svg.append(svgElement("line", {x1: left, y1: tickY, x2: width - right, y2: tickY, class: "northstar-widget__grid"}));
        }
        const openedCoordinates = coordinates("opened");
        const areaPath = "M " + openedCoordinates[0][0] + " " + (top + plotHeight) +
          " L " + openedCoordinates[0][0] + " " + openedCoordinates[0][1] +
          smoothPath("opened").replace(/^M [^C]+/, "") +
          " L " + openedCoordinates[openedCoordinates.length - 1][0] + " " + (top + plotHeight) + " Z";
        svg.append(svgElement("path", {d: areaPath, class: "northstar-widget__area"}));
        svg.append(svgElement("path", {d: smoothPath("opened"), class: "northstar-widget__line northstar-widget__line--opened"}));
        svg.append(svgElement("path", {d: smoothPath("resolved"), class: "northstar-widget__line northstar-widget__line--resolved"}));
        data.forEach(function (item, index) {
          svg.append(svgElement("circle", {cx: x(index), cy: y(item.opened), r: 3.5, class: "northstar-widget__point northstar-widget__point--opened"}));
          svg.append(svgElement("circle", {cx: x(index), cy: y(item.resolved), r: 3.5, class: "northstar-widget__point northstar-widget__point--resolved"}));
          const label = svgElement("text", {x: x(index), y: height - 8, "text-anchor": "middle", class: "northstar-widget__axis"});
          label.textContent = item.label;
          svg.append(label);
        });
        figure.append(svg);
        const caption = element("figcaption", "northstar-widget__sr-only");
        caption.textContent = data.map(function (item) {
          return item.label + ": " + item.opened + " opened, " + item.resolved + " resolved";
        }).join(". ");
        figure.append(caption);
        section.append(figure);
        if (this._showDataTable) {
          const table = element("table", "northstar-widget__data-table");
          const caption = element("caption", "", this._heading + " data");
          const head = element("thead");
          const headRow = element("tr");
          ["Period", "Opened", "Resolved"].forEach(function (label) { headRow.append(element("th", "", label)); });
          head.append(headRow);
          const body = element("tbody");
          data.forEach(function (item) {
            const row = element("tr");
            [item.label, item.opened, item.resolved].forEach(function (value) { row.append(element("td", "", value)); });
            body.append(row);
          });
          table.append(caption, head, body);
          section.append(table);
        }
      }

      _drawAttention(section) {
        const list = element("ul", "northstar-widget__attention");
        this._data.slice(0, 5).forEach(function (item) {
          const row = element("li", "northstar-widget__attention-row");
          const target = safeTarget(pick(item, ["TargetUrl", "targetUrl", "Route", "route"]));
          const content = target ? element("button", "northstar-widget__row-button") : element("div", "northstar-widget__row-button");
          if (target) {
            content.type = "button";
            content.disabled = !this._isEnabled || this._isReadOnly;
            content.tabIndex = Number(this._tabIndex) || 0;
            content.addEventListener("click", this._navigate.bind(this, target));
          }
          const tone = text(pick(item, ["Tone", "tone", "SLAStatus", "slaStatus"])).toLowerCase().replace(/[^a-z0-9]+/g, "-");
          const rail = element("span", "northstar-widget__risk-rail northstar-widget__risk-rail--" + (tone || "neutral"));
          rail.setAttribute("aria-hidden", "true");
          const copy = element("span", "northstar-widget__row-copy");
          copy.append(
            element("strong", "northstar-widget__eyebrow", text(pick(item, ["CaseNumber", "caseNumber", "Reference", "reference"]))),
            element("span", "northstar-widget__row-title", text(pick(item, ["Title", "title"])) || "Untitled case")
          );
          const badge = element("span", "northstar-widget__badge northstar-widget__badge--" + (tone || "neutral"), text(pick(item, ["DueLabel", "dueLabel", "SLAStatus", "slaStatus"])) || "Review");
          content.append(rail, copy, badge);
          row.append(content);
          list.append(row);
        }, this);
        section.append(list);
      }

      _drawStage(section) {
        const list = element("ol", "northstar-widget__stages");
        const rows = this._data.map(function (item) {
          return {
            label: text(pick(item, ["StageLabel", "stageLabel", "Label", "label"])) || "Unassigned",
            value: number(pick(item, ["CaseCount", "caseCount", "Value", "value"])),
            sortOrder: number(pick(item, ["SortOrder", "sortOrder"]))
          };
        }).sort(function (left, right) { return left.sortOrder - right.sortOrder; });
        const max = Math.max(1, ...rows.map(function (item) { return item.value; }));
        rows.slice(0, 8).forEach(function (item) {
          const row = element("li", "northstar-widget__stage");
          const meta = element("div", "northstar-widget__stage-meta");
          meta.append(element("span", "", item.label), element("strong", "", formatCount(item.value)));
          const track = element("div", "northstar-widget__stage-track");
          track.setAttribute("role", "img");
          track.setAttribute("aria-label", item.label + ": " + item.value + " open cases");
          const bar = element("span", "northstar-widget__stage-bar");
          bar.style.width = Math.max(item.value ? 4 : 0, item.value / max * 100) + "%";
          track.append(bar);
          row.append(meta, track);
          list.append(row);
        });
        section.append(list);
      }

      _drawSupplier(section) {
        const grid = element("div", "northstar-widget__suppliers");
        this._data.slice(0, 4).forEach(function (item) {
          const target = safeTarget(pick(item, ["TargetUrl", "targetUrl", "Route", "route"]));
          const card = target ? element("button", "northstar-widget__supplier") : element("article", "northstar-widget__supplier");
          if (target) {
            card.type = "button";
            card.disabled = !this._isEnabled || this._isReadOnly;
            card.tabIndex = Number(this._tabIndex) || 0;
            card.addEventListener("click", this._navigate.bind(this, target));
          }
          const avatar = element("span", "northstar-widget__avatar", text(pick(item, ["SupplierInitials", "supplierInitials", "Initials", "initials"])) || "SU");
          const copy = element("span", "northstar-widget__supplier-copy");
          copy.append(
            element("strong", "", text(pick(item, ["SupplierName", "supplierName", "Name", "name"])) || "Supplier"),
            element("span", "", text(pick(item, ["SignalLabel", "signalLabel", "Status", "status"])) || "No signal")
          );
          const tone = text(pick(item, ["Tone", "tone"])).toLowerCase().replace(/[^a-z0-9]+/g, "-") || "neutral";
          const score = element("span", "northstar-widget__score northstar-widget__score--" + tone, "Score " + formatCount(pick(item, ["Score", "score", "Value", "value"])));
          score.setAttribute("aria-label", "Signal score " + score.textContent);
          card.append(avatar, copy, score);
          grid.append(card);
        }, this);
        section.append(grid);
      }

      _navigate(target) {
        this.Value = target;
        this.dispatchEvent(new Event("Navigate"));
      }

      _actionButton() {
        const button = element("button", "northstar-widget__action", this._showDataTable && this._variant === "trend"
          ? "Hide data table"
          : this._actionLabel);
        button.type = "button";
        button.disabled = !this._isEnabled || this._isReadOnly;
        button.addEventListener("click", function () {
          const target = safeTarget(this._actionTarget);
          if (target) {
            this._navigate(target);
            return;
          }
          if (this._variant === "trend") {
            this._showDataTable = !this._showDataTable;
            this.render();
          }
        }.bind(this));
        return button;
      }
    });
  }
}());
