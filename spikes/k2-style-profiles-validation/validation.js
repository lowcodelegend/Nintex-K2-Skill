(function () {
  "use strict";

  /* k2style: designer-guard */
  var root = document.documentElement;
  if (/\/Designer(?:\/|$)/i.test(window.location.pathname || "") ||
      root.classList.contains("designer") ||
      root.getAttribute("data-designer") === "true") {
    return;
  }

  if (document.body) {
    document.body.classList.add("k2sp-validation");
  } else {
    document.addEventListener("DOMContentLoaded", function () {
      document.body.classList.add("k2sp-validation");
    }, { once: true });
  }
}());
