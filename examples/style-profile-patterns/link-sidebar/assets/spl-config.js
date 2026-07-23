(function () {
  "use strict";

  /*! k2style: designer-guard */
  if (document.documentElement.classList.contains("designer") ||
      /\/Designer(?:\/|$)/i.test(window.location.pathname || "")) {
    return;
  }

  window.K2SP_SIDEBAR_CONFIG = {
    version: "2026.07.23.1",
    applicationCssUrl: window.location.origin + "/SPLAssets/spl-application.v1.css",
    navigationViewTitle: "Application navigation",
    formNamePrefix: "SPL.Link",
    brandMark: "S",
    brandLabel: "Studio",
    bootTimeoutMilliseconds: 2500,
    navigationTimeoutMilliseconds: 1800,
    cacheVersionKey: "spl:navigation:version",
    cachePrefix: "spl:navigation:v:",
    fallbackNavigation: [
      {
        NavigationCode: "DASHBOARD",
        SectionLabel: "Workspace",
        Label: "Overview",
        IconToken: "home",
        TargetFormName: "SPL.Link Dashboard",
        SortOrder: 10,
        IsActive: true,
        ConfigurationVersion: "1"
      },
      {
        NavigationCode: "WORK",
        SectionLabel: "Workspace",
        Label: "Work items",
        IconToken: "work",
        TargetFormName: "SPL.Link Work",
        SortOrder: 20,
        IsActive: true,
        ConfigurationVersion: "1"
      }
    ]
  };
}());
