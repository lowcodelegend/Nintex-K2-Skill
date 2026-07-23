(function () {
  "use strict";

  /* k2style: designer-guard */
  function isDesigner() {
    var root = document.documentElement;
    var path = window.location.pathname || "";
    return /\/Designer(?:\/|$)/i.test(path) ||
      root.classList.contains("designer") ||
      root.getAttribute("data-designer") === "true";
  }

  if (isDesigner()) {
    return;
  }

  document.documentElement.classList.add("k2sp-runtime");
}());
