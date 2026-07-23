(function () {
  "use strict";

  /*! k2style: designer-guard */
  if (document.documentElement.classList.contains("designer") ||
      /\/Designer(?:\/|$)/i.test(window.location.pathname || "")) {
    return;
  }

  window.K2SP_NATIVE_TABS_CONFIG = {
    version: "1",
    applicationCssUrl: window.location.origin + "/APPTabAssets/tabs-sidebar-application.min.css?v=1",
    brandMark: "A",
    brandLabel: "Application",
    brandSubtitle: "Workspace",
    bootTimeoutMilliseconds: 2500,
    collapsedStorageKey: "k2sp:native-tabs:collapsed",
    allowExistingSidebar: false,
    iconByLabel: {
      "queue": "\u25a4",
      "details": "\u2637",
      "my tasks": "\u2611"
    }
  };
}());
