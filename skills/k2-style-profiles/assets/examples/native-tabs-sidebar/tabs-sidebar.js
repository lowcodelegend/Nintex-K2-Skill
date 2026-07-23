(function () {
  "use strict";

  var config = window.K2SP_NATIVE_TABS_CONFIG || {};
  var root = document.documentElement;
  var runtimeRoute = /^\/Runtime\/(?:Runtime\/)?Form\//i.test(window.location.pathname || "");

  /*! k2style: designer-guard */
  if ((!runtimeRoute && !config.allowFixture) ||
      root.classList.contains("designer") ||
      root.getAttribute("data-designer") === "true" ||
      /\/Designer(?:\/|$)/i.test(window.location.pathname || "")) {
    return;
  }
  if (window.__k2spNativeTabs) return;

  var defaults = {
    version: "1",
    applicationCssUrl: window.location.origin + "/APPAssets/tabs-sidebar-application.min.css?v=1",
    brandMark: "A",
    brandLabel: "Application",
    brandSubtitle: "Workspace",
    bootTimeoutMilliseconds: 2500,
    collapsedStorageKey: "k2sp:native-tabs:collapsed",
    allowExistingSidebar: false,
    iconByLabel: {
      "home": "\u2302",
      "cases": "\u25a4",
      "overview": "\u25c9",
      "details": "\u2637",
      "investigation": "\u2315",
      "collaboration": "\u25ce",
      "decisions & actions": "\u2713",
      "activity & history": "\u25f7",
      "analytics": "\u25eb",
      "reports": "\u2261",
      "my tasks": "\u2611"
    }
  };
  Object.keys(defaults).forEach(function (key) {
    if (typeof config[key] === "undefined") config[key] = defaults[key];
  });

  var state = window.__k2spNativeTabs = {
    version: String(config.version || "1"),
    stylesReady: false,
    stylesLoaded: false,
    ready: false,
    failedOpen: false,
    tabCount: 0,
    selectedLabel: null,
    tabs: null,
    tabsObserver: null,
    runtimeObserver: null,
    originalParent: null,
    originalNextSibling: null
  };

  root.classList.add("k2sp-tabs-runtime");
  mark("k2sp-tabs:boot-start");
  loadApplicationStyles();

  function mark(name) {
    try {
      if (window.performance && performance.mark) performance.mark(name);
    } catch (_) {}
  }

  function loadApplicationStyles() {
    var completed = false;
    var link = document.querySelector("link[data-k2sp-tabs-application-styles]");
    var created = false;

    function complete(loaded) {
      if (completed) return;
      completed = true;
      state.stylesReady = true;
      state.stylesLoaded = loaded;
      root.setAttribute("data-k2sp-tabs-application-styles", loaded ? "loaded" : "failed");
      mark("k2sp-tabs:styles-ready");
    }

    if (link && link.sheet) {
      complete(true);
      return;
    }
    if (!link) {
      link = document.createElement("link");
      link.rel = "stylesheet";
      link.href = config.applicationCssUrl;
      link.setAttribute("data-k2sp-tabs-application-styles", state.version);
      created = true;
    }
    link.addEventListener("load", function () { complete(true); }, { once: true });
    link.addEventListener("error", function () { complete(false); }, { once: true });
    if (created) (document.head || root).appendChild(link);
    window.setTimeout(function () { complete(false); }, config.bootTimeoutMilliseconds);
  }

  function normalize(value) {
    return String(value || "").replace(/\s+/g, " ").trim();
  }

  function nativeAnchors(tabs) {
    return Array.prototype.slice.call(tabs.querySelectorAll(":scope > li > a.tab, :scope > li > a"));
  }

  function associatedTabBox(tabs) {
    var id = tabs.id || "";
    var expected = id.replace(/_tabPanel$/i, "_tabbox");
    var byId = expected !== id && document.getElementById(expected);
    if (byId && byId.classList.contains("form-tabs")) return byId;
    var runtimeForm = tabs.closest(".runtime-form");
    return runtimeForm && runtimeForm.querySelector(":scope > .tab-box.form-tabs");
  }

  function findNativeTabs() {
    var candidates = document.querySelectorAll(".runtime-form ul.tab-box-tabs");
    for (var index = 0; index < candidates.length; index += 1) {
      var tabs = candidates[index];
      if (tabs.closest("#k2sp-tab-sidebar")) continue;
      if (nativeAnchors(tabs).length > 1 && associatedTabBox(tabs)) return tabs;
    }
    return null;
  }

  function create(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (typeof text === "string") node.textContent = text;
    return node;
  }

  function buildSidebar() {
    var existing = document.getElementById("k2sp-tab-sidebar");
    if (existing) return existing;

    var sidebar = create("nav", "k2sp-tab-sidebar");
    sidebar.id = "k2sp-tab-sidebar";
    sidebar.setAttribute("aria-label", "Application sections");

    var brand = create("div", "k2sp-tab-brand");
    brand.appendChild(create("span", "k2sp-tab-brand-mark", config.brandMark));
    var copy = create("span", "k2sp-tab-brand-copy");
    copy.appendChild(create("strong", "", config.brandLabel));
    copy.appendChild(create("small", "", config.brandSubtitle));
    brand.appendChild(copy);

    var tabsHost = create("div", "k2sp-tab-list-host");
    tabsHost.id = "k2sp-tab-list-host";
    var footer = create("div", "k2sp-tab-sidebar-footer", "Powered by native K2 tabs");
    sidebar.appendChild(brand);
    sidebar.appendChild(tabsHost);
    sidebar.appendChild(footer);

    var toggle = create("button", "k2sp-tab-sidebar-toggle");
    toggle.type = "button";
    toggle.setAttribute("aria-controls", sidebar.id);
    toggle.setAttribute("aria-label", "Collapse application navigation");
    toggle.innerHTML = '<span aria-hidden="true">\u2630</span>';
    toggle.addEventListener("click", function () {
      setCollapsed(!document.body.classList.contains("k2sp-tabs-collapsed"), true);
    });

    document.body.insertBefore(sidebar, document.body.firstChild);
    document.body.insertBefore(toggle, sidebar.nextSibling);
    setCollapsed(readCollapsed(), false);
    return sidebar;
  }

  function readCollapsed() {
    try { return localStorage.getItem(config.collapsedStorageKey) === "true"; }
    catch (_) { return false; }
  }

  function setCollapsed(collapsed, persist) {
    document.body.classList.toggle("k2sp-tabs-collapsed", collapsed);
    var toggle = document.querySelector(".k2sp-tab-sidebar-toggle");
    if (toggle) {
      toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
      toggle.setAttribute("aria-label", collapsed ? "Expand application navigation" : "Collapse application navigation");
    }
    if (persist) {
      try { localStorage.setItem(config.collapsedStorageKey, String(collapsed)); } catch (_) {}
    }
  }

  function labelOf(anchor) {
    var text = anchor.querySelector(".tab-text");
    return normalize(text ? text.textContent : anchor.textContent);
  }

  function iconFor(label) {
    return config.iconByLabel[String(label || "").toLowerCase()] || "\u2022";
  }

  function syncTabs() {
    if (!state.tabs) return;
    var anchors = nativeAnchors(state.tabs);
    var selected = null;
    anchors.forEach(function (anchor) {
      var label = labelOf(anchor);
      var active = anchor.classList.contains("selected") ||
        (anchor.parentElement && anchor.parentElement.classList.contains("selected"));
      anchor.setAttribute("role", "tab");
      anchor.setAttribute("aria-selected", active ? "true" : "false");
      anchor.setAttribute("tabindex", active ? "0" : "-1");
      anchor.setAttribute("data-k2sp-tab-label", label);
      anchor.setAttribute("data-k2sp-tab-icon", iconFor(label));
      if (!anchor.getAttribute("aria-label")) anchor.setAttribute("aria-label", label);

      var pane = anchor.id && document.getElementById(anchor.id + "_form");
      if (pane) {
        pane.setAttribute("role", "tabpanel");
        pane.setAttribute("aria-labelledby", anchor.id);
      }
      if (active) selected = anchor;
    });

    if (!selected && anchors.length) {
      selected = anchors[0];
      selected.setAttribute("tabindex", "0");
    }
    state.tabCount = anchors.length;
    state.selectedLabel = selected ? labelOf(selected) : null;
    document.body.setAttribute("data-k2sp-selected-tab", state.selectedLabel || "");
    document.body.classList.remove("k2sp-tabs-switching");
    mark("k2sp-tabs:selection-synced");
  }

  function bindKeyboard(tabs) {
    tabs.addEventListener("keydown", function (event) {
      var anchor = event.target.closest && event.target.closest("a[role=tab]");
      if (!anchor) return;
      var anchors = nativeAnchors(tabs);
      var index = anchors.indexOf(anchor);
      var next = -1;
      if (event.key === "ArrowDown" || event.key === "ArrowRight") next = (index + 1) % anchors.length;
      if (event.key === "ArrowUp" || event.key === "ArrowLeft") next = (index - 1 + anchors.length) % anchors.length;
      if (event.key === "Home") next = 0;
      if (event.key === "End") next = anchors.length - 1;
      if (next >= 0) {
        event.preventDefault();
        anchors[next].focus();
      } else if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        anchor.click();
      }
    });
  }

  function bindSelectionFeedback(tabs) {
    tabs.addEventListener("click", function (event) {
      if (!event.target.closest || !event.target.closest("a.tab, a[role=tab]")) return;
      document.body.classList.add("k2sp-tabs-switching");
      window.setTimeout(function () {
        document.body.classList.remove("k2sp-tabs-switching");
        syncTabs();
      }, 1200);
    }, true);
  }

  function syncOrientation() {
    if (!state.tabs) return;
    state.tabs.setAttribute(
      "aria-orientation",
      window.matchMedia("(max-width: 800px)").matches ? "horizontal" : "vertical"
    );
  }

  function bindOrientation() {
    if (state.orientationBound) return;
    state.orientationBound = true;
    var media = window.matchMedia("(max-width: 800px)");
    if (media.addEventListener) media.addEventListener("change", syncOrientation);
    else if (media.addListener) media.addListener(syncOrientation);
  }

  function observeTabs(tabs) {
    if (state.tabsObserver) state.tabsObserver.disconnect();
    state.tabsObserver = new MutationObserver(function () {
      window.clearTimeout(state.syncTimer);
      state.syncTimer = window.setTimeout(syncTabs, 20);
    });
    state.tabsObserver.observe(tabs, {
      attributes: true,
      attributeFilter: ["class", "style", "aria-selected"],
      childList: true,
      subtree: true
    });
  }

  function attachTabs(tabs) {
    if (!tabs || tabs === state.tabs) return true;
    var sidebar = buildSidebar();
    var host = sidebar.querySelector("#k2sp-tab-list-host");
    if (!host) return false;

    if (state.tabs && state.tabs.parentElement === host) state.tabs.remove();
    state.originalParent = tabs.parentNode;
    state.originalNextSibling = tabs.nextSibling;
    state.tabs = tabs;
    tabs.setAttribute("role", "tablist");
    syncOrientation();
    tabs.removeAttribute("tabindex");
    host.appendChild(tabs);
    syncOrientation();
    bindOrientation();
    bindKeyboard(tabs);
    bindSelectionFeedback(tabs);
    observeTabs(tabs);
    syncTabs();
    document.body.classList.add("k2sp-tabs-enhanced");
    mark("k2sp-tabs:native-strip-relocated");
    return true;
  }

  function restoreNativeTabs() {
    if (state.tabs && state.originalParent) {
      if (state.originalNextSibling && state.originalNextSibling.parentNode === state.originalParent) {
        state.originalParent.insertBefore(state.tabs, state.originalNextSibling);
      } else {
        state.originalParent.appendChild(state.tabs);
      }
      state.tabs.removeAttribute("role");
      state.tabs.removeAttribute("aria-orientation");
      nativeAnchors(state.tabs).forEach(function (anchor) {
        anchor.removeAttribute("role");
        anchor.removeAttribute("aria-selected");
        anchor.setAttribute("tabindex", "-1");
        anchor.removeAttribute("data-k2sp-tab-label");
        anchor.removeAttribute("data-k2sp-tab-icon");
      });
    }
    if (state.tabsObserver) state.tabsObserver.disconnect();
    var sidebar = document.getElementById("k2sp-tab-sidebar");
    var toggle = document.querySelector(".k2sp-tab-sidebar-toggle");
    if (sidebar) sidebar.remove();
    if (toggle) toggle.remove();
    document.body.classList.remove("k2sp-tabs-enhanced", "k2sp-tabs-collapsed", "k2sp-tabs-switching");
  }

  function failOpen(reason) {
    if (state.ready || state.failedOpen) return;
    state.failedOpen = true;
    state.failureReason = reason;
    restoreNativeTabs();
    document.body.classList.add("k2sp-tabs-ready");
    root.setAttribute("data-k2sp-tabs-status", "native-fallback");
    mark("k2sp-tabs:failed-open");
  }

  function reveal() {
    if (state.ready || state.failedOpen) return;
    state.ready = true;
    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        document.body.classList.add("k2sp-tabs-ready");
        root.setAttribute("data-k2sp-tabs-status", "enhanced");
        mark("k2sp-tabs:content-ready");
      });
    });
  }

  function watchForReplacement(runtimeForm) {
    if (state.runtimeObserver) state.runtimeObserver.disconnect();
    state.runtimeObserver = new MutationObserver(function () {
      var replacement = findNativeTabs();
      if (replacement && replacement !== state.tabs) attachTabs(replacement);
    });
    state.runtimeObserver.observe(runtimeForm, { childList: true, subtree: true });
  }

  function activate() {
    if (!document.body) return;
    if (!config.allowExistingSidebar &&
        document.querySelector(".psf-sidebar, [data-application-sidebar]:not(#k2sp-tab-sidebar)")) {
      failOpen("conflicting-sidebar");
      return;
    }

    var started = Date.now();
    (function poll() {
      var tabs = findNativeTabs();
      if (state.stylesLoaded && tabs && attachTabs(tabs)) {
        var runtimeForm = associatedTabBox(state.tabs).closest(".runtime-form");
        if (runtimeForm) watchForReplacement(runtimeForm);
        reveal();
        return;
      }
      if ((state.stylesReady && !state.stylesLoaded) ||
          Date.now() - started >= config.bootTimeoutMilliseconds) {
        failOpen(state.stylesLoaded ? "native-tabs-not-found" : "application-css-unavailable");
        return;
      }
      window.setTimeout(poll, 40);
    }());
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", activate, { once: true });
  } else {
    activate();
  }
}());
