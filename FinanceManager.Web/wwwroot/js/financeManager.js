window.financeManager = window.financeManager || {};

(function (fm) {
  const loadingBarId = "fm-loading-bar";
  const keepaliveUrl = "/api/auth/keepalive";
  const keepaliveIntervalMs = 60000;
  const keepaliveForcedIntervalMs = 5000;
  const keepaliveTimeoutMs = 8000;
  const keepaliveInteractionEvents = ["pointerdown", "keydown", "focusin", "input"];
  let submitFallbackTimers = [];
  let keepaliveRequest = null;
  let keepaliveLastSuccess = 0;
  let keepaliveLastForcedStart = 0;
  let keepaliveAbortController = null;
  let keepaliveListenersAttached = false;

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

  function isPublicKeepalivePath() {
    const path = window.location.pathname || "/";
    return path === "/login"
      || path === "/register"
      || path === "/help"
      || path === "/error"
      || path.startsWith("/help/");
  }

  function triggerKeepalive(options) {
    options = options || {};
    const now = Date.now();

    if (isPublicKeepalivePath()) {
      return Promise.resolve(false);
    }

    if (keepaliveRequest) {
      return keepaliveRequest;
    }

    if (options.force) {
      if (now - keepaliveLastForcedStart < keepaliveForcedIntervalMs) {
        return Promise.resolve(false);
      }

      keepaliveLastForcedStart = now;
    } else if (now - keepaliveLastSuccess < keepaliveIntervalMs) {
      return Promise.resolve(false);
    }

    keepaliveAbortController = typeof AbortController === "function" ? new AbortController() : null;
    let timeoutHandle = null;
    if (keepaliveAbortController) {
      timeoutHandle = window.setTimeout(function () {
        try {
          keepaliveAbortController.abort();
        } catch { }
      }, keepaliveTimeoutMs);
    }

    keepaliveRequest = fetch(keepaliveUrl, {
      method: "GET",
      credentials: "include",
      cache: "no-store",
      headers: { "X-Requested-With": "fetch" },
      signal: keepaliveAbortController ? keepaliveAbortController.signal : undefined
    })
      .then(function (response) {
        if (response.ok) {
          keepaliveLastSuccess = Date.now();
          return true;
        }

        return false;
      })
      .catch(function () {
        return false;
      })
      .finally(function () {
        if (timeoutHandle !== null) {
          window.clearTimeout(timeoutHandle);
        }

        keepaliveRequest = null;
        keepaliveAbortController = null;
      });

    return keepaliveRequest;
  }

  function handleKeepaliveInteraction(event) {
    if (event
      && event.type === "input"
      && event.target
      && event.target.matches
      && event.target.matches("[data-fm-quickedit-keepalive]")) {
      return;
    }

    triggerKeepalive();
  }

  function handleQuickEditBlur(event) {
    const target = event.target;
    if (!target || !target.matches || !target.matches("[data-fm-quickedit-keepalive]")) {
      return;
    }

    window.setTimeout(function () {
      triggerKeepalive({ force: true, replace: true });
    }, 0);
  }

  function registerKeepalive() {
    if (keepaliveListenersAttached) {
      return;
    }

    keepaliveInteractionEvents.forEach(function (eventName) {
      document.addEventListener(eventName, handleKeepaliveInteraction, true);
    });
    document.addEventListener("blur", handleQuickEditBlur, true);
    keepaliveListenersAttached = true;
  }

  function unregisterKeepalive() {
    if (!keepaliveListenersAttached) {
      return;
    }

    keepaliveInteractionEvents.forEach(function (eventName) {
      document.removeEventListener(eventName, handleKeepaliveInteraction, true);
    });
    document.removeEventListener("blur", handleQuickEditBlur, true);
    keepaliveListenersAttached = false;
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

  fm.quickEdit = {
    applyValues: function (values) {
      if (!Array.isArray(values)) return;
      values.forEach(function (v) {
        var el = document.getElementById(v.id);
        if (el) el.value = v.value;
      });
    }
  };

  fm.keepalive = {
    ping: triggerKeepalive,
    register: registerKeepalive,
    unregister: unregisterKeepalive
  };

  fm.loadingBar = {
    start: startLoadingBar,
    restart: startLoadingBar,
    stop: stopLoadingBar
  };

  attachGlobalListeners();
  registerKeepalive();
})(window.financeManager);
