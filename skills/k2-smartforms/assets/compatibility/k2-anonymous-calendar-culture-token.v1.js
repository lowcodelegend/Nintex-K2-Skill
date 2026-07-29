(function () {
    "use strict";

    /* k2style: designer-guard */
    var root = document.documentElement;
    var path = window.location.pathname || "";
    if (/\/Designer(?:\/|$)/i.test(path) ||
        (root && (root.classList.contains("designer") ||
            root.getAttribute("data-designer") === "true"))) {
        return;
    }

    if (window.__runtimeIsAnonymous !== true ||
        typeof window.__runtimeAnonTokenName !== "string" ||
        !window.__runtimeAnonTokenName ||
        typeof window.__runtimeAnonToken !== "string" ||
        !window.__runtimeAnonToken ||
        window.__k2AnonymousCalendarCultureTokenV1 === true) {
        return;
    }

    var targetMethod = "getCulturesListAndCurrentCultureDetailsAndTimezones";
    var originalOpen = XMLHttpRequest.prototype.open;
    var originalSend = XMLHttpRequest.prototype.send;

    XMLHttpRequest.prototype.open = function () {
        this.__k2AnonymousCalendarCultureRequestV1 = false;
        try {
            var candidate = new URL(String(arguments[1]), window.location.href);
            this.__k2AnonymousCalendarCultureRequestV1 =
                candidate.origin === window.location.origin &&
                /\/AJAXCall\.ashx$/i.test(candidate.pathname) &&
                candidate.searchParams.get("method") === targetMethod;
        } catch (_) {
            // Fail open: leave unrelated or malformed requests untouched.
        }
        return originalOpen.apply(this, arguments);
    };

    XMLHttpRequest.prototype.send = function () {
        if (this.__k2AnonymousCalendarCultureRequestV1 === true &&
            window.__runtimeIsAnonymous === true &&
            typeof window.__runtimeAnonTokenName === "string" &&
            window.__runtimeAnonTokenName &&
            typeof window.__runtimeAnonToken === "string" &&
            window.__runtimeAnonToken) {
            try {
                this.setRequestHeader(
                    window.__runtimeAnonTokenName,
                    window.__runtimeAnonToken
                );
            } catch (_) {
                // Fail open: K2 will handle the unmodified request.
            }
        }
        return originalSend.apply(this, arguments);
    };

    window.__k2AnonymousCalendarCultureTokenV1 = true;
}());
