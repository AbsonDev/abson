(function () {
  "use strict";

  const $ = (selector, root) => (root || document).querySelector(selector);
  const $$ = (selector, root) => Array.from((root || document).querySelectorAll(selector));
  const clone = (value) => JSON.parse(JSON.stringify(value));
  const esc = (value) =>
    String(value == null ? "" : value).replace(/[&<>"']/g, (char) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;",
    }[char]));
  const plural = (count, one, many) => count + " " + (count === 1 ? one : many);
  const norm = (value) =>
    String(value).normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();

  const ICONS = {
    check: '<path d="M20 6L9 17l-5-5"/>',
    checkCircle: '<circle cx="12" cy="12" r="9"/><path d="M8.5 12.5l2.2 2.2 4.8-4.8"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    trash: '<path d="M4 7h16"/><path d="M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2"/><path d="M6 7l1 13h10l1-13"/><path d="M10 11v5M14 11v5"/>',
    arrowRight: '<path d="M5 12h14"/><path d="M13 6l6 6-6 6"/>',
    mail: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 7l9 6 9-6"/>',
    archive: '<rect x="3" y="4" width="18" height="5" rx="1"/><path d="M5 9v9a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V9"/><path d="M10 13h4"/>',
    rotate: '<path d="M21 12a9 9 0 1 1-3-6.7"/><path d="M21 3v6h-6"/>',
    send: '<path d="M22 2L11 13"/><path d="M22 2l-7 20-4-9-9-4z"/>',
  };

  function icon(name, size) {
    const style = size ? ' style="--od-icon-size:' + size + 'px"' : "";
    return (
      '<svg class="od-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"' +
      style +
      ">" +
      (ICONS[name] || "") +
      "</svg>"
    );
  }

  const API_BASE = (document.body.getAttribute("data-api-base") || "https://api.abson.tech").replace(/\/$/, "");
  const TOKEN_KEY = "abson_admin_token";
  const STATUS_KEY = { new: "nova", read: "lida", replied: "respondida", archived: "arquivada" };
  const STATUS_LABEL = { nova: "Nova", lida: "Lida", respondida: "Respondida", arquivada: "Arquivada" };
  const TONES = ["purple", "cyan", "neutral", "warn"];

  const CAREER_STATUSES = [
    "Evaluated",
    "Applied",
    "Responded",
    "Assessment",
    "Interview",
    "Offer",
    "Hired",
    "Rejected",
    "Discarded",
    "SKIP",
  ];
  const CAREER_STATUS_LABELS = {
    Evaluated: "Avaliada",
    Applied: "Aplicada",
    Responded: "Respondida",
    Assessment: "Teste",
    Interview: "Entrevista",
    Offer: "Oferta",
    Hired: "Contratado",
    Rejected: "Rejeitada",
    Discarded: "Descartada",
    SKIP: "SKIP",
  };
  const CAREER_ACTION_LABELS = {
    set_status: "Mudar status",
    add_note: "Adicionar nota",
    add_pipeline_url: "Adicionar vaga",
  };

  const sections = [
    { id: "seo", path: "seo", num: "01", initial: "SE", name: "SEO", hint: "Título, descrição, canonical e imagem de compartilhamento." },
    { id: "perfil", path: "profile", num: "02", initial: "PE", name: "Perfil", hint: "Nome, marca, headline, apresentação e contato." },
    { id: "ficha", path: "workCard", num: "03", initial: "FC", name: "Ficha de trabalho", hint: "Título, índice, linhas e rodapé da ficha lateral." },
    { id: "sobre", path: "about", num: "04", initial: "SO", name: "Sobre", hint: "Texto de apresentação e princípios publicados." },
    { id: "competencias", path: "skills", num: "05", initial: "CP", name: "Competências", hint: "Grupos de competências e itens exibidos na grade." },
    { id: "experiencia", path: "experience", num: "06", initial: "EX", name: "Experiência", hint: "Vagas, períodos, locais e atividades." },
    { id: "projetos", path: "projects", num: "07", initial: "PR", name: "Projetos", hint: "Cases, tags, contexto, decisão e resultado." },
    { id: "formacao", path: "education", num: "08", initial: "FO", name: "Formação", hint: "Graduação e certificações." },
    { id: "processo", path: "process", num: "09", initial: "PS", name: "Processo", hint: "Etapas, descrições e saídas." },
    { id: "contato", path: "contact", num: "10", initial: "CT", name: "Contato", hint: "Textos do formulário, assuntos e mensagens de sucesso." },
  ];

  const login = $("#login");
  const app = $("#app");
  const sidebar = $("#sidebar");
  const scrim = $("#scrim");
  const menuButton = $("#menu-btn");
  const mainArea = $("#main-content");
  const toasts = $("#toasts");
  const dialog = $("#confirm-dialog");
  const editorContainer = $("#content-editor");

  let token = sessionStorage.getItem(TOKEN_KEY) || "";
  let editor = null;
  let pendingConfirm = null;
  let currentUser = null;

  const state = {
    view: "overview",
    activeSection: "seo",
    content: null,
    savedContent: null,
    contentUpdatedAt: null,
    messages: [],
    selectedMessageId: null,
    filter: "todas",
    query: "",
    stats: { messages: { total: 0, unread: 0 }, contentUpdatedAt: null },
    career: null,
    careerQuery: "",
    careerStatusFilter: "",
  };

  function headers(extra) {
    const result = { Accept: "application/json" };
    if (token) result.Authorization = "Bearer " + token;
    if (extra) Object.assign(result, extra);
    return result;
  }

  async function api(path, options) {
    options = options || {};
    const response = await fetch(API_BASE + path, {
      method: options.method || "GET",
      headers: headers(options.body ? { "Content-Type": "application/json" } : undefined),
      body: options.body,
    });
    if (response.status === 401) {
      signOut("Sua sessão expirou. Entre novamente.");
      throw new Error("unauthorized");
    }
    return response;
  }

  function setLoginLoading(loading) {
    const button = $("#login-button");
    const label = $("#login-button-label");
    const spinner = $("#login-spinner");
    if (button) button.disabled = loading;
    if (label) label.textContent = loading ? "Entrando…" : "Entrar";
    if (spinner) spinner.hidden = !loading;
  }

  function showError(id, message) {
    const error = document.getElementById(id + "-error");
    const input = document.getElementById(id);
    if (error) {
      error.textContent = message;
      error.hidden = false;
    }
    if (input) input.setAttribute("aria-invalid", "true");
  }

  function clearError(id) {
    const error = document.getElementById(id + "-error");
    const input = document.getElementById(id);
    if (error) {
      error.textContent = "";
      error.hidden = true;
    }
    if (input) input.removeAttribute("aria-invalid");
  }

  function toast(message, variant) {
    const node = document.createElement("div");
    node.className = "toast" + (variant === "success" ? " is-success" : "");
    node.innerHTML =
      icon(variant === "success" ? "checkCircle" : "check", 16) + "<span>" + esc(message) + "</span>";
    toasts.append(node);
    requestAnimationFrame(() => requestAnimationFrame(() => node.classList.add("is-in")));
    setTimeout(() => {
      node.classList.remove("is-in");
      setTimeout(() => node.remove(), 280);
    }, 3400);
  }

  function openConfirm(config) {
    $("#dlg-title").textContent = config.title;
    $("#dlg-text").textContent = config.text;
    const confirm = $("#dlg-confirm");
    confirm.textContent = config.confirmLabel || "Confirmar";
    confirm.className = "btn " + (config.danger === false ? "btn-primary" : "btn-danger");
    pendingConfirm = config.onConfirm;
    dialog.showModal();
    $("#dlg-cancel").focus();
  }

  function initials(name) {
    const parts = String(name || "").trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return "?";
    return ((parts[0][0] || "") + (parts[parts.length - 1][0] || "")).toUpperCase();
  }

  function toneFor(id) {
    const number = Math.abs(Number(id) || 0);
    return TONES[number % TONES.length];
  }

  function formatShortDate(value) {
    if (!value) return "";
    return new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "short" })
      .format(new Date(value))
      .replace(".", "");
  }

  function formatDateTime(value) {
    if (!value) return "—";
    return new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
  }

  function relativeTime(value) {
    if (!value) return "sem informação";
    const diff = Date.now() - new Date(value).getTime();
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1) return "agora";
    if (minutes < 60) return "há " + minutes + " min";
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return "há " + hours + " h";
    const days = Math.floor(hours / 24);
    if (days < 30) return "há " + days + (days === 1 ? " dia" : " dias");
    return formatShortDate(value);
  }

  function completionOf(value) {
    if (value == null) return 0;
    if (typeof value === "string") return value.trim() ? 100 : 0;
    if (typeof value === "number" || typeof value === "boolean") return 100;
    if (Array.isArray(value)) {
      if (!value.length) return 0;
      return Math.round(value.reduce((total, item) => total + completionOf(item), 0) / value.length);
    }
    if (typeof value === "object") {
      const keys = Object.keys(value);
      if (!keys.length) return 0;
      return Math.round(keys.reduce((total, key) => total + completionOf(value[key]), 0) / keys.length);
    }
    return 0;
  }

  function averageCompletion(content) {
    if (!content) return 0;
    const values = sections.map((section) => completionOf(content[section.path]));
    return Math.round(values.reduce((total, value) => total + value, 0) / values.length);
  }

  function updateTodayLabel() {
    const formatted = new Intl.DateTimeFormat("pt-BR", {
      weekday: "long",
      day: "numeric",
      month: "long",
      year: "numeric",
    }).format(new Date());
    const label = $("#today-label");
    if (label) label.textContent = formatted.charAt(0).toUpperCase() + formatted.slice(1);
  }

  async function handleLogin(event) {
    event.preventDefault();
    clearError("login-email");
    clearError("login-password");
    const email = $("#login-email").value.trim();
    const password = $("#login-password").value;

    if (!email) {
      showError("login-email", "Informe seu e-mail para continuar.");
      return;
    }
    if (!password) {
      showError("login-password", "Informe sua senha para continuar.");
      return;
    }

    setLoginLoading(true);
    try {
      const response = await fetch(API_BASE + "/api/admin/login", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ email: email, password: password }),
      });
      if (!response.ok) {
        showError(
          "login-password",
          response.status === 429
            ? "Muitas tentativas. Aguarde um minuto e tente novamente."
            : "E-mail ou senha inválidos.",
        );
        return;
      }
      const data = await response.json();
      token = data.token;
      sessionStorage.setItem(TOKEN_KEY, token);
      currentUser = data.user;
      $("#login-form").reset();
      await enterApp();
    } catch (error) {
      showError("login-password", "Não foi possível conectar ao servidor. Tente novamente.");
    } finally {
      setLoginLoading(false);
    }
  }

  async function enterApp() {
    login.hidden = true;
    app.hidden = false;
    document.body.classList.add("is-authed");
    go("overview");
    loadStats();
    loadCareerOps();
    loadMessages();
    loadContent();
    $("#session-detail").textContent = currentUser?.lastLoginAt
      ? "Última entrada em " + formatDateTime(currentUser.lastLoginAt)
      : "Sessão iniciada agora";
  }

  function signOut(message) {
    token = "";
    sessionStorage.removeItem(TOKEN_KEY);
    currentUser = null;
    editor = null;
    state.content = null;
    state.savedContent = null;
    state.messages = [];
    state.selectedMessageId = null;
    state.career = null;
    state.stats = { messages: { total: 0, unread: 0 }, contentUpdatedAt: null };
    app.hidden = true;
    login.hidden = false;
    document.body.classList.remove("is-authed");
    closeSidebar();
    if (editorContainer) editorContainer.innerHTML = '<p class="editor-loading">Carregando conteúdo…</p>';
    $("#login-form").reset();
    $("#msg-list").innerHTML = "";
    $("#msg-detail").innerHTML = "";
    $("#recent-list").innerHTML = "";
    $("#health-list").innerHTML = "";
    $("#sec-nav").innerHTML = "";
    if (message) showError("login-password", message);
  }

  function go(view) {
    state.view = view;
    $$(".view").forEach((node) => {
      node.hidden = node.id !== "view-" + view;
    });
    $$(".nav-item").forEach((button) => {
      const active = button.dataset.view === view;
      button.classList.toggle("is-active", active);
      if (active) button.setAttribute("aria-current", "page");
      else button.removeAttribute("aria-current");
    });
    closeSidebar();
    mainArea.scrollTop = 0;
    if (view === "overview") loadStats();
    if (view === "career") loadCareerOps();
    if (view === "messages") loadMessages();
    if (view === "content" && !state.content) loadContent();
    const heading = $("#view-" + view + " h1");
    if (heading) heading.focus({ preventScroll: true });
  }

  function openSidebar() {
    if (!window.matchMedia("(max-width: 900px)").matches) return;
    sidebar.classList.add("is-open");
    scrim.hidden = false;
    menuButton.setAttribute("aria-expanded", "true");
    document.body.style.overflow = "hidden";
    setTimeout(() => $("#side-close").focus(), 60);
  }

  function closeSidebar() {
    sidebar.classList.remove("is-open");
    scrim.hidden = true;
    menuButton.setAttribute("aria-expanded", "false");
    document.body.style.overflow = "";
  }

  async function loadStats() {
    try {
      const response = await api("/api/admin/stats");
      if (!response.ok) return;
      state.stats = await response.json();
      refreshOverview();
    } catch (error) {
      /* sessão expirada já tratada */
    }
  }

  function refreshOverview() {
    const unread = state.stats.messages?.unread ?? 0;
    $("#stat-total").textContent = state.stats.messages?.total ?? 0;
    $("#stat-unread").textContent = unread;
    $("#stat-completeness").innerHTML = averageCompletion(state.content) + "<small>%</small>";
    $("#stat-updated-foot").textContent =
      "revisão " + relativeTime(state.stats.contentUpdatedAt || state.contentUpdatedAt);
    const badge = $("#nav-unread");
    if (unread > 0) {
      badge.hidden = false;
      badge.textContent = unread;
      badge.setAttribute("aria-label", plural(unread, "não lida", "não lidas"));
    } else {
      badge.hidden = true;
    }
    $("#quick-unread").textContent =
      unread === 0 ? "nenhuma não lida" : plural(unread, "não lida", "não lidas");

    const recent = state.messages.slice(0, 4);
    $("#recent-list").innerHTML = recent.length
      ? recent.map(recentRowHTML).join("")
      : '<p class="recent-empty">Nenhuma mensagem na caixa de entrada.</p>';

    renderHealth();
    if (state.content) renderSectionNav();
  }

  function recentRowHTML(message) {
    const key = STATUS_KEY[message.status] || "nova";
    return (
      '<button type="button" class="recent-row" data-open-message="' +
      message.id +
      '">' +
      '<span class="avatar tone-' +
      toneFor(message.id) +
      '" aria-hidden="true">' +
      esc(initials(message.name)) +
      "</span>" +
      '<span class="recent-main"><span class="recent-name"><span>' +
      esc(message.name) +
      '</span><span class="date">' +
      esc(formatShortDate(message.createdAt)) +
      '</span></span><span class="recent-subject">' +
      esc(message.subject) +
      "</span></span>" +
      '<span class="pill status-' +
      key +
      '">' +
      STATUS_LABEL[key] +
      "</span>" +
      "</button>"
    );
  }

  function renderHealth() {
    const ids = ["seo", "perfil", "experiencia", "projetos"];
    $("#health-list").innerHTML = ids
      .map((id) => {
        const section = sections.find((item) => item.id === id);
        const percent = state.content ? completionOf(state.content[section.path]) : 0;
        return (
          "<li>" +
          '<div class="health-top"><span class="health-name">' +
          esc(section.name) +
          '</span><span class="health-pct">' +
          percent +
          "%</span></div>" +
          '<div class="bar" role="progressbar" aria-label="' +
          esc(section.name) +
          '" aria-valuemin="0" aria-valuemax="100" aria-valuenow="' +
          percent +
          '"><i style="width:' +
          percent +
          '%"></i></div>' +
          '<p class="health-update">Atualizado ' +
          esc(relativeTime(state.stats.contentUpdatedAt || state.contentUpdatedAt)) +
          "</p></li>"
        );
      })
      .join("");
  }

  function renderSectionNav() {
    if (!state.content) return;
    $("#sec-nav").innerHTML = sections
      .map((section) => {
        const percent = completionOf(state.content[section.path]);
        const dotClass = percent >= 100 ? "done" : percent >= 60 ? "part" : "low";
        const active = section.id === state.activeSection;
        return (
          '<button type="button" class="sec-link' +
          (active ? " is-active" : "") +
          '" data-section="' +
          section.id +
          '"' +
          (active ? ' aria-current="true"' : "") +
          ">" +
          '<span class="sec-initial" aria-hidden="true">' +
          section.initial +
          "</span>" +
          '<span class="sec-name">' +
          esc(section.name) +
          "</span>" +
          '<span class="sec-pct">' +
          percent +
          "%</span>" +
          '<span class="dot ' +
          dotClass +
          '" aria-hidden="true"></span>' +
          "</button>"
        );
      })
      .join("");
  }

  function selectSection(id, scroll) {
    const section = sections.find((item) => item.id === id) || sections[0];
    state.activeSection = section.id;
    renderSectionNav();
    $("#editor-num").textContent = "Seção " + section.num;
    $("#editor-title").textContent = section.name;
    $("#editor-hint").textContent = section.hint;
    const percent = state.content ? completionOf(state.content[section.path]) : 0;
    $("#editor-pct").textContent = percent + "% completo";
    $("#editor-dot").className = "dot " + (percent >= 100 ? "done" : percent >= 60 ? "part" : "low");
    if (scroll !== false && editor) {
      const target = editor.getEditor("root." + section.path);
      if (target && target.container) target.container.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }

  async function loadContent() {
    if (!editorContainer) return;
    if (!state.content) editorContainer.innerHTML = '<p class="editor-loading">Carregando conteúdo…</p>';
    try {
      const response = await api("/api/admin/content");
      if (!response.ok) {
        editorContainer.innerHTML = '<p class="editor-loading">Não foi possível carregar o conteúdo.</p>';
        return;
      }
      const data = await response.json();
      state.content = data.content;
      state.savedContent = clone(data.content);
      state.contentUpdatedAt = data.updatedAt;
      $("#editor-updated").textContent = formatDateTime(data.updatedAt);
      await initEditor();
      renderSectionNav();
      selectSection(state.activeSection, false);
      refreshOverview();
      updateDirtyState();
    } catch (error) {
      editorContainer.innerHTML = '<p class="editor-loading">Falha ao carregar o conteúdo.</p>';
    }
  }

  async function initEditor() {
    if (!window.JSONEditor || !window.ABSON_ADMIN_SCHEMA) {
      editorContainer.innerHTML = '<p class="editor-loading">Editor de conteúdo não carregou. Recarregue a página.</p>';
      return;
    }
    if (!editor) {
      editorContainer.innerHTML = "";
      editor = new window.JSONEditor(editorContainer, {
        schema: window.ABSON_ADMIN_SCHEMA,
        theme: "html",
        disable_collapse: false,
        disable_edit_json: true,
        disable_properties: true,
        disable_array_reorder: false,
        required_by_default: false,
        show_errors: "always",
      });
      await editor.promise;
      editor.on("change", updateDirtyState);
    }
    editor.setValue(state.content);
    state.savedContent = clone(editor.getValue());
    updateDirtyState();
  }

  function updateDirtyState() {
    if (!editor || !state.savedContent) return;
    const dirty = JSON.stringify(editor.getValue()) !== JSON.stringify(state.savedContent);
    $("#dirty-flag").hidden = !dirty;
    $("#save-btn").disabled = !dirty;
  }

  async function saveContent() {
    if (!editor) return;
    const value = editor.getValue();
    $("#save-btn").disabled = true;
    try {
      const response = await api("/api/admin/content", {
        method: "PUT",
        body: JSON.stringify({ content: value }),
      });
      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        toast(error.error || "Não foi possível salvar o conteúdo.");
        return;
      }
      const data = await response.json();
      state.content = value;
      state.savedContent = clone(value);
      state.contentUpdatedAt = data.updatedAt;
      $("#editor-updated").textContent = formatDateTime(data.updatedAt);
      updateDirtyState();
      renderSectionNav();
      selectSection(state.activeSection, false);
      loadStats();
      toast("Conteúdo salvo com sucesso.", "success");
    } catch (error) {
      toast("Falha ao salvar. Tente novamente.");
    } finally {
      updateDirtyState();
    }
  }

  function reloadContent() {
    if (!editor || !state.savedContent) return;
    editor.setValue(state.savedContent);
    updateDirtyState();
    toast("Conteúdo recarregado do servidor.");
  }

  async function loadMessages() {
    try {
      const response = await api("/api/admin/messages?page=1&pageSize=100");
      if (!response.ok) return;
      const data = await response.json();
      state.messages = data.items;
      if (!state.messages.some((message) => message.id === state.selectedMessageId)) {
        state.selectedMessageId = state.messages[0]?.id ?? null;
      }
      renderMessages();
      refreshOverview();
    } catch (error) {
      /* sessão expirada já tratada */
    }
  }

  function filteredMessages() {
    const query = norm(state.query.trim());
    return state.messages.filter((message) => {
      const key = STATUS_KEY[message.status] || "nova";
      if (state.filter !== "todas" && key !== state.filter) return false;
      if (!query) return true;
      return norm([message.name, message.email, message.subject, message.message].join(" ")).includes(query);
    });
  }

  function updateFilterCounts() {
    const counts = { todas: state.messages.length, nova: 0, lida: 0, respondida: 0, arquivada: 0 };
    state.messages.forEach((message) => {
      const key = STATUS_KEY[message.status] || "nova";
      counts[key] += 1;
    });
    $$("#filter-row [data-count]").forEach((node) => {
      node.textContent = counts[node.dataset.count] || 0;
    });
  }

  function messageRowHTML(message) {
    const key = STATUS_KEY[message.status] || "nova";
    const active = message.id === state.selectedMessageId;
    const preview = String(message.message || "").replace(/\s+/g, " ").slice(0, 110);
    return (
      '<button type="button" class="msg-row' +
      (active ? " is-active" : "") +
      (message.status === "new" ? " is-unread" : "") +
      '" data-message="' +
      message.id +
      '"' +
      (active ? ' aria-current="true"' : "") +
      ">" +
      '<span class="avatar tone-' +
      toneFor(message.id) +
      '" aria-hidden="true">' +
      esc(initials(message.name)) +
      "</span>" +
      '<span class="msg-main"><span class="msg-top"><span class="msg-name">' +
      esc(message.name) +
      '</span><span class="msg-date">' +
      esc(formatShortDate(message.createdAt)) +
      '</span></span><span class="msg-subject">' +
      esc(message.subject) +
      '</span><span class="msg-preview">' +
      esc(preview) +
      "</span></span>" +
      '<span class="pill status-' +
      key +
      '">' +
      STATUS_LABEL[key] +
      "</span></button>"
    );
  }

  function renderMessages() {
    const list = filteredMessages();
    const unread = state.stats.messages?.unread ?? state.messages.filter((m) => m.status === "new").length;
    $("#messages-meta").textContent =
      plural(state.messages.length, "mensagem", "mensagens") + " · " + plural(unread, "não lida", "não lidas");
    $$("#filter-row .chip").forEach((chip) => {
      chip.classList.toggle("is-active", chip.dataset.filter === state.filter);
    });
    updateFilterCounts();

    const listNode = $("#msg-list");
    const emptyNode = $("#msg-empty");
    if (!list.length) {
      listNode.innerHTML = "";
      listNode.hidden = true;
      emptyNode.hidden = false;
      state.selectedMessageId = null;
      renderDetail(null);
      return;
    }
    listNode.hidden = false;
    emptyNode.hidden = true;
    if (!list.some((message) => message.id === state.selectedMessageId)) {
      state.selectedMessageId = list[0].id;
    }
    listNode.innerHTML = list.map(messageRowHTML).join("");
    renderDetail(state.messages.find((message) => message.id === state.selectedMessageId) || null);
  }

  function detailHTML(message) {
    if (!message) {
      return '<div class="detail-empty">' + icon("mail", 28) + "<p>Selecione uma mensagem para ler.</p></div>";
    }
    const key = STATUS_KEY[message.status] || "nova";
    const readLabel = message.status === "new" ? "Marcar como lida" : "Marcar como nova";
    const readIcon = message.status === "new" ? "checkCircle" : "mail";
    const archived = message.status === "archived";
    const answered = message.status === "replied";
    const paragraphs = String(message.message || "")
      .split(/\n+/)
      .filter((paragraph) => paragraph.trim())
      .map((paragraph) => "<p>" + esc(paragraph).replace(/\n/g, "<br>") + "</p>")
      .join("");
    const replySubject = encodeURIComponent("Re: " + message.subject);

    return (
      '<div class="detail-top"><div class="detail-from">' +
      '<span class="avatar tone-' +
      toneFor(message.id) +
      '" aria-hidden="true">' +
      esc(initials(message.name)) +
      "</span>" +
      '<div><p class="detail-name">' +
      esc(message.name) +
      '</p><p class="detail-email mono">' +
      esc(message.email) +
      "</p></div></div>" +
      '<span class="pill status-' +
      key +
      '">' +
      STATUS_LABEL[key] +
      "</span></div>" +
      '<h2 class="detail-subject" tabindex="-1">' +
      esc(message.subject) +
      "</h2>" +
      '<div class="detail-body">' +
      paragraphs +
      "</div>" +
      '<dl class="detail-meta"><div><dt>Origem</dt><dd>Formulário do site</dd></div><div><dt>Assunto</dt><dd>' +
      esc(message.subject) +
      "</dd></div><div><dt>Recebida</dt><dd>" +
      esc(formatDateTime(message.createdAt)) +
      "</dd></div></dl>" +
      '<div class="detail-actions">' +
      '<button class="btn btn-ghost" type="button" data-action="toggle-read">' +
      icon(readIcon, 15) +
      " " +
      readLabel +
      "</button>" +
      '<button class="btn btn-ghost" type="button" data-action="answered"' +
      (answered ? " disabled" : "") +
      ">" +
      icon("checkCircle", 15) +
      " Marcar respondida</button>" +
      '<button class="btn btn-ghost" type="button" data-action="archive">' +
      icon("archive", 15) +
      " " +
      (archived ? "Desarquivar" : "Arquivar") +
      "</button>" +
      '<button class="btn btn-ghost danger" type="button" data-action="delete">' +
      icon("trash", 15) +
      " Excluir</button></div>" +
      '<div class="reply"><label>Responder por e-mail</label>' +
      '<p class="help">Abrir seu cliente de e-mail para responder a ' +
      esc(message.name) +
      ".</p>" +
      '<div class="reply-foot"><a class="btn btn-primary" href="mailto:' +
      esc(message.email) +
      "?subject=" +
      replySubject +
      '">' +
      icon("send", 15) +
      " Abrir e-mail</a></div></div>"
    );
  }

  function renderDetail(message) {
    $("#msg-detail").innerHTML = detailHTML(message);
  }

  function selectMessage(id) {
    state.selectedMessageId = Number(id);
    renderMessages();
    if (window.matchMedia("(max-width: 900px)").matches) {
      $("#inbox").classList.add("is-detail");
      const heading = $(".detail-subject");
      if (heading) {
        heading.setAttribute("tabindex", "-1");
        heading.focus({ preventScroll: true });
      }
    }
  }

  async function setMessageStatus(id, status, successMessage) {
    const response = await api("/api/admin/messages/" + id, {
      method: "PATCH",
      body: JSON.stringify({ status: status }),
    });
    if (response.ok) {
      await loadMessages();
      await loadStats();
      toast(successMessage, "success");
    }
  }

  async function deleteMessage(id) {
    const response = await api("/api/admin/messages/" + id, { method: "DELETE" });
    if (response.ok) {
      state.selectedMessageId = null;
      await loadMessages();
      await loadStats();
      toast("Mensagem excluída.", "success");
    }
  }

  const passwordForm = $("#pw-form");
  const passwordCurrent = $("#pw-current");
  const passwordNew = $("#pw-new");
  const passwordConfirm = $("#pw-confirm");
  const touched = { current: false, next: false, confirm: false };

  function passwordRules(value) {
    return {
      length: value.length >= 10,
      letter: /[A-Za-zÀ-ÿ]/.test(value),
      number: /\d/.test(value),
      symbol: /[^A-Za-zÀ-ÿ0-9\s]/.test(value),
    };
  }

  function passwordIsValid() {
    const rules = passwordRules(passwordNew.value);
    return (
      Object.values(rules).every(Boolean) &&
      passwordCurrent.value.length > 0 &&
      passwordConfirm.value.length > 0 &&
      passwordConfirm.value === passwordNew.value &&
      passwordNew.value !== passwordCurrent.value
    );
  }

  function validatePasswordField(field, force) {
    if (field === "current") {
      if (!passwordCurrent.value) {
        if (force || touched.current) showError("pw-current", "Informe sua senha atual para continuar.");
        return false;
      }
      clearError("pw-current");
      return true;
    }
    if (field === "next") {
      const value = passwordNew.value;
      const rules = passwordRules(value);
      if (!value) {
        if (force || touched.next) showError("pw-new", "Crie uma nova senha com pelo menos 10 caracteres.");
        return false;
      }
      if (!Object.values(rules).every(Boolean)) {
        if (force || touched.next) showError("pw-new", "Use pelo menos 10 caracteres, uma letra, um número e um símbolo.");
        return false;
      }
      if (value === passwordCurrent.value) {
        if (force || touched.next) showError("pw-new", "A nova senha precisa ser diferente da senha atual.");
        return false;
      }
      clearError("pw-new");
      return true;
    }
    if (!passwordConfirm.value) {
      if (force || touched.confirm) showError("pw-confirm", "Repita a nova senha para confirmar.");
      return false;
    }
    if (passwordConfirm.value !== passwordNew.value) {
      if (force || touched.confirm) showError("pw-confirm", "As senhas não coincidem. Revise os dois campos.");
      return false;
    }
    clearError("pw-confirm");
    return true;
  }

  function updatePasswordUI() {
    const rules = passwordRules(passwordNew.value);
    let score = 0;
    Object.keys(rules).forEach((rule) => {
      const item = $('#pw-hints [data-rule="' + rule + '"]');
      if (item) item.classList.toggle("is-ok", rules[rule]);
      if (rules[rule]) score += 1;
    });
    const strength = $("#strength");
    if (strength) strength.dataset.score = String(score);
    const labels = ["Força da senha", "Muito fraca", "Fraca", "Razoável", "Boa", "Forte"];
    const label = $("#strength-label");
    if (label) label.textContent = labels[score];
    const submit = $("#pw-submit");
    if (submit) submit.disabled = !passwordIsValid();
  }

  async function changePassword(event) {
    event.preventDefault();
    const okCurrent = validatePasswordField("current", true);
    const okNext = validatePasswordField("next", true);
    const okConfirm = validatePasswordField("confirm", true);
    if (!okCurrent || !okNext || !okConfirm) {
      const invalid = $('[aria-invalid="true"]', passwordForm);
      if (invalid) invalid.focus();
      return;
    }

    const submit = $("#pw-submit");
    submit.disabled = true;
    submit.textContent = "Alterando…";
    try {
      const response = await api("/api/admin/change-password", {
        method: "POST",
        body: JSON.stringify({
          currentPassword: passwordCurrent.value,
          newPassword: passwordNew.value,
        }),
      });
      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        showError("pw-current", error.error || "Não foi possível alterar a senha.");
        submit.textContent = "Alterar senha";
        submit.disabled = false;
        return;
      }
      passwordForm.reset();
      touched.current = false;
      touched.next = false;
      touched.confirm = false;
      clearError("pw-current");
      clearError("pw-new");
      clearError("pw-confirm");
      updatePasswordUI();
      const status = $("#pw-status");
      if (status) status.hidden = false;
      $("#pw-status-text").textContent = "Senha alterada agora. Use a nova senha no próximo acesso.";
      toast("Senha alterada com sucesso.", "success");
      $("#pw-current").focus();
    } catch (error) {
      showError("pw-current", "Falha ao alterar a senha. Tente novamente.");
    } finally {
      submit.textContent = "Alterar senha";
      updatePasswordUI();
    }
  }

  async function loadCareerOps() {
    try {
      const response = await api("/api/admin/career-ops");
      if (!response.ok) return;
      state.career = await response.json();
      renderCareerOps();
    } catch (error) {
      /* sessão expirada já tratada */
    }
  }

  function careerStatusPill(status) {
    const key = String(status || "neutral").toLowerCase();
    const label = CAREER_STATUS_LABELS[status] || status || "—";
    return '<span class="pill career-status is-' + esc(key) + '">' + esc(label) + "</span>";
  }

  function careerStatusOptions(selected) {
    return CAREER_STATUSES.map((status) => {
      const isSelected = String(status) === String(selected || "");
      return (
        '<option value="' + esc(status) + '"' + (isSelected ? " selected" : "") + ">" +
        esc(CAREER_STATUS_LABELS[status] || status) +
        "</option>"
      );
    }).join("");
  }

  function shortUrl(value) {
    try {
      const parsed = new URL(value);
      return parsed.hostname.replace(/^www\./, "") + (parsed.pathname !== "/" ? parsed.pathname.slice(0, 28) : "");
    } catch (error) {
      return String(value || "");
    }
  }

  function renderCareerOps() {
    const data = state.career || {};
    const snapshot = data.snapshot || null;
    const totals = snapshot?.totals || {};
    const byStatus = totals.byStatus || {};
    const applications = Array.isArray(snapshot?.applications) ? snapshot.applications : [];
    const pipeline = Array.isArray(snapshot?.pipeline) ? snapshot.pipeline : [];
    const actions = Array.isArray(data.actions) ? data.actions : [];

    const updatedAt = data.updatedAt || null;
    const ageMinutes = updatedAt ? Math.max(0, Math.round((Date.now() - new Date(updatedAt).getTime()) / 60000)) : null;
    const stale = !updatedAt || ageMinutes > 20;
    const syncDot = $("#career-sync-dot");
    if (syncDot) syncDot.className = "dot " + (stale ? "warn" : "ok");
    $("#career-sync-label").textContent = updatedAt ? "Sincronizado " + relativeTime(updatedAt) : "Aguardando sync";

    const pending = data.pendingActions ?? actions.filter((action) => action.status === "pending").length;
    const navBadge = $("#nav-career-pending");
    if (navBadge) {
      navBadge.hidden = pending <= 0;
      navBadge.textContent = pending;
      navBadge.setAttribute("aria-label", plural(pending, "ação pendente", "ações pendentes"));
    }
    $("#career-pending-count").textContent = pending ? plural(pending, "pendente", "pendentes") : "sem pendências";

    const activeStatuses = ["Applied", "Responded", "Assessment", "Interview"];
    const active = activeStatuses.reduce((total, status) => total + (byStatus[status] || 0), 0);
    const offers = (byStatus.Offer || 0) + (byStatus.Hired || 0);

    $("#career-stat-apps").textContent = totals.applications ?? applications.length;
    $("#career-stat-active").textContent = active;
    $("#career-stat-offers").textContent = offers;
    $("#career-stat-score").innerHTML =
      totals.averageScore == null ? "—" : Number(totals.averageScore).toFixed(1) + "<small>/5</small>";

    renderCareerFunnel(byStatus);
    renderCareerTable(applications);
    renderCareerPipeline(pipeline);
    renderCareerActions(actions);
  }

  function renderCareerFunnel(byStatus) {
    const container = $("#career-funnel");
    if (!container) return;
    const total = CAREER_STATUSES.reduce((sum, status) => sum + (byStatus[status] || 0), 0);
    if (!total) {
      container.innerHTML = '<p class="career-empty">Nenhuma candidatura no tracker ainda.</p>';
      return;
    }
    const max = Math.max(1, ...CAREER_STATUSES.map((status) => byStatus[status] || 0));
    container.innerHTML = CAREER_STATUSES.map((status) => {
      const count = byStatus[status] || 0;
      const width = Math.round((count / max) * 100);
      return (
        '<div class="career-funnel-row">' +
        '<span class="career-funnel-label">' + esc(CAREER_STATUS_LABELS[status] || status) + "</span>" +
        '<div class="career-funnel-bar"><i class="is-' + esc(status.toLowerCase()) + '" style="width:' + width + '%"></i></div>' +
        '<strong class="mono">' + count + "</strong>" +
        "</div>"
      );
    }).join("");
  }

  function filteredCareerApplications(applications) {
    const query = norm(state.careerQuery || "");
    return applications.filter((app) => {
      if (state.careerStatusFilter && app.status !== state.careerStatusFilter) return false;
      if (!query) return true;
      return norm((app.company || "") + " " + (app.role || "")).includes(query);
    });
  }

  function renderCareerTable(applications) {
    const body = $("#career-table-body");
    if (!body) return;

    const filter = $("#career-filter");
    if (filter && filter.options.length === 1) {
      filter.insertAdjacentHTML(
        "beforeend",
        CAREER_STATUSES.map(
          (status) => '<option value="' + esc(status) + '">' + esc(CAREER_STATUS_LABELS[status] || status) + "</option>",
        ).join(""),
      );
    }

    const rows = filteredCareerApplications(applications);
    if (!rows.length) {
      body.innerHTML =
        '<tr><td colspan="7" class="career-empty">' +
        (applications.length ? "Nenhuma candidatura corresponde ao filtro." : "Nenhuma candidatura no tracker ainda.") +
        "</td></tr>";
      return;
    }

    body.innerHTML = rows.map((app) => {
      const num = app.num == null ? "" : String(app.num);
      const company = app.company || "—";
      const role = app.role || "—";
      const score = app.score == null ? "—" : Number(app.score).toFixed(1) + "/5";
      return (
        "<tr>" +
        '<td class="mono">' + esc(num || "—") + "</td>" +
        "<td><strong>" + esc(company) + "</strong>" +
        (app.url ? ' <a class="career-link" href="' + esc(app.url) + '" target="_blank" rel="noopener">abrir</a>' : "") +
        "</td>" +
        "<td>" + esc(role) + "</td>" +
        '<td class="mono">' + esc(score) + "</td>" +
        "<td>" + careerStatusPill(app.status) + "</td>" +
        '<td class="mono">' + esc(app.date || "—") + "</td>" +
        '<td class="career-row-action">' +
        '<select class="input career-status-select" data-career-status="' + esc(num) + '" aria-label="Novo status">' +
        careerStatusOptions(app.status) +
        "</select>" +
        '<button class="btn btn-ghost btn-sm" type="button" data-career-save="' + esc(num) +
        '" data-company="' + esc(company) + '">Aplicar</button>' +
        "</td>" +
        "</tr>"
      );
    }).join("");
  }

  function renderCareerPipeline(pipeline) {
    const container = $("#career-pipeline");
    if (!container) return;
    $("#career-pipeline-count").textContent = pipeline.length;
    if (!pipeline.length) {
      container.innerHTML = '<p class="career-empty">Nenhuma vaga pendente no pipeline.</p>';
      return;
    }
    container.innerHTML = pipeline.slice(0, 20).map((item) => {
      return (
        '<div class="career-item">' +
        '<div class="career-item-copy"><strong>' + esc(item.company || "Vaga") + "</strong>" +
        (item.role ? "<span>" + esc(item.role) + "</span>" : "") +
        "</div>" +
        '<a class="career-link" href="' + esc(item.url) + '" target="_blank" rel="noopener">' +
        esc(shortUrl(item.url)) +
        "</a>" +
        "</div>"
      );
    }).join("");
  }

  function renderCareerActions(actions) {
    const container = $("#career-actions");
    if (!container) return;
    if (!actions.length) {
      container.innerHTML = '<p class="career-empty">Nenhuma ação ainda.</p>';
      return;
    }
    container.innerHTML = actions.slice(0, 12).map((action) => {
      const tone = action.status === "applied" ? "tone-ok" : action.status === "failed" ? "tone-danger" : "tone-warn";
      const label = CAREER_ACTION_LABELS[action.type] || action.type;
      const detail = action.result || (action.status === "pending" ? "aguardando o Mac" : "");
      return (
        '<div class="career-item">' +
        '<div class="career-item-copy"><strong>' + esc(label) + "</strong>" +
        (detail ? "<span>" + esc(detail) + "</span>" : "") +
        '<span class="mono">' + esc(formatDateTime(action.createdAt)) + "</span>" +
        "</div>" +
        '<span class="pill ' + tone + '">' + esc(action.status) + "</span>" +
        "</div>"
      );
    }).join("");
  }

  async function createCareerAction(type, payload) {
    try {
      const response = await api("/api/admin/career-ops/actions", {
        method: "POST",
        body: JSON.stringify({ type, payload }),
      });
      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        toast(error.error || "Não foi possível enfileirar a ação.");
        return false;
      }
      toast("Ação enfileirada. O Mac aplica no próximo sync.", "success");
      await loadCareerOps();
      return true;
    } catch (error) {
      toast("Falha de conexão com a API.");
      return false;
    }
  }

  async function handleCareerAdd(event) {
    event.preventDefault();
    const urlInput = $("#career-url");
    const url = urlInput.value.trim();
    if (!url) {
      toast("Informe a URL da vaga.");
      urlInput.focus();
      return;
    }
    try {
      const parsed = new URL(url);
      if (parsed.protocol !== "http:" && parsed.protocol !== "https:") throw new Error("invalid");
    } catch (error) {
      toast("Informe uma URL http(s) válida.");
      urlInput.focus();
      return;
    }

    const button = $("#career-add-submit");
    button.disabled = true;
    const ok = await createCareerAction("add_pipeline_url", {
      url,
      company: $("#career-company").value.trim() || null,
      role: $("#career-role").value.trim() || null,
    });
    button.disabled = false;
    if (ok) $("#career-add-form").reset();
  }

  async function saveCareerStatus(button) {
    const num = button.dataset.careerSave || "";
    const select = document.querySelector('[data-career-status="' + (window.CSS && CSS.escape ? CSS.escape(num) : num) + '"]');
    if (!select) return;
    button.disabled = true;
    await createCareerAction("set_status", {
      report: num,
      company: button.dataset.company || "",
      status: select.value,
    });
    button.disabled = false;
  }

  function bindEvents() {
    updateTodayLabel();
    $("#login-form").addEventListener("submit", handleLogin);
    $("#logout").addEventListener("click", () => signOut(""));
    $("#menu-btn").addEventListener("click", openSidebar);
    $("#side-close").addEventListener("click", closeSidebar);
    $("#scrim").addEventListener("click", closeSidebar);

    document.addEventListener("click", (event) => {
      const goButton = event.target.closest("[data-go]");
      if (goButton) {
        go(goButton.dataset.go);
        return;
      }
      const navButton = event.target.closest(".nav-item[data-view]");
      if (navButton) {
        go(navButton.dataset.view);
        return;
      }
      const recent = event.target.closest("[data-open-message]");
      if (recent) {
        selectMessage(recent.dataset.openMessage);
        go("messages");
        return;
      }
      const sectionButton = event.target.closest("[data-section]");
      if (sectionButton) {
        selectSection(sectionButton.dataset.section, true);
        return;
      }
      const filter = event.target.closest("[data-filter]");
      if (filter) {
        state.filter = filter.dataset.filter;
        renderMessages();
        return;
      }
      const messageRow = event.target.closest("[data-message]");
      if (messageRow) {
        selectMessage(messageRow.dataset.message);
        return;
      }
      const careerSave = event.target.closest("[data-career-save]");
      if (careerSave) {
        saveCareerStatus(careerSave);
        return;
      }
      const action = event.target.closest("[data-action]");
      if (action) {
        const message = state.messages.find((item) => item.id === state.selectedMessageId);
        if (!message) return;
        if (action.dataset.action === "toggle-read") {
          setMessageStatus(
            message.id,
            message.status === "new" ? "read" : "new",
            message.status === "new" ? "Mensagem marcada como lida." : "Mensagem marcada como nova.",
          );
        } else if (action.dataset.action === "answered") {
          setMessageStatus(message.id, "replied", "Mensagem marcada como respondida.");
        } else if (action.dataset.action === "archive") {
          setMessageStatus(
            message.id,
            message.status === "archived" ? "read" : "archived",
            message.status === "archived" ? "Mensagem desarquivada." : "Mensagem arquivada.",
          );
        } else if (action.dataset.action === "delete") {
          openConfirm({
            title: "Excluir mensagem?",
            text: "Esta ação não pode ser desfeita.",
            confirmLabel: "Excluir",
            danger: true,
            onConfirm: () => deleteMessage(message.id),
          });
        }
      }
    });

    $("#msg-search").addEventListener("input", (event) => {
      state.query = event.target.value;
      renderMessages();
    });
    $("#clear-filters").addEventListener("click", () => {
      state.filter = "todas";
      state.query = "";
      $("#msg-search").value = "";
      renderMessages();
    });
    $("#msg-back").addEventListener("click", () => $("#inbox").classList.remove("is-detail"));

    $("#career-refresh").addEventListener("click", loadCareerOps);
    $("#career-add-form").addEventListener("submit", handleCareerAdd);
    $("#career-search").addEventListener("input", (event) => {
      state.careerQuery = event.target.value;
      renderCareerTable(state.career?.snapshot?.applications || []);
    });
    $("#career-filter").addEventListener("change", (event) => {
      state.careerStatusFilter = event.target.value;
      renderCareerTable(state.career?.snapshot?.applications || []);
    });

    $("#save-btn").addEventListener("click", saveContent);
    $("#reload-btn").addEventListener("click", reloadContent);

    passwordCurrent.addEventListener("input", () => {
      touched.current = true;
      validatePasswordField("current");
      if (touched.next) validatePasswordField("next");
      updatePasswordUI();
    });
    passwordNew.addEventListener("input", () => {
      touched.next = true;
      validatePasswordField("next");
      if (touched.confirm) validatePasswordField("confirm");
      updatePasswordUI();
    });
    passwordConfirm.addEventListener("input", () => {
      touched.confirm = true;
      validatePasswordField("confirm");
      updatePasswordUI();
    });
    passwordCurrent.addEventListener("blur", () => {
      touched.current = true;
      validatePasswordField("current", true);
    });
    passwordNew.addEventListener("blur", () => {
      touched.next = true;
      validatePasswordField("next", true);
    });
    passwordConfirm.addEventListener("blur", () => {
      touched.confirm = true;
      validatePasswordField("confirm", true);
    });
    passwordForm.addEventListener("submit", changePassword);

    $("#dlg-cancel").addEventListener("click", () => dialog.close());
    $("#dlg-confirm").addEventListener("click", async () => {
      const action = pendingConfirm;
      pendingConfirm = null;
      dialog.close();
      if (action) await action();
    });
    dialog.addEventListener("close", () => {
      pendingConfirm = null;
    });
    dialog.addEventListener("click", (event) => {
      if (event.target === dialog) dialog.close();
    });

    window.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && sidebar.classList.contains("is-open")) closeSidebar();
    });
    window.addEventListener("resize", () => {
      if (window.matchMedia("(min-width: 901px)").matches && sidebar.classList.contains("is-open")) closeSidebar();
    });
  }

  async function init() {
    bindEvents();
    updatePasswordUI();
    if (token) {
      try {
        const response = await api("/api/admin/me");
        if (response.ok) {
          currentUser = await response.json();
          await enterApp();
          return;
        }
      } catch (error) {
        /* cai no login */
      }
    }
    signOut("");
  }

  init();
})();
