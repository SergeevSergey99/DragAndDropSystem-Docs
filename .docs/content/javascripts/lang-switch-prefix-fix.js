(function () {
  // mkdocs-static-i18n emits the language-selector and hreflang links as
  // root-absolute paths ("/ru/page/") that omit the deployment base. On a
  // GitHub *project* site the base is "/<repo>"; once the docs are versioned
  // with mike it also carries a version segment, e.g. "/<repo>/<version>".
  // This script recovers that base from the live URL and prepends it so the
  // language switch keeps you inside the same repo AND the same version.
  function normalizePath(path) {
    return path.replace(/\/+/g, "/").replace(/\/$/, "") || "/";
  }

  function joinPrefix(prefix, href) {
    if (!href.startsWith("/")) {
      return href;
    }

    if (!prefix || prefix === "/") {
      return href;
    }

    return normalizePath(prefix) + href;
  }

  // Derive the base that i18n's links are missing (repo + optional mike
  // version). We take the current language's own selector link — which i18n
  // emits root-absolute, without the base — and strip it from the real
  // pathname. Whatever remains in front is exactly "/<repo>/<version>"
  // (or just "/<repo>", or "" when served from the domain root).
  function detectBasePrefix() {
    var pathname = window.location.pathname || "/";
    var currentLang = (document.documentElement.lang || "").toLowerCase();
    var selector = '.md-select__link[href^="/"]';
    var links = Array.prototype.slice.call(document.querySelectorAll(selector));

    if (!links.length || !currentLang) {
      return "";
    }

    var currentLink = links.find(function (link) {
      return (link.getAttribute("hreflang") || "").toLowerCase() === currentLang;
    });

    if (!currentLink) {
      return "";
    }

    var currentHref = currentLink.getAttribute("href");
    // Default language sits at the version root ("/" link): the whole pathname
    // up to here is the base (repo + version).
    if (!currentHref || currentHref === "/") {
      return normalizePath(pathname);
    }

    // Non-default language: the link is the page path under "/<lang>/...".
    // It must be a suffix of the live pathname; the leading remainder is the
    // base. This naturally keeps the mike "/<version>/" segment in the base.
    if (!pathname.endsWith(currentHref)) {
      return "";
    }

    var prefix = pathname.slice(0, pathname.length - currentHref.length);
    return normalizePath(prefix);
  }

  function rewriteLinks() {
    var prefix = detectBasePrefix();
    if (!prefix || prefix === "/") {
      return;
    }

    // Only the language selector and hreflang links need patching. The mike
    // version dropdown (.md-version__*) is intentionally left untouched —
    // mike resolves those relative to the current page and already accounts
    // for the base, so prefixing them would double the path.
    var selectors = [
      '.md-select__link[href^="/"]',
      'link[rel="alternate"][href^="/"]'
    ];

    selectors.forEach(function (selector) {
      var nodes = document.querySelectorAll(selector);
      nodes.forEach(function (node) {
        // Idempotent: never prefix the same node twice (guards against the
        // script running again after instant navigation / re-injection).
        if (node.getAttribute("data-base-prefix-applied") === "1") {
          return;
        }

        var href = node.getAttribute("href");
        if (!href) {
          return;
        }

        node.setAttribute("href", joinPrefix(prefix, href));
        node.setAttribute("data-base-prefix-applied", "1");
      });
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", rewriteLinks);
  } else {
    rewriteLinks();
  }
})();
