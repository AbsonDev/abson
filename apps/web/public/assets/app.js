(function () {
  "use strict";

  var root = document.documentElement;

  var themeToggle = document.querySelector(".theme-toggle");
  var THEME_KEY = "abson-theme";

  function storedTheme() {
    try {
      return window.localStorage.getItem(THEME_KEY);
    } catch (error) {
      return null;
    }
  }

  function applyTheme(theme) {
    root.setAttribute("data-theme", theme);
    var light = theme === "light";
    themeToggle.setAttribute("aria-pressed", light ? "true" : "false");
    themeToggle.setAttribute("aria-label", light ? "Ativar tema escuro" : "Ativar tema claro");
  }

  applyTheme(storedTheme() === "light" ? "light" : "dark");

  themeToggle.addEventListener("click", function () {
    var next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
    applyTheme(next);
    try {
      window.localStorage.setItem(THEME_KEY, next);
    } catch (error) {
      /* memory fallback is the applied theme in this session */
    }
  });

  var nav = document.getElementById("nav-principal");
  var navToggle = document.querySelector(".nav-toggle");
  var mobile = window.matchMedia("(max-width: 860px)");

  function setNavOpen(open) {
    nav.hidden = !open;
    navToggle.setAttribute("aria-expanded", open ? "true" : "false");
    navToggle.textContent = open ? "Fechar" : "Menu";
  }

  function syncNav() {
    if (mobile.matches) {
      setNavOpen(false);
    } else {
      nav.hidden = false;
      navToggle.setAttribute("aria-expanded", "false");
      navToggle.textContent = "Menu";
    }
  }

  syncNav();

  if (typeof mobile.addEventListener === "function") {
    mobile.addEventListener("change", syncNav);
  } else if (typeof mobile.addListener === "function") {
    mobile.addListener(syncNav);
  }

  navToggle.addEventListener("click", function () {
    if (!mobile.matches) return;
    setNavOpen(nav.hidden);
  });

  nav.addEventListener("click", function (event) {
    var link = event.target.closest("a");
    if (link && mobile.matches) setNavOpen(false);
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && mobile.matches && !nav.hidden) {
      setNavOpen(false);
      navToggle.focus();
    }
  });

  var links = Array.prototype.slice.call(document.querySelectorAll('.nav a[href^="#"]'));
  var targets = links
    .map(function (link) {
      return document.getElementById(link.getAttribute("href").slice(1));
    })
    .filter(Boolean);

  if ("IntersectionObserver" in window && targets.length) {
    var observer = new IntersectionObserver(
      function (entries) {
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
        });
      },
      { rootMargin: "-45% 0px -50% 0px", threshold: 0 }
    );
    targets.forEach(function (target) {
      observer.observe(target);
    });
  }

  var casesElement = document.getElementById("site-cases");
  var projects = {};
  if (casesElement) {
    try {
      JSON.parse(casesElement.textContent).forEach(function (item) {
        if (item && item.slug) projects[item.slug] = item;
      });
    } catch (error) {
      projects = {};
    }
  }

  var dialog = document.getElementById("case-dialog");
  var closeButton = document.getElementById("case-close");
  var caseFields = {
    kicker: document.getElementById("case-kicker"),
    title: document.getElementById("case-title"),
    context: document.getElementById("case-context"),
    decision: document.getElementById("case-decision"),
    result: document.getElementById("case-result"),
    stack: document.getElementById("case-stack"),
    note: document.getElementById("case-note")
  };
  var opener = null;

  function openCase(id, trigger) {
    var data = projects[id];
    if (!data) return;
    caseFields.kicker.textContent = data.kicker;
    caseFields.title.textContent = data.title;
    caseFields.context.textContent = data.context;
    caseFields.decision.textContent = data.decision;
    caseFields.result.textContent = data.result;
    if (caseFields.note) {
      caseFields.note.textContent =
        data.note ||
        "Ficha baseada em experiência profissional real — sem cliente identificado ou métrica de resultado.";
    }
    caseFields.stack.textContent = "";
    (data.stack || []).forEach(function (item) {
      var chip = document.createElement("li");
      chip.className = "tag";
      chip.textContent = item;
      caseFields.stack.appendChild(chip);
    });
    opener = trigger;
    if (typeof dialog.showModal === "function") {
      dialog.showModal();
    }
  }

  Array.prototype.forEach.call(document.querySelectorAll("[data-case]"), function (button) {
    button.addEventListener("click", function () {
      openCase(button.getAttribute("data-case"), button);
    });
  });

  closeButton.addEventListener("click", function () {
    dialog.close();
  });

  dialog.addEventListener("close", function () {
    if (opener && document.contains(opener)) {
      opener.focus();
    }
  });

  dialog.addEventListener("click", function (event) {
    if (event.target === dialog) {
      dialog.close();
    }
  });

  var form = document.getElementById("contact-form");
  var summary = document.getElementById("resumo-erros");
  var summaryTitle = document.getElementById("resumo-titulo");
  var summaryList = document.getElementById("lista-erros");
  var status = document.getElementById("form-status");
  var statusTitle = document.getElementById("status-title");
  var resetButton = document.getElementById("reset-form");
  var submitButton = form ? form.querySelector('button[type="submit"]') : null;
  var submitLabel = submitButton ? submitButton.textContent : "Enviar mensagem";
  var websiteField = document.getElementById("campo-website");
  var API_BASE =
    window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1"
      ? "http://localhost:8080"
      : "https://api.abson.tech";

  function setSending(sending) {
    if (!submitButton) return;
    submitButton.disabled = sending;
    submitButton.textContent = sending ? "Enviando..." : submitLabel;
  }

  function showSendError() {
    summaryList.textContent = "";
    var item = document.createElement("li");
    item.textContent =
      "Não foi possível enviar agora. Tente novamente em instantes ou envie um e-mail para absongalvao@gmail.com.";
    summaryList.appendChild(item);
    summaryTitle.textContent = "Falha ao enviar";
    summary.hidden = false;
    summaryTitle.focus();
  }

  var EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;

  var controls = [
    { name: "nome", input: document.getElementById("campo-nome"), error: document.getElementById("erro-nome") },
    { name: "email", input: document.getElementById("campo-email"), error: document.getElementById("erro-email") },
    { name: "assunto", input: document.getElementById("campo-assunto"), error: document.getElementById("erro-assunto") },
    { name: "mensagem", input: document.getElementById("campo-mensagem"), error: document.getElementById("erro-mensagem") }
  ];

  function messageFor(name, value) {
    var clean = value.trim();
    if (name === "nome") {
      if (!clean) return "Informe seu nome para eu saber com quem falo.";
      if (clean.length < 2) return "O nome precisa de pelo menos 2 caracteres.";
      return "";
    }
    if (name === "email") {
      if (!clean) return "Informe um e-mail para eu poder responder.";
      if (!EMAIL.test(clean)) return "Endereço incompleto — use o formato nome@dominio.com.";
      return "";
    }
    if (name === "assunto") {
      if (!clean) return "Escolha o tipo de conversa para eu preparar o retorno.";
      return "";
    }
    if (name === "mensagem") {
      if (!clean) return "Descreva brevemente o problema ou o processo atual.";
      if (clean.length < 20) return "Conte um pouco mais: pelo menos 20 caracteres, com o contexto atual.";
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
    var invalid = controls
      .map(function (field) {
        return validateField(field) ? field : null;
      })
      .filter(Boolean);

    if (invalid.length) {
      summaryList.textContent = "";
      invalid.forEach(function (field) {
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
        invalid.length === 1
          ? "Revise 1 campo antes de enviar"
          : "Revise " + invalid.length + " campos antes de enviar";
      summary.hidden = false;
      summaryTitle.focus();
      return;
    }

    summary.hidden = true;
    setSending(true);

    fetch(API_BASE + "/api/contact", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: document.getElementById("campo-nome").value.trim(),
        email: document.getElementById("campo-email").value.trim(),
        subject: document.getElementById("campo-assunto").value,
        message: document.getElementById("campo-mensagem").value.trim(),
        website: websiteField ? websiteField.value : ""
      })
    })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("HTTP " + response.status);
        }
        return response.json();
      })
      .then(function () {
        form.reset();
        controls.forEach(function (field) {
          setFieldState(field, "");
        });
        form.hidden = true;
        status.hidden = false;
        statusTitle.focus();
      })
      .catch(showSendError)
      .finally(function () {
        setSending(false);
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
    controls[0].input.focus();
  });
})();
