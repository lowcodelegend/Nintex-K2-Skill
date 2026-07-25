(function () {
  "use strict";

  /*! k2style: designer-guard */
  var root = document.documentElement;
  if (!/^\/Runtime\/(?:Runtime\/)?Form\//i.test(window.location.pathname || "") ||
      root.classList.contains("designer") ||
      root.getAttribute("data-designer") === "true") {
    return;
  }

  window.K2SP_SIDEBAR_CONFIG = {
    version: "1",
    applicationCssUrl: window.location.origin + "/NorthstarAssets/northstar-homepage.css?v=1",
    navigationViewTitle: "Application navigation",
    formNamePrefix: "",
    brandMark: "N",
    brandLabel: "Northstar",
    bootTimeoutMilliseconds: 2500,
    navigationTimeoutMilliseconds: 1800,
    cacheVersionKey: "northstar:navigation:version",
    cachePrefix: "northstar:navigation:v:",
    fallbackNavigation: []
  };
}());
