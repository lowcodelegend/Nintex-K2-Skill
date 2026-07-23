(function () {
  "use strict";

  /*! k2style: designer-guard */
  var root = document.documentElement;
  if (!/^\/Runtime\/(?:Runtime\/)?Form\//i.test(window.location.pathname || "") ||
      root.classList.contains("designer") ||
      root.getAttribute("data-designer") === "true") {
    return;
  }

  if (window.__k2spSidebarBoot) {
    return;
  }

  var config = window.K2SP_SIDEBAR_CONFIG || {
    version: "1",
    applicationCssUrl: window.location.origin + "/APPAssets/sidebar-application.min.css?v=1",
    navigationViewTitle: "Application navigation",
    formNamePrefix: "APP.",
    brandMark: "A",
    brandLabel: "Application",
    bootTimeoutMilliseconds: 2500,
    navigationTimeoutMilliseconds: 1800,
    cacheVersionKey: "k2sp:navigation:version",
    cachePrefix: "k2sp:navigation:v:",
    fallbackNavigation: [
      {
        NavigationCode: "HOME",
        SectionLabel: "Workspace",
        Label: "Home",
        IconToken: "home",
        TargetFormName: "APP.Home",
        SortOrder: 10,
        IsActive: true,
        ConfigurationVersion: "1"
      }
    ]
  };

  var state = window.__k2spSidebarBoot = {
    version: String(config.version || "1"),
    stylesReady: false,
    stylesLoaded: false,
    navigationSource: "fallback",
    dirty: false
  };

  root.classList.add("k2sp-runtime");
  mark("k2sp:boot-start");
  loadApplicationStyles();

  function mark(name) {
    try {
      if (window.performance && performance.mark) performance.mark(name);
    } catch (_) {}
  }

  function loadApplicationStyles() {
    var completed = false;
    var link = document.querySelector("link[data-k2sp-application-styles]");
    var created = false;

    function complete(loaded) {
      if (completed) return;
      completed = true;
      state.stylesReady = true;
      state.stylesLoaded = loaded;
      root.setAttribute("data-k2sp-application-styles", loaded ? "loaded" : "fallback");
      mark("k2sp:styles-ready");
    }

    if (link && link.sheet) {
      complete(true);
      return;
    }
    if (!link) {
      link = document.createElement("link");
      link.rel = "stylesheet";
      link.href = config.applicationCssUrl;
      link.setAttribute("data-k2sp-application-styles", state.version);
      created = true;
    }
    link.addEventListener("load", function () { complete(true); }, { once: true });
    link.addEventListener("error", function () { complete(false); }, { once: true });
    if (created) (document.head || root).appendChild(link);
    window.setTimeout(function () { complete(false); }, config.bootTimeoutMilliseconds);
  }

  function currentFormName() {
    var match = window.location.pathname.match(/\/Form\/([^/?#]+)/i);
    if (!match) return "";
    try {
      return decodeURIComponent(match[1].replace(/\+/g, " ")).replace(/\/$/, "");
    } catch (_) {
      return match[1].replace(/%20/gi, " ");
    }
  }

  function runtimeUrl(formName) {
    return window.location.origin + "/Runtime/Runtime/Form/" + encodeURIComponent(formName) + "/";
  }

  function create(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (typeof text === "string") node.textContent = text;
    return node;
  }

  function readNavigationCache() {
    try {
      var version = sessionStorage.getItem(config.cacheVersionKey);
      var raw = version && sessionStorage.getItem(config.cachePrefix + version);
      var parsed = raw && JSON.parse(raw);
      return Array.isArray(parsed) && parsed.length ? parsed : null;
    } catch (_) {
      return null;
    }
  }

  function writeNavigationCache(items) {
    if (!items || !items.length) return;
    var version = String(items[0].ConfigurationVersion || state.version);
    try {
      sessionStorage.setItem(config.cacheVersionKey, version);
      sessionStorage.setItem(config.cachePrefix + version, JSON.stringify(items));
    } catch (_) {}
  }

  function iconText(token) {
    var icons = {
      home: "\u2302",
      work: "\u2713",
      cases: "\u25a4",
      actions: "\u2197",
      reports: "\u25eb",
      settings: "\u2699"
    };
    return icons[String(token || "").toLowerCase()] || "\u2022";
  }

  function buildShell(formName, items) {
    if (document.getElementById("k2sp-shell")) return;
    var shell = create("div", "k2sp-shell");
    shell.id = "k2sp-shell";
    shell.setAttribute("data-k2sp-form", formName);

    var sidebar = create("aside", "k2sp-sidebar");
    sidebar.setAttribute("aria-label", "Application navigation");
    var brand = create("div", "k2sp-brand");
    brand.appendChild(create("span", "k2sp-brand-mark", config.brandMark || "A"));
    brand.appendChild(create("span", "k2sp-brand-label", config.brandLabel || "Application"));
    var navigation = create("nav", "k2sp-nav");
    navigation.setAttribute("aria-label", "Primary");
    sidebar.appendChild(brand);
    sidebar.appendChild(navigation);

    var transition = create("div", "k2sp-transition");
    transition.setAttribute("aria-live", "polite");
    transition.setAttribute("aria-hidden", "true");
    transition.appendChild(create("span", "k2sp-transition-mark", config.brandMark || "A"));
    transition.appendChild(create("span", "k2sp-transition-text", "Opening workspace\u2026"));

    shell.appendChild(sidebar);
    shell.appendChild(transition);
    document.body.insertBefore(shell, document.body.firstChild);
    renderNavigation(items, formName);
    mark("k2sp:shell-ready");
  }

  function renderNavigation(items, formName) {
    var host = document.querySelector("#k2sp-shell .k2sp-nav");
    if (!host) return;
    host.textContent = "";
    var section = null;
    items.filter(function (item) {
      return item && item.IsActive !== false && item.TargetFormName;
    }).sort(function (left, right) {
      return Number(left.SortOrder || 0) - Number(right.SortOrder || 0);
    }).forEach(function (item) {
      var nextSection = item.SectionLabel || "";
      if (nextSection && nextSection !== section) {
        host.appendChild(create("div", "k2sp-nav-label", nextSection));
        section = nextSection;
      }
      var link = create("a", "k2sp-nav-item");
      link.href = runtimeUrl(item.TargetFormName);
      link.setAttribute("data-k2sp-route", item.TargetFormName);
      link.setAttribute("data-k2sp-code", item.NavigationCode || "");
      var active = item.TargetFormName === formName;
      if (active) {
        link.classList.add("active");
        link.setAttribute("aria-current", "page");
      }
      var icon = create("span", "k2sp-nav-icon", iconText(item.IconToken));
      icon.setAttribute("aria-hidden", "true");
      link.appendChild(icon);
      link.appendChild(create("span", "k2sp-nav-text", item.Label || item.TargetFormName));
      host.appendChild(link);
    });
  }

  function titleOf(view) {
    var nodes = view.querySelectorAll("[data-sf-title], .panel-header-text, .panel-header-text span, .header .title");
    for (var index = 0; index < nodes.length; index += 1) {
      var value = (nodes[index].getAttribute("data-sf-title") || nodes[index].textContent || "")
        .replace(/\s+/g, " ").trim();
      if (value) return value;
    }
    return "";
  }

  function findNavigationView() {
    var views = document.querySelectorAll(".runtime-form .view, .form .view");
    var expected = String(config.navigationViewTitle).toLowerCase();
    for (var index = 0; index < views.length; index += 1) {
      if (titleOf(views[index]).toLowerCase() === expected) return views[index];
    }
    return null;
  }

  function cellValue(cell) {
    if (!cell) return "";
    var input = cell.querySelector('input:not([type="checkbox"]), textarea, select');
    if (input && typeof input.value === "string") return input.value.trim();
    var checkbox = cell.querySelector('input[type="checkbox"]');
    if (checkbox) return checkbox.checked ? "true" : "false";
    return (cell.textContent || "").replace(/\s+/g, " ").trim();
  }

  function extractNavigation(view) {
    if (!view) return [];
    var rows = view.querySelectorAll("table.grid-content-table tr, .grid-content-table tr");
    var items = [];
    for (var index = 0; index < rows.length; index += 1) {
      var cells = rows[index].querySelectorAll("td");
      if (cells.length < 6) continue;
      var code = cellValue(cells[0]);
      var target = cellValue(cells[4]);
      if (!code || !target || (config.formNamePrefix && target.indexOf(config.formNamePrefix) !== 0)) continue;
      items.push({
        NavigationCode: code,
        SectionLabel: cellValue(cells[1]),
        Label: cellValue(cells[2]),
        IconToken: cellValue(cells[3]),
        TargetFormName: target,
        SortOrder: Number(cellValue(cells[5]) || 0),
        IsActive: cells.length < 7 || !/^(false|0|no|inactive)$/i.test(cellValue(cells[6])),
        ConfigurationVersion: cells.length > 7 ? cellValue(cells[7]) || state.version : state.version
      });
    }
    return items;
  }

  function suppressNavigationSource() {
    var view = findNavigationView();
    if (!view) return null;
    var row = view.closest(".row") || view;
    row.classList.add("k2sp-native-navigation-source");
    row.setAttribute("aria-hidden", "true");
    return view;
  }

  function reconcileNavigation(formName) {
    var reconciled = false;
    var observer;
    var timeout;
    var cleanup;

    function attempt() {
      var view = suppressNavigationSource();
      var items = extractNavigation(view);
      if (reconciled || !items.length) return false;
      reconciled = true;
      state.navigationSource = "smartobject";
      document.body.setAttribute("data-k2sp-navigation-source", state.navigationSource);
      writeNavigationCache(items);
      renderNavigation(items, formName);
      if (observer) observer.disconnect();
      if (timeout) clearTimeout(timeout);
      if (cleanup) clearTimeout(cleanup);
      mark("k2sp:navigation-reconciled");
      return true;
    }

    if (attempt()) return;
    observer = new MutationObserver(function () {
      suppressNavigationSource();
      window.clearTimeout(state.reconcileDebounce);
      state.reconcileDebounce = window.setTimeout(attempt, 40);
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
    timeout = window.setTimeout(function () {
      if (!reconciled) mark("k2sp:navigation-timeout");
    }, config.navigationTimeoutMilliseconds);
    cleanup = window.setTimeout(function () {
      suppressNavigationSource();
      if (observer) observer.disconnect();
    }, 15000);
  }

  function bindNavigation(formName) {
    document.addEventListener("click", function (event) {
      var link = event.target.closest && event.target.closest("[data-k2sp-route]");
      if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      var target = link.getAttribute("data-k2sp-route");
      if (!target || target === formName) {
        event.preventDefault();
        return;
      }
      if (state.dirty && !window.confirm("You have unsaved changes. Leave this page?")) {
        event.preventDefault();
        return;
      }
      event.preventDefault();
      state.dirty = false;
      var transition = document.querySelector(".k2sp-transition");
      var text = transition && transition.querySelector(".k2sp-transition-text");
      if (text) text.textContent = "Opening " + (link.textContent || target).replace(/\s+/g, " ").trim() + "\u2026";
      if (transition) transition.setAttribute("aria-hidden", "false");
      document.body.classList.add("k2sp-leaving");
      window.setTimeout(function () { window.location.assign(link.href); }, 80);
    });
  }

  function bindDirtyTracking() {
    function markDirty(event) {
      if (!event.isTrusted || !event.target || !event.target.closest) return;
      if (event.target.closest("#k2sp-shell") || event.target.closest(".k2sp-native-navigation-source")) return;
      if (event.target.matches('input, textarea, select, [contenteditable="true"]')) state.dirty = true;
    }
    document.addEventListener("input", markDirty, true);
    document.addEventListener("change", markDirty, true);
    window.addEventListener("beforeunload", function (event) {
      if (!state.dirty) return;
      event.preventDefault();
      event.returnValue = "";
    });
  }

  function revealWhenReady() {
    var revealed = false;
    var started = Date.now();

    function reveal() {
      if (revealed) return;
      revealed = true;
      requestAnimationFrame(function () {
        requestAnimationFrame(function () {
          document.body.classList.add("k2sp-ready");
          mark("k2sp:content-ready");
        });
      });
    }

    (function poll() {
      suppressNavigationSource();
      var nativeReady = !!document.querySelector(".runtime-form .form, .runtime-form .view, .form .view");
      if ((nativeReady && state.stylesReady) || Date.now() - started >= config.bootTimeoutMilliseconds) {
        reveal();
        return;
      }
      window.setTimeout(poll, 50);
    }());
    window.setTimeout(reveal, config.bootTimeoutMilliseconds);
  }

  function activate() {
    if (!document.body || document.getElementById("k2sp-shell")) return;
    var formName = currentFormName();
    if (config.formNamePrefix && formName.indexOf(config.formNamePrefix) !== 0) return;
    var cached = readNavigationCache();
    var initial = cached || config.fallbackNavigation || [];
    state.navigationSource = cached ? "cache" : "fallback";
    document.body.classList.add("k2sp-enhanced");
    document.body.setAttribute("data-k2sp-version", state.version);
    document.body.setAttribute("data-k2sp-navigation-source", state.navigationSource);
    suppressNavigationSource();
    buildShell(formName, initial);
    bindNavigation(formName);
    bindDirtyTracking();
    reconcileNavigation(formName);
    revealWhenReady();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", activate, { once: true });
  } else {
    activate();
  }
}());
