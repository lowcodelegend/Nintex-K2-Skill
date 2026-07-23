(function () {
  "use strict";

  /*! k2style: designer-guard */
  if (document.documentElement.classList.contains("designer") ||
      /\/Designer(?:\/|$)/i.test(window.location.pathname || "")) {
    return;
  }

  window.K2SP_NATIVE_TABS_CONFIG = {
    version: "2026.07.23.1",
    applicationCssUrl: window.location.origin + "/SPTAssets/spt-application.v1.css",
    brandMark: "S",
    brandLabel: "Studio",
    brandSubtitle: "Native workspace",
    bootTimeoutMilliseconds: 2500,
    collapsedStorageKey: "spt:native-tabs:collapsed",
    allowExistingSidebar: false,
    iconByLabel: {
      "overview": "\u25c9",
      "work queue": "\u25a4",
      "details": "\u2637"
    }
  };
}());
