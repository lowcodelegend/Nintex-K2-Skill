(function () {
  "use strict";

  /*! k2style: designer-guard */
  var root = document.documentElement;
  if (!/^\/Runtime\/(?:Runtime\/)?Form\//i.test(window.location.pathname || "") ||
      root.classList.contains("designer") ||
      root.getAttribute("data-designer") === "true") {
    return;
  }

  window.K2SP_NORTHSTAR_CONFIG = {
    version: "15",
    applicationCssUrl: window.location.origin + "/NorthstarAssets/northstar-homepage.v15.css",
    navigationViewTitle: "Application navigation",
    commandPaletteViewTitle: "Command palette",
    formNamePrefix: "",
    brandMark: "N",
    brandLabel: "Northstar",
    brandSubtitle: "Quality operations",
    userInitials: "AM",
    userName: "Alex Morgan",
    userRole: "Quality manager",
    newCaseNavigationCode: "NEW_CASE",
    insightNavigationCode: "CASES",
    navigationCounts: {
      MY_WORK: 8,
      ACTIONS: 5
    },
    kpiViewTitle: "Operational position",
    trendViewTitle: "Nonconformance trend",
    trendDataViewTitle: "Nonconformance trend data",
    attentionViewTitle: "Attention now",
    attentionDataViewTitle: "Attention now data",
    stagesViewTitle: "Where work is accumulating",
    stagesDataViewTitle: "Where work is accumulating data",
    supplierSignalViewTitle: "Supplier signal",
    supplierSignalDataViewTitle: "Supplier signal data",
    suppressedFrameworkViews: [],
    suppressedFrameworkPanelNames: ["Header", "Footer"],
    enableDashboardComposition: true,
    kpiDecorations: {
      OpenCaseCount: { text: "↓ 8.6% vs last month", tone: "positive" },
      SLAAtRiskCount: { text: "↑ 3 need intervention", tone: "critical" },
      OverdueActionCount: { text: "2 supplier-owned", tone: "critical" },
      FirstPassYieldPercent: { text: "↑ 1.7 pts", tone: "positive" }
    },
    pages: {
      "NTH.Quality Operations": {
        key: "command",
        eyebrow: "Wednesday, 22 July",
        title: "Good morning, Alex",
        subtitle: "Here is what changed, what needs attention, and where quality is trending.",
        insightTitle: "Three related defects may share a machining cause",
        insightBody: "Cases from Apex Precision Metals reference the same cell, alloy batch, and surface condition. Estimated exposure: 1,840 units.",
        insightAction: "Review cluster",
        insightNavigationCode: "CASES"
      }
    },
    bootTimeoutMilliseconds: 2500,
    navigationTimeoutMilliseconds: 1800,
    cacheVersionKey: "northstar:navigation:version",
    cachePrefix: "northstar:navigation:v:",
    fallbackNavigation: []
  };
}());
