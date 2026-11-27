window.financeManager = window.financeManager || {};

(function (fm) {
  const loadingBarId = "fm-loading-bar";
  const keepaliveUrl = "api/auth/keepalive";
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

  function getBasePath() {
    const baseEl = document.querySelector("base");
    if (!baseEl) return "";
    try {
      return new URL(baseEl.getAttribute("href"), window.location.origin).pathname.replace(/\/$/, "");
    } catch (e) {
      return "";
    }
  }

  function isPublicKeepalivePath() {
    const basePath = getBasePath().toLowerCase();
    let path = (window.location.pathname || "/").toLowerCase();
    if (basePath && path.startsWith(basePath)) {
      path = path.slice(basePath.length) || "/";
    }
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

  function handleQuickEditKeyDown(event) {
    const target = event.target;
    if (!target || !target.id || !target.id.startsWith("qe_")) {
      return;
    }

    if (event.ctrlKey && (event.key === "ArrowUp" || event.key === "ArrowDown")) {
      event.preventDefault();
    }
  }

  function attachGlobalListeners() {
    if (fm.__loadingBarListenersAttached) {
      return;
    }

    document.addEventListener("click", handleClick, true);
    document.addEventListener("submit", handleSubmit, true);
    document.addEventListener("keydown", handleQuickEditKeyDown, true);
    window.addEventListener("pageshow", stopLoadingBar);
    fm.__loadingBarListenersAttached = true;
  }

  fm.clickElementById = function (id) {
    try {
      var el = document.getElementById(id);
      if (el) el.click();
    } catch { }
  };

  fm.downloadFromUrl = function (url) {
    try {
      var a = document.createElement("a");
      a.href = url;
      a.download = "";
      a.style.display = "none";
      document.body.appendChild(a);
      a.click();
      window.setTimeout(function () {
        document.body.removeChild(a);
      }, 200);
      return true;
    } catch {
      return false;
    }
  };

  fm.localStorage = {
    removeByPrefix: function (prefix) {
      if (!prefix) return;
      Object.keys(localStorage)
        .filter(function (k) { return k.indexOf(prefix) === 0; })
        .forEach(function (k) { localStorage.removeItem(k); });
    }
  };

  fm.quickEdit = {
    applyValues: function (values) {
      if (!Array.isArray(values)) return;
      values.forEach(function (v) {
        var el = document.getElementById(v.id);
        if (el) el.value = v.value;
      });
    },
    focusById: function (id) {
      var el = document.getElementById(id);
      if (el && typeof el.focus === "function") {
        el.focus();
      }
    },
    isFocusInsideRow: function (rowId) {
      var row = document.getElementById(rowId);
      var active = document.activeElement;
      return !!(row && active && (row === active || row.contains(active)));
    }
  };

  fm.keepalive = {
    ping: triggerKeepalive,
    register: registerKeepalive,
    unregister: unregisterKeepalive
  };

  // Navigation rail expand/collapse state, persisted in localStorage.
  // The expanded flag is mirrored as a class on <html> so pure CSS can react;
  // initializing it here (script loads in <head>) avoids a flash of the wrong mode.
  var navStorageKey = "fm.nav.expanded";
  var navExpandedClass = "fm-nav-expanded";

  function navIsExpanded() {
    try { return window.localStorage.getItem(navStorageKey) === "1"; }
    catch { return false; }
  }

  function navSetExpanded(expanded) {
    try { window.localStorage.setItem(navStorageKey, expanded ? "1" : "0"); }
    catch { }
    document.documentElement.classList.toggle(navExpandedClass, !!expanded);
  }

  function navSyncToggle() {
    var btn = document.querySelector(".nav-collapse-toggle");
    if (!btn) { return; }
    var expanded = navIsExpanded();
    btn.setAttribute("aria-expanded", expanded ? "true" : "false");
    var label = expanded ? btn.getAttribute("data-label-collapse") : btn.getAttribute("data-label-expand");
    if (label) {
      btn.setAttribute("title", label);
      var span = btn.querySelector(".nav-collapse-label");
      if (span) { span.textContent = label; }
    }
  }

  fm.nav = {
    isExpanded: navIsExpanded,
    setExpanded: function (expanded) { navSetExpanded(expanded); navSyncToggle(); },
    toggleExpanded: function () { fm.nav.setExpanded(!navIsExpanded()); }
  };

  function navApply() {
    document.documentElement.classList.toggle(navExpandedClass, navIsExpanded());
    navSyncToggle();
  }

  navApply();

  // Blazor page transitions may rewrite attributes on <html>, dropping the
  // expanded class. Re-apply it whenever it disappears while the stored
  // preference still says expanded.
  new MutationObserver(navApply).observe(document.documentElement, {
    attributes: true,
    attributeFilter: ["class"]
  });

  document.addEventListener("click", function (e) {
    if (e.target && e.target.closest && e.target.closest(".nav-collapse-toggle")) {
      fm.nav.toggleExpanded();
    }
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", navSyncToggle);
  } else {
    navSyncToggle();
  }
  document.addEventListener("enhancedload", navApply);

  // Ribbon label density (Issue #586 follow-up): labels are hidden only when
  // hiding them actually saves a wrapped row. The groups wrap via flexbox, so
  // each level is tried in turn and the level with the most labels that still
  // reaches the lowest achievable row count wins. Fixed breakpoints proved too
  // coarse: on pages with few actions labels vanished despite free space.
  var ribbonLevels = ["", "fm-ribbon--hide-small-labels", "fm-ribbon--hide-labels"];
  var ribbonObserved = typeof WeakSet === "function" ? new WeakSet() : null;
  var ribbonScheduled = typeof WeakSet === "function" ? new WeakSet() : null;
  var ribbonPendingFrame = false;

  function ribbonVisibleDesktopGroups(ribbon) {
    var groups = ribbon.querySelector(".fm-ribbon-groups");
    if (!groups) { return []; }
    return Array.prototype.filter.call(groups.children, function (el) {
      return el.classList && el.classList.contains("fm-ribbon-group-desktop") && el.offsetParent !== null;
    });
  }

  function ribbonRowCount(groupEls) {
    var tops = [];
    for (var i = 0; i < groupEls.length; i++) {
      var t = groupEls[i].offsetTop;
      if (!tops.some(function (x) { return Math.abs(x - t) <= 2; })) { tops.push(t); }
    }
    return tops.length;
  }

  function ribbonEvaluate(ribbon) {
    if (!ribbon.isConnected) { return; }
    var groupEls = ribbonVisibleDesktopGroups(ribbon);
    if (!groupEls.length) {
      // Mobile layout or empty ribbon: desktop buttons are hidden anyway.
      ribbon.classList.remove(ribbonLevels[1], ribbonLevels[2]);
      return;
    }
    var rowsByLevel = [];
    for (var i = 0; i < ribbonLevels.length; i++) {
      ribbon.classList.remove(ribbonLevels[1], ribbonLevels[2]);
      if (ribbonLevels[i]) { ribbon.classList.add(ribbonLevels[i]); }
      rowsByLevel.push(ribbonRowCount(groupEls));
    }
    var minRows = Math.min.apply(Math, rowsByLevel);
    var best = 0;
    for (var j = 0; j < rowsByLevel.length; j++) {
      if (rowsByLevel[j] === minRows) { best = j; break; }
    }
    ribbon.classList.remove(ribbonLevels[1], ribbonLevels[2]);
    if (ribbonLevels[best]) { ribbon.classList.add(ribbonLevels[best]); }
  }

  function ribbonSchedule(ribbon) {
    if (ribbonScheduled && ribbonScheduled.has(ribbon)) { return; }
    if (ribbonScheduled) { ribbonScheduled.add(ribbon); }
    window.requestAnimationFrame(function () {
      if (ribbonScheduled) { ribbonScheduled.delete(ribbon); }
      ribbonEvaluate(ribbon);
    });
  }

  function ribbonObserve(ribbon) {
    if (ribbonObserved && ribbonObserved.has(ribbon)) { return; }
    if (ribbonObserved) { ribbonObserved.add(ribbon); }
    if (typeof ResizeObserver === "function") {
      new ResizeObserver(function () { ribbonSchedule(ribbon); }).observe(ribbon);
    }
    // Blazor re-renders can swap items inside the same ribbon element.
    new MutationObserver(function () { ribbonSchedule(ribbon); })
      .observe(ribbon, { childList: true, subtree: true });
    ribbonSchedule(ribbon);
  }

  function ribbonScan(root) {
    var ribbons = root.querySelectorAll ? root.querySelectorAll(".fm-ribbon") : [];
    Array.prototype.forEach.call(ribbons, ribbonObserve);
  }

  function ribbonScanAll() {
    if (document.body) { ribbonScan(document.body); }
  }

  // Watches for ribbons added by client-side navigation or first render.
  new MutationObserver(function (mutations) {
    var needsScan = mutations.some(function (m) {
      return Array.prototype.some.call(m.addedNodes, function (n) {
        return n.nodeType === 1 && (n.matches && n.matches(".fm-ribbon") || n.querySelector && n.querySelector(".fm-ribbon"));
      });
    });
    if (needsScan && !ribbonPendingFrame) {
      ribbonPendingFrame = true;
      window.requestAnimationFrame(function () {
        ribbonPendingFrame = false;
        ribbonScanAll();
      });
    }
  }).observe(document.documentElement, { childList: true, subtree: true });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", ribbonScanAll);
  } else {
    ribbonScanAll();
  }
  document.addEventListener("enhancedload", ribbonScanAll);

  fm.loadingBar = {
    start: startLoadingBar,
    restart: startLoadingBar,
    stop: stopLoadingBar
  };

  // Copies text to the clipboard. Used by the MCP token one-time display; falls
  // back to a temporary textarea for contexts without the async clipboard API.
  fm.copyText = function (text) {
    text = text || "";
    if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
      return navigator.clipboard.writeText(text).then(function () { return true; }).catch(function () { return false; });
    }
    try {
      var ta = document.createElement("textarea");
      ta.value = text;
      ta.setAttribute("readonly", "");
      ta.style.position = "absolute";
      ta.style.left = "-9999px";
      document.body.appendChild(ta);
      ta.select();
      var ok = false;
      try { ok = document.execCommand("copy"); } catch { }
      document.body.removeChild(ta);
      return Promise.resolve(ok);
    } catch {
      return Promise.resolve(false);
    }
  };

  attachGlobalListeners();
  registerKeepalive();
})(window.financeManager);
