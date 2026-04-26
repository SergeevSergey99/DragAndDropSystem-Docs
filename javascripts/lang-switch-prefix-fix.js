(function () {
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

  function detectRepoPrefix() {
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
    if (!currentHref || currentHref === "/") {
      return normalizePath(pathname);
    }

    if (!pathname.endsWith(currentHref)) {
      return "";
    }

    var prefix = pathname.slice(0, pathname.length - currentHref.length);
    return normalizePath(prefix);
  }

  function rewriteLinks() {
    var prefix = detectRepoPrefix();
    if (!prefix || prefix === "/") {
      return;
    }

    var selectors = [
      '.md-select__link[href^="/"]',
      'link[rel="alternate"][href^="/"]'
    ];

    selectors.forEach(function (selector) {
      var nodes = document.querySelectorAll(selector);
      nodes.forEach(function (node) {
        var href = node.getAttribute("href");
        if (!href) {
          return;
        }

        node.setAttribute("href", joinPrefix(prefix, href));
      });
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", rewriteLinks);
  } else {
    rewriteLinks();
  }
})();
