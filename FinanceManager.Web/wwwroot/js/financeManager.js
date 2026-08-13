window.financeManager = window.financeManager || {};

(function (fm) {
  const loadingBarId = "fm-loading-bar";
  let submitFallbackTimers = [];

  function getLoadingBar() {
    return document.querySelector("[data-mst-loading-bar]") || document.getElementById(loadingBarId);
  }

  function getLoadingBarColors(el) {
    const configuredColors = (el.dataset.loadingColors || "")
      .split(",")
      .map(function (color) { return color.trim(); })
      .filter(Boolean);

    return configuredColors.length > 0 ? configuredColors : ["currentColor"];
  }

  function randomColor(el) {
    const colors = getLoadingBarColors(el);
    if (window.crypto && typeof window.crypto.getRandomValues === "function") {
      const values = new Uint32Array(1);
      window.crypto.getRandomValues(values);
      return colors[values[0] % colors.length];
    }

    return colors[Math.floor(Math.random() * colors.length)];
  }

  function restartAnimation(el) {
    el.classList.remove("is-running");
    void el.offsetWidth;
    el.classList.add("is-running");
  }

  function startLoadingBar() {
    const el = getLoadingBar();
    if (!el) {
      return;
    }

    el.style.setProperty("--mst-loading-bar-color", randomColor(el));
    el.dataset.sequence = String((Number(el.dataset.sequence || "0") + 1));
    el.classList.add("is-visible");
    restartAnimation(el);
  }

  function stopLoadingBar() {
    const el = getLoadingBar();
    if (!el) {
      return;
    }

    el.classList.remove("is-visible", "is-running");
  }

  function isModifiedClick(event) {
    return event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
  }

  function shouldTrackLink(anchor) {
    if (!anchor || anchor.hasAttribute("download")) {
      return false;
    }

    const target = (anchor.getAttribute("target") || "").toLowerCase();
    if (target && target !== "_self") {
      return false;
    }

    const rawHref = anchor.getAttribute("href");
    if (!rawHref || rawHref.startsWith("#")) {
      return false;
    }

    let url;
    try {
      url = new URL(anchor.href, window.location.href);
    } catch {
      return false;
    }

    if (url.origin !== window.location.origin) {
      return false;
    }

    if (url.pathname === window.location.pathname && url.search === window.location.search) {
      return url.hash !== window.location.hash && !url.hash;
    }

    return true;
  }

  function handleClick(event) {
    if (event.defaultPrevented || isModifiedClick(event)) {
      return;
    }

    const anchor = event.target && event.target.closest ? event.target.closest("a[href]") : null;
    if (shouldTrackLink(anchor)) {
      startLoadingBar();
    }
  }

  function stopIfValidationFailed() {
    submitFallbackTimers.forEach(window.clearTimeout);
    submitFallbackTimers = [150, 500, 1200].map(function (delay) {
      return window.setTimeout(function () {
        const validationMessage = document.querySelector(".validation-message, .validation-summary-errors");
        if (validationMessage) {
          stopLoadingBar();
          submitFallbackTimers.forEach(window.clearTimeout);
          submitFallbackTimers = [];
        }
      }, delay);
    });
  }

  function handleSubmit(event) {
    if (event.defaultPrevented) {
      return;
    }

    startLoadingBar();
    stopIfValidationFailed();
  }

  function attachGlobalListeners() {
    if (fm.__loadingBarListenersAttached) {
      return;
    }

    document.addEventListener("click", handleClick, true);
    document.addEventListener("submit", handleSubmit, true);
    window.addEventListener("pageshow", stopLoadingBar);
    fm.__loadingBarListenersAttached = true;
  }

  fm.clickElementById = function (id) {
    try {
      var el = document.getElementById(id);
      if (el) el.click();
    } catch { }
  };

  fm.loadingBar = {
    start: startLoadingBar,
    restart: startLoadingBar,
    stop: stopLoadingBar
  };

  attachGlobalListeners();
})(window.financeManager);
