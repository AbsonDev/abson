(function () {
  "use strict";

  var root = document.documentElement;
  var DICT = window.ABSON_I18N || {};
  if (window.ABSON_CMS_I18N && DICT.pt) {
    Object.keys(window.ABSON_CMS_I18N).forEach(function (key) {
      DICT.pt[key] = window.ABSON_CMS_I18N[key];
    });
  }
  if (window.ABSON_EXTRA_I18N) {
    Object.keys(window.ABSON_EXTRA_I18N).forEach(function (locale) {
      if (DICT[locale]) Object.assign(DICT[locale], window.ABSON_EXTRA_I18N[locale]);
    });
  }
  var LOCALE_KEY = "abson-locale";
  var THEME_KEY = "abson-theme";
  var EMAIL_ADDRESS = document.body.getAttribute("data-contact-email") || "absongalvao@gmail.com";
  var API_BASE = (document.body.getAttribute("data-api-base") || "https://api.abson.tech").replace(/\/$/, "");
  var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

  var themeToggle = document.querySelector(".theme-toggle");
  var langSelect = document.getElementById("language-select");
  var announcer = document.getElementById("status-announcer");

  function readStore(key) {
    try {
      return window.localStorage.getItem(key);
    } catch (error) {
      return null;
    }
  }

  function writeStore(key, value) {
    try {
      window.localStorage.setItem(key, value);
    } catch (error) {
      /* memory fallback applies for the current session */
    }
  }

  function normalizeLocale(value) {
    if (!value) return null;
    var lower = String(value).toLowerCase();
    if (lower.indexOf("pt") === 0) return "pt";
    if (lower.indexOf("en") === 0) return "en";
    if (lower.indexOf("es") === 0) return "es";
    return null;
  }

  function localeFromQuery() {
    try {
      var params = new URLSearchParams(window.location.search);
      return normalizeLocale(params.get("lang"));
    } catch (error) {
      return null;
    }
  }

  function localeFromNavigator() {
    var language = navigator.language || navigator.userLanguage || "pt";
    return normalizeLocale(language) || "pt";
  }

  var storedLocale = normalizeLocale(readStore(LOCALE_KEY));
  var currentLocale = localeFromQuery() || storedLocale || localeFromNavigator();
  if (!DICT[currentLocale]) currentLocale = "pt";

  var storedTheme = readStore(THEME_KEY);
  var themeLocked = storedTheme === "light" || storedTheme === "dark";
  var systemLight = window.matchMedia("(prefers-color-scheme: light)");

  function t(key) {
    var bundle = DICT[currentLocale] || {};
    if (bundle[key] != null) return bundle[key];
    if (DICT.pt && DICT.pt[key] != null) return DICT.pt[key];
    return key;
  }

  function announce(message) {
    if (!announcer || !message) return;
    announcer.textContent = "";
    window.setTimeout(function () {
      announcer.textContent = message;
    }, 60);
  }

  function setMetaContent(selector, value) {
    var el = document.querySelector(selector);
    if (el) el.setAttribute("content", value);
  }

  function updateThemeMeta(theme) {
    var meta = document.getElementById("meta-theme-color");
    if (meta) meta.setAttribute("content", theme === "light" ? "#f6f5f2" : "#0b0b0c");
  }

  function setTheme(theme, announceChange) {
    root.setAttribute("data-theme", theme);
    var light = theme === "light";
    themeToggle.setAttribute("aria-pressed", light ? "true" : "false");
    themeToggle.setAttribute("aria-label", t(light ? "ui.themeToDark" : "ui.themeToLight"));
    updateThemeMeta(theme);
    if (announceChange) {
      announce(t(light ? "ui.themeLight" : "ui.themeDark"));
    }
  }

  setTheme(root.getAttribute("data-theme") === "light" ? "light" : "dark", false);

  function onSystemThemeChange(event) {
    if (themeLocked) return;
    setTheme(event.matches ? "light" : "dark", false);
  }

  if (typeof systemLight.addEventListener === "function") {
    systemLight.addEventListener("change", onSystemThemeChange);
  } else if (typeof systemLight.addListener === "function") {
    systemLight.addListener(onSystemThemeChange);
  }

  var nav = document.getElementById("nav-principal");
  var navToggle = document.querySelector(".nav-toggle");
  var mobile = window.matchMedia("(max-width: 1080px)");

  function refreshNavLabel() {
    navToggle.textContent = nav.hidden ? t("ui.menu") : t("ui.close");
  }

  function setNavOpen(open) {
    nav.hidden = !open;
    navToggle.setAttribute("aria-expanded", open ? "true" : "false");
    refreshNavLabel();
  }

  function syncNav() {
    if (mobile.matches) {
      setNavOpen(false);
    } else {
      nav.hidden = false;
      navToggle.setAttribute("aria-expanded", "false");
      refreshNavLabel();
    }
  }

  navToggle.addEventListener("click", function () {
    if (!mobile.matches) return;
    setNavOpen(nav.hidden);
  });

  var navClickLocked = false;
  var navClickTimer = 0;

  nav.addEventListener("click", function (event) {
    var link = event.target.closest("a");
    if (!link) return;
    var href = link.getAttribute("href");
    if (href && href.charAt(0) === "#") {
      var current = "#" + href.slice(1);
      links.forEach(function (item) {
        var active = item.getAttribute("href") === current;
        item.classList.toggle("is-active", active);
        if (active) {
          item.setAttribute("aria-current", "location");
        } else {
          item.removeAttribute("aria-current");
        }
      });
      syncNavIndicator();
      navClickLocked = true;
      window.clearTimeout(navClickTimer);
      navClickTimer = window.setTimeout(function () {
        navClickLocked = false;
      }, 700);
    }
    if (mobile.matches) setNavOpen(false);
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && mobile.matches && !nav.hidden) {
      setNavOpen(false);
      navToggle.focus();
    }
  });

  var syncNavIndicator = (function () {
    var nav = document.getElementById("nav-principal");
    var indicator = nav ? nav.querySelector(".nav-indicator") : null;
    if (!nav || !indicator) return function () {};

    function place() {
      var active = nav.querySelector("a.is-active");
      if (!active) {
        indicator.classList.remove("is-visible");
        return;
      }
      var left = active.offsetLeft;
      var width = active.offsetWidth;
      var top = active.offsetTop + active.offsetHeight - 4;
      indicator.style.top = top + "px";
      indicator.style.width = width + "px";
      indicator.style.transform = "translate3d(" + left + "px, 0, 0)";
      indicator.classList.add("is-visible");
    }

    window.addEventListener("resize", place);
    if ("ResizeObserver" in window) {
      new ResizeObserver(place).observe(nav);
    }
    return place;
  })();

  var links = Array.prototype.slice.call(document.querySelectorAll('.nav a[href^="#"]'));
  var targets = links
    .map(function (link) {
      return document.getElementById(link.getAttribute("href").slice(1));
    })
    .filter(Boolean);

  if ("IntersectionObserver" in window && targets.length) {
    var observer = new IntersectionObserver(
      function (entries) {
        if (navClickLocked) return;
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          var id = entry.target.id;
          links.forEach(function (link) {
            var active = link.getAttribute("href") === "#" + id;
            link.classList.toggle("is-active", active);
            if (active) {
              link.setAttribute("aria-current", "location");
            } else {
              link.removeAttribute("aria-current");
            }
          });
          syncNavIndicator();
        });
      },
      { rootMargin: "-45% 0px -50% 0px", threshold: 0 }
    );
    targets.forEach(function (target) {
      observer.observe(target);
    });
  }

  var header = document.querySelector(".site-header");
  var scrollProgress = document.querySelector(".scroll-progress");
  var scrollTicking = false;

  var parallax = (function () {
    if (reduceMotion.matches) return null;
    var items = [];
    var narrow = window.matchMedia("(max-width: 640px)");

    function number(value, fallback) {
      var parsed = parseFloat(value);
      return parsed === parsed ? parsed : fallback;
    }

    function sourceFor(el) {
      var source = el.getAttribute("data-parallax-source");
      if (source === "section") return el.closest(".section");
      if (source === "parent") return el.parentElement;
      return null;
    }

    function push(el, amount, kind, direction) {
      if (!amount) return;
      var dir = direction === undefined ? number(el.getAttribute("data-parallax-direction"), 1) : direction;
      items.push({ el: el, box: sourceFor(el), amount: amount, dir: dir, kind: kind });
    }

    Array.prototype.forEach.call(document.querySelectorAll("[data-parallax]"), function (el) {
      var kind =
        el.classList.contains("atmos-layer") || el.classList.contains("section-ghost") ? "ambient" : "content";
      push(el, number(el.getAttribute("data-parallax"), 0), kind);
    });

    Array.prototype.forEach.call(document.querySelectorAll("[data-parallax-rows]"), function (group) {
      var base = number(group.getAttribute("data-parallax-rows"), 0);
      var stagger = number(group.getAttribute("data-parallax-stagger"), 0);
      var direction = number(group.getAttribute("data-parallax-direction"), 1);
      Array.prototype.forEach.call(group.children, function (child, index) {
        push(child, base + index * stagger, "content", direction);
      });
    });

    return items.length ? { items: items, narrow: narrow } : null;
  })();

  function syncParallax() {
    if (!parallax) return;
    var vh = window.innerHeight || 1;
    var narrow = parallax.narrow.matches;
    var writes = [];

    parallax.items.forEach(function (item) {
      if (narrow && item.kind === "content") {
        writes.push([item.el, ""]);
        return;
      }
      var box = item.box || item.el;
      var rect = box.getBoundingClientRect();
      var span = vh / 2 + rect.height / 2;
      var progress = span > 0 ? (vh / 2 - (rect.top + rect.height / 2)) / span : 0;
      progress = Math.max(-1, Math.min(1, progress));
      var y = progress * item.amount * item.dir;
      writes.push([item.el, y ? "0 " + y.toFixed(2) + "px" : ""]);
    });

    writes.forEach(function (entry) {
      if (entry[0].style.translate !== entry[1]) {
        entry[0].style.translate = entry[1];
      }
    });
  }

  function syncScroll() {
    scrollTicking = false;
    var doc = document.documentElement;
    var max = doc.scrollHeight - doc.clientHeight;
    var ratio = max > 0 ? Math.min(1, Math.max(0, doc.scrollTop / max)) : 0;
    if (scrollProgress) {
      scrollProgress.style.transform = "scaleX(" + ratio.toFixed(4) + ")";
    }
    if (header) {
      header.classList.toggle("is-scrolled", doc.scrollTop > 8);
    }
    syncParallax();
  }

  function requestScrollSync() {
    if (scrollTicking) return;
    scrollTicking = true;
    window.requestAnimationFrame(syncScroll);
  }

  window.addEventListener("scroll", requestScrollSync, { passive: true });
  window.addEventListener("resize", requestScrollSync);
  syncScroll();

  (function () {
    var canvas = document.querySelector("[data-dot-field]");
    var hero = canvas ? canvas.closest(".hero") : null;
    var ctx = canvas && canvas.getContext ? canvas.getContext("2d") : null;
    if (!canvas || !hero || !ctx) return;

    var stage = document.createElement("canvas");
    var stageCtx = stage.getContext("2d");
    var wide = window.matchMedia("(min-width: 641px)");
    var fine = window.matchMedia("(hover: hover) and (pointer: fine)");
    var size = { w: 0, h: 0, dpr: 1 };
    var spacing = wide.matches ? 26 : 24;
    var baseRadius = 1.35;
    var reach = 120;
    var baseColor = "rgba(52, 52, 58, 0.82)";
    var hotColor = "rgba(133, 133, 140, 0.92)";
    var cursor = { x: -5000, y: -5000 };
    var target = { x: -5000, y: -5000 };
    var inside = false;
    var enabled = false;
    var frame = 0;

    function parseHex(raw) {
      var hex = String(raw || "").trim().replace("#", "");
      if (hex.length === 3) {
        hex =
          hex.charAt(0) +
          hex.charAt(0) +
          hex.charAt(1) +
          hex.charAt(1) +
          hex.charAt(2) +
          hex.charAt(2);
      }
      if (hex.length !== 6) return null;
      var number = parseInt(hex, 16);
      if (number !== number) return null;
      return { r: (number >> 16) & 255, g: (number >> 8) & 255, b: number & 255 };
    }

    function toRgba(rgb, alpha) {
      return "rgba(" + rgb.r + ", " + rgb.g + ", " + rgb.b + ", " + alpha + ")";
    }

    function readColors() {
      var style = window.getComputedStyle(root);
      var light = root.getAttribute("data-theme") === "light";
      var base = parseHex(style.getPropertyValue("--border-strong")) || { r: 52, g: 52, b: 58 };
      var hot = parseHex(style.getPropertyValue("--fg-subtle")) || { r: 133, g: 133, b: 140 };
      baseColor = toRgba(base, light ? 0.66 : 0.82);
      hotColor = toRgba(hot, light ? 0.82 : 0.92);
    }

    function paint() {
      if (!size.w || !size.h) return;
      var step = spacing * size.dpr;
      var radius = baseRadius * size.dpr;
      var rows = Math.floor(size.h / spacing);
      var cols = Math.floor(size.w / spacing);
      stageCtx.clearRect(0, 0, stage.width, stage.height);
      stageCtx.fillStyle = baseColor;
      for (var row = 0; row <= rows; row++) {
        var y = row * step + step / 2;
        if (y > stage.height) break;
        for (var col = 0; col <= cols; col++) {
          var x = col * step + step / 2;
          if (x > stage.width) break;
          stageCtx.beginPath();
          stageCtx.arc(x, y, radius, 0, Math.PI * 2);
          stageCtx.fill();
        }
      }
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(stage, 0, 0);
    }

    function resetCursor() {
      target.x = -spacing * 2;
      target.y = -spacing * 2;
      cursor.x = target.x;
      cursor.y = target.y;
      inside = false;
      if (frame) {
        window.cancelAnimationFrame(frame);
        frame = 0;
      }
    }

    function resize() {
      var rect = canvas.getBoundingClientRect();
      if (!rect.width || !rect.height) return;
      var dpr = Math.min(window.devicePixelRatio || 1, 2);
      size.w = Math.round(rect.width);
      size.h = Math.round(rect.height);
      size.dpr = dpr;
      canvas.width = Math.round(size.w * dpr);
      canvas.height = Math.round(size.h * dpr);
      stage.width = canvas.width;
      stage.height = canvas.height;
      resetCursor();
      paint();
    }

    function draw() {
      frame = 0;
      if (!size.w || !size.h || !enabled) return;
      var dpr = size.dpr;
      var step = spacing * dpr;
      var reachDev = reach * dpr;
      cursor.x += (target.x - cursor.x) * 0.18;
      cursor.y += (target.y - cursor.y) * 0.18;

      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.drawImage(stage, 0, 0);

      var colStart = Math.floor((cursor.x - reachDev) / step) - 1;
      var colEnd = Math.floor((cursor.x + reachDev) / step) + 1;
      var rowStart = Math.floor((cursor.y - reachDev) / step) - 1;
      var rowEnd = Math.floor((cursor.y + reachDev) / step) + 1;

      ctx.fillStyle = hotColor;
      for (var row = rowStart; row <= rowEnd; row++) {
        var y = row * step + step / 2;
        if (y < 0 || y > canvas.height) continue;
        for (var col = colStart; col <= colEnd; col++) {
          var x = col * step + step / 2;
          if (x < 0 || x > canvas.width) continue;
          var dx = x - cursor.x;
          var dy = y - cursor.y;
          var dist = Math.sqrt(dx * dx + dy * dy);
          if (dist >= reachDev) continue;
          var falloff = 1 - dist / reachDev;
          falloff = falloff * falloff;
          ctx.globalAlpha = Math.min(1, falloff * 1.15);
          ctx.beginPath();
          ctx.arc(x, y, (baseRadius + 1.05 * falloff) * dpr, 0, Math.PI * 2);
          ctx.fill();
        }
      }
      ctx.globalAlpha = 1;

      var remainingX = target.x - cursor.x;
      var remainingY = target.y - cursor.y;
      if (Math.abs(remainingX) > 0.5 * dpr || Math.abs(remainingY) > 0.5 * dpr) {
        frame = window.requestAnimationFrame(draw);
      }
    }

    function requestDraw() {
      if (!enabled || frame) return;
      frame = window.requestAnimationFrame(draw);
    }

    function onPointer(event) {
      if (event.pointerType === "touch") return;
      var rect = canvas.getBoundingClientRect();
      var inField =
        event.clientX >= rect.left - 8 &&
        event.clientX <= rect.right + 8 &&
        event.clientY >= rect.top - 8 &&
        event.clientY <= rect.bottom + 8;
      if (inField) {
        inside = true;
        target.x = (event.clientX - rect.left) * size.dpr;
        target.y = (event.clientY - rect.top) * size.dpr;
      } else if (inside) {
        inside = false;
        target.x = -reach * size.dpr * 2;
        target.y = -reach * size.dpr * 2;
      } else {
        return;
      }
      requestDraw();
    }

    function syncMode() {
      spacing = wide.matches ? 26 : 24;
      var canHover = wide.matches && fine.matches && !reduceMotion.matches;
      if (canHover && !enabled) {
        window.addEventListener("pointermove", onPointer, { passive: true });
      } else if (!canHover && enabled) {
        window.removeEventListener("pointermove", onPointer);
        resetCursor();
        paint();
      }
      enabled = canHover;
    }

    function refresh() {
      readColors();
      paint();
      if (enabled && inside) requestDraw();
    }

    if (typeof wide.addEventListener === "function") {
      wide.addEventListener("change", function () {
        syncMode();
        resize();
      });
    }
    if (typeof fine.addEventListener === "function") {
      fine.addEventListener("change", syncMode);
    }
    if (typeof reduceMotion.addEventListener === "function") {
      reduceMotion.addEventListener("change", syncMode);
    }

    if ("ResizeObserver" in window) {
      new ResizeObserver(function () {
        resize();
      }).observe(hero);
    }
    window.addEventListener("resize", resize);

    if ("MutationObserver" in window) {
      new MutationObserver(refresh).observe(root, {
        attributes: true,
        attributeFilter: ["data-theme"]
      });
    }

    document.addEventListener("visibilitychange", function () {
      if (document.hidden && frame) {
        window.cancelAnimationFrame(frame);
        frame = 0;
      }
    });

    readColors();
    syncMode();
    resize();
  })();

  if (root.classList.contains("js-reveal") && "IntersectionObserver" in window) {
    var revealList = [];

    function queueReveal(el, delay) {
      el.setAttribute("data-reveal", "pending");
      if (delay) el.style.setProperty("--reveal-delay", delay + "ms");
      revealList.push(el);
    }

    Array.prototype.forEach.call(document.querySelectorAll(".section-head"), function (head) {
      Array.prototype.forEach.call(
        head.querySelectorAll(".section-label, .section-title, .section-lead"),
        function (part, index) {
          queueReveal(part, index * 60);
        }
      );
    });

    [
      [".about-copy", "p", 60],
      [".principles", ".principle", 60],
      [".cap-grid", ".cap", 60],
      [".jobs", ".job", 70],
      [".cases", ".case", 70],
      [".steps", ".step", 70],
      [".edu-grid", ":scope > *", 80],
      [".contact-grid", ":scope > *", 80]
    ].forEach(function (group) {
      Array.prototype.forEach.call(document.querySelectorAll(group[0]), function (parent) {
        Array.prototype.forEach.call(parent.querySelectorAll(group[1]), function (el, index) {
          queueReveal(el, index * group[2]);
        });
      });
    });

    var revealObserver = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          var el = entry.target;
          revealObserver.unobserve(el);
          window.requestAnimationFrame(function () {
            el.setAttribute("data-reveal", "shown");
          });
        });
      },
      { rootMargin: "0px 0px -6% 0px", threshold: 0.08 }
    );

    revealList.forEach(function (el) {
      revealObserver.observe(el);
    });
  }

  var caseDetails = Array.prototype.slice.call(document.querySelectorAll(".case"));

  function syncCaseLabels() {
    caseDetails.forEach(function (detail) {
      var label = detail.querySelector(".case-action-label");
      if (!label) return;
      var key = detail.open ? "proj.close" : "proj.open";
      label.setAttribute("data-i18n", key);
      label.textContent = t(key);
    });
  }

  caseDetails.forEach(function (detail) {
    detail.addEventListener("toggle", function () {
      syncCaseLabels();
      requestScrollSync();
    });
  });

  function parseYm(value) {
    var match = /^(\d{4})-(\d{2})$/.exec(value || "");
    if (!match) return null;
    return { year: parseInt(match[1], 10), month: parseInt(match[2], 10) };
  }

  function durationLabel(months) {
    months = Math.max(1, months);
    var years = Math.floor(months / 12);
    var rest = months % 12;
    var parts = [];
    if (years) {
      parts.push(t(years === 1 ? "dur.yearOne" : "dur.yearMany").replace("{n}", String(years)));
    }
    if (rest) {
      parts.push(t(rest === 1 ? "dur.monthOne" : "dur.monthMany").replace("{n}", String(rest)));
    }
    return parts.join(" ");
  }

  function updateDurations() {
    var now = new Date();
    var current = { year: now.getFullYear(), month: now.getMonth() + 1 };
    Array.prototype.forEach.call(document.querySelectorAll("[data-start]"), function (el) {
      var start = parseYm(el.getAttribute("data-start"));
      if (!start) return;
      var end = parseYm(el.getAttribute("data-end")) || current;
      var months = (end.year - start.year) * 12 + (end.month - start.month) + 1;
      el.textContent = durationLabel(months);
    });
  }

  var form = document.getElementById("contact-form");
  var summary = document.getElementById("resumo-erros");
  var summaryTitle = document.getElementById("resumo-titulo");
  var summaryList = document.getElementById("lista-erros");
  var status = document.getElementById("form-status");
  var statusTitle = document.getElementById("status-title");
  var resetButton = document.getElementById("reset-form");
  var mailtoLink = document.getElementById("mailto-link");
  var messagePreview = document.getElementById("message-preview");
  var copyMessage = document.getElementById("copy-message");
  var copyMessageStatus = document.getElementById("copy-message-status");

  var EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
  var lastSubmission = null;
  var lastMessage = "";

  var controls = [
    { name: "nome", input: document.getElementById("campo-nome"), error: document.getElementById("erro-nome") },
    { name: "email", input: document.getElementById("campo-email"), error: document.getElementById("erro-email") },
    { name: "assunto", input: document.getElementById("campo-assunto"), error: document.getElementById("erro-assunto") },
    { name: "mensagem", input: document.getElementById("campo-mensagem"), error: document.getElementById("erro-mensagem") }
  ];

  function messageFor(name, value) {
    var clean = value.trim();
    if (name === "nome") {
      if (!clean) return t("form.nomeEmpty");
      if (clean.length < 2) return t("form.nomeShort");
      return "";
    }
    if (name === "email") {
      if (!clean) return t("form.emailEmpty");
      if (!EMAIL.test(clean)) return t("form.emailInvalid");
      return "";
    }
    if (name === "assunto") {
      if (!clean) return t("form.assuntoEmpty");
      return "";
    }
    if (name === "mensagem") {
      if (!clean) return t("form.mensagemEmpty");
      if (clean.length < 20) return t("form.mensagemShort");
      return "";
    }
    return "";
  }

  function setFieldState(field, message) {
    if (message) {
      field.error.textContent = message;
      field.error.hidden = false;
      field.input.setAttribute("aria-invalid", "true");
    } else {
      field.error.textContent = "";
      field.error.hidden = true;
      field.input.removeAttribute("aria-invalid");
    }
  }

  function validateField(field) {
    var message = messageFor(field.name, field.input.value);
    setFieldState(field, message);
    return message;
  }

  function invalidFields() {
    return controls.filter(function (field) {
      return field.input.getAttribute("aria-invalid") === "true";
    });
  }

  function renderSummary(fields) {
    summaryList.textContent = "";
    if (!fields.length) {
      summary.hidden = true;
      return;
    }
    fields.forEach(function (field) {
      var item = document.createElement("li");
      var link = document.createElement("a");
      link.href = "#" + field.input.id;
      link.textContent = field.error.textContent;
      link.addEventListener("click", function (clickEvent) {
        clickEvent.preventDefault();
        field.input.focus();
      });
      item.appendChild(link);
      summaryList.appendChild(item);
    });
    summaryTitle.textContent =
      fields.length === 1 ? t("form.summaryOne") : t("form.summaryMany").replace("{n}", String(fields.length));
    summary.hidden = false;
  }

  var SUBJECT_KEYS = {
    novo: "form.optNew",
    melhoria: "form.optImprove",
    integracao: "form.optIntegration",
    consultoria: "form.optConsult"
  };

  function composeMessage(payload) {
    var subjectLabel = payload.subjectKey ? t(payload.subjectKey) : payload.subject;
    var subject = t("form.mailSubject").replace("{subject}", subjectLabel);
    var lines = [
      t("form.mailName") + ": " + payload.name,
      t("form.mailEmail") + ": " + payload.email,
      t("form.mailSubjectLabel") + ": " + subjectLabel,
      "",
      t("form.mailMessageLabel") + ":",
      payload.message
    ];
    return { subject: subject, body: lines.join("\n") };
  }

  function renderMessage(payload) {
    if (!payload) return;
    var message = composeMessage(payload);
    lastMessage = message.body;
    if (messagePreview) messagePreview.textContent = message.body;
    if (mailtoLink) {
      mailtoLink.href =
        "mailto:" + EMAIL_ADDRESS + "?subject=" + encodeURIComponent(message.subject) +
        "&body=" + encodeURIComponent(message.body);
    }
  }

  function applyLocale(locale, options) {
    options = options || {};
    if (!DICT[locale]) locale = "pt";
    currentLocale = locale;
    root.setAttribute("lang", locale === "pt" ? "pt-BR" : locale);
    root.setAttribute("data-locale", locale);

    Array.prototype.forEach.call(document.querySelectorAll("[data-i18n]"), function (el) {
      el.textContent = t(el.getAttribute("data-i18n"));
    });
    Array.prototype.forEach.call(document.querySelectorAll("[data-i18n-html]"), function (el) {
      el.innerHTML = t(el.getAttribute("data-i18n-html"));
    });
    Array.prototype.forEach.call(document.querySelectorAll("[data-i18n-aria]"), function (el) {
      el.setAttribute("aria-label", t(el.getAttribute("data-i18n-aria")));
    });

    document.title = t("meta.title");
    setMetaContent('meta[name="description"]', t("meta.description"));
    setMetaContent('meta[property="og:title"]', t("meta.title"));
    setMetaContent('meta[property="og:description"]', t("meta.description"));
    setMetaContent('meta[name="twitter:title"]', t("meta.title"));
    setMetaContent('meta[name="twitter:description"]', t("meta.description"));
    setMetaContent('meta[property="og:image:alt"]', t("meta.imageAlt"));
    setMetaContent('meta[name="twitter:image:alt"]', t("meta.imageAlt"));
    setMetaContent(
      'meta[property="og:locale"]',
      locale === "pt" ? "pt_BR" : locale === "en" ? "en_US" : "es_ES"
    );

    langSelect.value = currentLocale;
    langSelect.setAttribute("aria-label", t("ui.language"));

    setTheme(root.getAttribute("data-theme") === "light" ? "light" : "dark", false);
    refreshNavLabel();
    if (copyButton) clearCopyFeedback();
    if (copyMessageStatus) {
      copyMessageStatus.textContent = "";
      copyMessageStatus.classList.remove("is-ok");
    }

    invalidFields().forEach(function (field) {
      validateField(field);
    });
    if (!summary.hidden) renderSummary(invalidFields());

    updateDurations();
    syncCaseLabels();

    if (status && !status.hidden && lastSubmission) {
      renderMessage(lastSubmission);
    }

    if (options.persist) {
      writeStore(LOCALE_KEY, currentLocale);
    }
    if (options.updateUrl) {
      updateLangUrl(currentLocale);
    }
    if (options.announceChange) {
      announce(t("ui.langChanged"));
    }
  }

  function updateLangUrl(locale) {
    try {
      var url = new URL(window.location.href);
      if (locale === "pt") {
        url.searchParams.delete("lang");
      } else {
        url.searchParams.set("lang", locale);
      }
      var next = url.pathname + (url.search ? url.search : "") + url.hash;
      window.history.replaceState(null, "", next);
    } catch (error) {
      /* URL metadata is optional; the view already updated */
    }
  }

  controls.forEach(function (field) {
    field.input.addEventListener("blur", function () {
      validateField(field);
    });
    field.input.addEventListener("input", function () {
      if (field.input.getAttribute("aria-invalid") === "true") {
        validateField(field);
      }
    });
  });

  form.addEventListener("submit", function (event) {
    event.preventDefault();
    var invalid = controls.filter(function (field) {
      return validateField(field) !== "";
    });

    if (invalid.length) {
      renderSummary(invalid);
      summaryTitle.focus();
      return;
    }

    var name = document.getElementById("campo-nome").value.trim();
    var email = document.getElementById("campo-email").value.trim();
    var message = document.getElementById("campo-mensagem").value.trim();
    var subjectSelect = document.getElementById("campo-assunto");
    var subjectValue = subjectSelect.value;

    lastSubmission = {
      name: name,
      email: email,
      subject: subjectSelect.options[subjectSelect.selectedIndex].textContent,
      subjectKey: SUBJECT_KEYS[subjectValue] || "",
      message: message
    };
    renderMessage(lastSubmission);

    var submitButton = form.querySelector('button[type="submit"]');
    var originalLabel = submitButton ? submitButton.textContent : "";
    if (submitButton) {
      submitButton.disabled = true;
      submitButton.textContent = t("form.sending");
    }

    fetch(API_BASE + "/api/contact", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: name,
        email: email,
        subject: subjectValue,
        message: message
      })
    })
      .then(function (response) {
        if (!response.ok) throw new Error("HTTP " + response.status);
        return response.json();
      })
      .then(function () {
        summary.hidden = true;
        form.reset();
        controls.forEach(function (field) {
          setFieldState(field, "");
        });
        form.hidden = true;
        status.hidden = false;
        statusTitle.focus();
      })
      .catch(function () {
        summaryList.textContent = "";
        var item = document.createElement("li");
        item.textContent = t("form.sendError");
        summaryList.appendChild(item);
        summaryTitle.textContent = t("form.sendErrorTitle");
        summary.hidden = false;
        summaryTitle.focus();
      })
      .finally(function () {
        if (submitButton) {
          submitButton.disabled = false;
          submitButton.textContent = originalLabel;
        }
      });
  });

  resetButton.addEventListener("click", function () {
    status.hidden = true;
    form.hidden = false;
    summary.hidden = true;
    form.reset();
    controls.forEach(function (field) {
      setFieldState(field, "");
    });
    lastSubmission = null;
    lastMessage = "";
    if (messagePreview) messagePreview.textContent = "";
    if (copyMessageStatus) {
      copyMessageStatus.textContent = "";
      copyMessageStatus.classList.remove("is-ok");
    }
    controls[0].input.focus();
  });

  var copyButton = document.getElementById("copy-email");
  var copyLabel = document.getElementById("copy-email-label");
  var copyStatus = document.getElementById("copy-status");
  var copyTimer = null;

  function fallbackCopy(text) {
    var area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.top = "-120px";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    area.setSelectionRange(0, area.value.length);
    var ok = false;
    try {
      ok = document.execCommand("copy");
    } catch (error) {
      ok = false;
    }
    document.body.removeChild(area);
    return ok;
  }

  function copyText(value, onResult) {
    function succeed() {
      onResult(true);
    }

    function fail() {
      if (fallbackCopy(value)) {
        succeed();
      } else {
        onResult(false);
      }
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      try {
        navigator.clipboard.writeText(value).then(succeed, fail);
      } catch (error) {
        fail();
      }
    } else if (fallbackCopy(value)) {
      succeed();
    } else {
      onResult(false);
    }
  }

  function clearCopyFeedback() {
    if (copyTimer) {
      window.clearTimeout(copyTimer);
      copyTimer = null;
    }
    if (!copyButton) return;
    copyButton.classList.remove("is-copied");
    if (copyLabel) copyLabel.textContent = t("contact.copy");
    if (copyStatus) {
      copyStatus.textContent = "";
      copyStatus.classList.remove("is-ok");
    }
  }

  function reportCopy(copied) {
    if (!copyButton) return;
    if (copyTimer) window.clearTimeout(copyTimer);

    if (copied) {
      copyButton.classList.add("is-copied");
      if (copyLabel) copyLabel.textContent = t("contact.copied");
      if (copyStatus) {
        copyStatus.textContent = t("contact.copiedStatus");
        copyStatus.classList.add("is-ok");
      }
    } else {
      copyButton.classList.remove("is-copied");
      if (copyLabel) copyLabel.textContent = t("contact.copy");
      if (copyStatus) {
        copyStatus.textContent = t("contact.copyFailed");
        copyStatus.classList.remove("is-ok");
      }
    }

    copyButton.focus();
    copyTimer = window.setTimeout(clearCopyFeedback, 2400);
  }

  if (copyButton) {
    copyButton.addEventListener("click", function () {
      var value = copyButton.getAttribute("data-copy-value") || "";
      copyText(value, reportCopy);
    });
  }

  if (copyMessage) {
    copyMessage.addEventListener("click", function () {
      if (!lastMessage) return;
      copyText(lastMessage, function (copied) {
        if (copyMessageStatus) {
          copyMessageStatus.textContent = copied ? t("form.copiedMessage") : t("form.copyFailed");
          copyMessageStatus.classList.toggle("is-ok", copied);
        }
        copyMessage.focus();
      });
    });
  }

  var themeTimer = null;

  themeToggle.addEventListener("click", function () {
    var next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
    themeLocked = true;
    writeStore(THEME_KEY, next);
    if (!reduceMotion.matches) {
      root.classList.add("is-theme-changing");
      window.clearTimeout(themeTimer);
      themeTimer = window.setTimeout(function () {
        root.classList.remove("is-theme-changing");
      }, 320);
    }
    setTheme(next, true);
  });

  var langSwapTimer = null;

  langSelect.addEventListener("change", function () {
    var next = normalizeLocale(langSelect.value);
    if (!next) {
      applyLocale(currentLocale, {});
      return;
    }
    if (!DICT[next] || reduceMotion.matches) {
      applyLocale(next, { persist: true, updateUrl: true, announceChange: true });
      return;
    }
    if (next === currentLocale) return;

    document.body.classList.add("is-lang-swapping");
    window.clearTimeout(langSwapTimer);
    langSwapTimer = window.setTimeout(function () {
      applyLocale(next, { persist: true, updateUrl: true, announceChange: true });
      window.requestAnimationFrame(function () {
        window.requestAnimationFrame(function () {
          document.body.classList.remove("is-lang-swapping");
        });
      });
    }, 140);
  });

  if (typeof mobile.addEventListener === "function") {
    mobile.addEventListener("change", syncNav);
  } else if (typeof mobile.addListener === "function") {
    mobile.addListener(syncNav);
  }

  applyLocale(currentLocale, {});
  updateDurations();
  syncCaseLabels();
  setTheme(root.getAttribute("data-theme") === "light" ? "light" : "dark", false);
  syncNav();
  syncNavIndicator();
})();
