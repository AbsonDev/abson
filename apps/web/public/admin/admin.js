(function () {
  "use strict";

  var API_BASE = (document.body.getAttribute("data-api-base") || "https://api.abson.tech").replace(/\/$/, "");
  var TOKEN_KEY = "abson_admin_token";
  var token = sessionStorage.getItem(TOKEN_KEY) || "";

  var loginView = document.getElementById("login-view");
  var dashboardView = document.getElementById("dashboard-view");
  var loginForm = document.getElementById("login-form");
  var loginError = document.getElementById("login-error");
  var logoutButton = document.getElementById("logout");

  var editorContainer = document.getElementById("content-editor");
  var contentStatus = document.getElementById("content-status");
  var saveButton = document.getElementById("save-content");
  var reloadButton = document.getElementById("reload-content");

  var statTotal = document.getElementById("stat-total");
  var statUnread = document.getElementById("stat-unread");
  var statContent = document.getElementById("stat-content");

  var messagesBody = document.getElementById("messages-body");
  var messagesStatus = document.getElementById("messages-status");
  var messageFilter = document.getElementById("message-filter");
  var refreshMessages = document.getElementById("refresh-messages");

  var changePasswordForm = document.getElementById("change-password-form");
  var passwordStatus = document.getElementById("password-status");

  var editor = null;

  function field(title) {
    return { type: "string", title: title };
  }

  function textarea(title) {
    return { type: "string", title: title, format: "textarea" };
  }

  function object(title, properties) {
    return { type: "object", title: title, properties: properties };
  }

  function array(title, items) {
    return { type: "array", title: title, items: items };
  }

  var schema = object("Conteúdo do site", {
    seo: object("SEO", {
      title: field("Título da página"),
      description: textarea("Descrição (meta)"),
      canonical: field("URL canônica"),
      ogImage: field("Imagem Open Graph (URL)")
    }),
    theme: object("Tema", {
      default: { type: "string", title: "Tema padrão", enum: ["dark", "light"] }
    }),
    profile: object("Perfil", {
      name: field("Nome"),
      brand: field("Marca"),
      brandMark: field("Iniciais"),
      role: field("Cargo"),
      headline: object("Título principal", {
        before: field("Antes do destaque"),
        emphasis: field("Palavra em destaque"),
        after: field("Depois do destaque")
      }),
      lead: textarea("Texto de apresentação"),
      location: field("Localização"),
      availability: field("Disponibilidade"),
      email: field("E-mail"),
      linkedin: field("LinkedIn (URL)"),
      linkedinLabel: field("LinkedIn (texto)"),
      cvUrl: field("Currículo (URL)"),
      footerNote: field("Rodapé"),
      heroMeta: array("Itens do hero", { type: "string" })
    }),
    workCard: object("Ficha de trabalho", {
      title: field("Título"),
      index: field("Número"),
      rows: array(
        "Linhas",
        object("Linha", {
          label: field("Rótulo"),
          value: field("Valor"),
          note: field("Observação")
        })
      ),
      badge: field("Badge"),
      caption: field("Legenda")
    }),
    about: object("Sobre", {
      number: field("Número"),
      label: field("Rótulo"),
      title: field("Título"),
      lead: textarea("Lead"),
      paragraphs: array("Parágrafos", textarea("Parágrafo")),
      principles: array(
        "Princípios",
        object("Princípio", {
          title: field("Título"),
          description: textarea("Descrição")
        })
      )
    }),
    skills: object("Competências", {
      number: field("Número"),
      label: field("Rótulo"),
      title: field("Título"),
      lead: textarea("Lead"),
      groups: array(
        "Grupos",
        object("Grupo", {
          title: field("Título"),
          items: array("Itens", { type: "string" })
        })
      )
    }),
    experience: object("Experiência", {
      number: field("Número"),
      label: field("Rótulo"),
      title: field("Título"),
      lead: textarea("Lead"),
      jobs: array(
        "Vagas",
        object("Vaga", {
          company: field("Empresa"),
          role: field("Cargo"),
          location: field("Local"),
          period: field("Período"),
          duration: field("Duração"),
          bullets: array("Atividades", textarea("Atividade"))
        })
      )
    }),
    projects: object("Projetos", {
      number: field("Número"),
      label: field("Rótulo"),
      title: field("Título"),
      lead: textarea("Lead"),
      note: textarea("Nota"),
      cases: array(
        "Cases",
        object("Case", {
          slug: field("Slug"),
          kicker: field("Categoria"),
          title: field("Título"),
          summary: textarea("Resumo"),
          tags: array("Tags visíveis", { type: "string" }),
          context: textarea("Contexto"),
          decision: textarea("Decisão"),
          result: textarea("Resultado"),
          stack: array("Stack", { type: "string" }),
          note: textarea("Nota da ficha")
        })
      )
    }),
    education: object("Formação", {
      number: field("Número"),
      label: field("Rótulo"),
      title: field("Título"),
      lead: textarea("Lead"),
      degree: object("Grau", {
        label: field("Rótulo"),
        institution: field("Instituição"),
        degree: field("Curso"),
        period: field("Período")
      }),
      certificationsLabel: field("Rótulo das certificações"),
      certifications: array("Certificações", { type: "string" })
    }),
    process: object("Processo", {
      number: field("Número"),
      label: field("Rótulo"),
      title: field("Título"),
      lead: textarea("Lead"),
      steps: array(
        "Etapas",
        object("Etapa", {
          title: field("Título"),
          description: textarea("Descrição"),
          output: textarea("Saída")
        })
      )
    }),
    contact: object("Contato", {
      number: field("Número"),
      label: field("Rótulo"),
      title: field("Título"),
      lead: textarea("Lead"),
      formNote: textarea("Nota do formulário"),
      subjects: array(
        "Tipos de conversa",
        object("Tipo", {
          value: field("Valor"),
          label: field("Rótulo")
        })
      ),
      successTitle: field("Título de sucesso"),
      successText: textarea("Texto de sucesso")
    }),
    navigation: array(
      "Navegação do topo",
      object("Link", {
        label: field("Rótulo"),
        href: field("URL")
      })
    ),
    footerNavigation: array(
      "Navegação do rodapé",
      object("Link", {
        label: field("Rótulo"),
        href: field("URL")
      })
    )
  });

  function headers(extra) {
    var result = { Accept: "application/json" };
    if (token) result.Authorization = "Bearer " + token;
    if (extra) {
      Object.keys(extra).forEach(function (key) {
        result[key] = extra[key];
      });
    }
    return result;
  }

  async function api(path, options) {
    options = options || {};
    var response = await fetch(API_BASE + path, {
      method: options.method || "GET",
      headers: headers(options.body ? { "Content-Type": "application/json" } : undefined),
      body: options.body
    });
    if (response.status === 401) {
      showLogin("Sessão expirada. Entre novamente.");
      throw new Error("unauthorized");
    }
    return response;
  }

  function showLogin(message) {
    token = "";
    sessionStorage.removeItem(TOKEN_KEY);
    loginView.hidden = false;
    dashboardView.hidden = true;
    if (message) {
      loginError.textContent = message;
      loginError.hidden = false;
    }
  }

  function showDashboard() {
    loginError.hidden = true;
    loginView.hidden = true;
    dashboardView.hidden = false;
    loadStats();
    loadContent();
    loadMessages();
  }

  loginForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    loginError.hidden = true;
    var email = document.getElementById("login-email").value.trim();
    var password = document.getElementById("login-password").value;

    var response = await fetch(API_BASE + "/api/admin/login", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ email: email, password: password })
    });

    if (!response.ok) {
      loginError.textContent =
        response.status === 429
          ? "Muitas tentativas. Aguarde um minuto e tente novamente."
          : "E-mail ou senha inválidos.";
      loginError.hidden = false;
      return;
    }

    var data = await response.json();
    token = data.token;
    sessionStorage.setItem(TOKEN_KEY, token);
    loginForm.reset();
    showDashboard();
  });

  logoutButton.addEventListener("click", function () {
    showLogin("");
    loginError.hidden = true;
  });

  var tabButtons = Array.prototype.slice.call(document.querySelectorAll("[data-tab]"));
  var tabPanels = Array.prototype.slice.call(document.querySelectorAll("[data-tab-panel]"));

  function selectTab(name) {
    tabButtons.forEach(function (button) {
      button.classList.toggle("active", button.getAttribute("data-tab") === name);
    });
    tabPanels.forEach(function (panel) {
      panel.hidden = panel.getAttribute("data-tab-panel") !== name;
    });
    if (name === "messages") loadMessages();
    if (name === "content") loadStats();
  }

  tabButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      selectTab(button.getAttribute("data-tab"));
    });
  });

  async function loadStats() {
    try {
      var response = await api("/api/admin/stats");
      if (!response.ok) return;
      var data = await response.json();
      statTotal.textContent = data.messages ? data.messages.total : "—";
      statUnread.textContent = data.messages ? data.messages.unread : "—";
      statContent.textContent = data.contentUpdatedAt
        ? new Date(data.contentUpdatedAt).toLocaleString("pt-BR")
        : "—";
    } catch (error) {
      /* sessão expirada já tratada */
    }
  }

  async function loadContent() {
    contentStatus.textContent = "Carregando conteúdo...";
    try {
      var response = await api("/api/admin/content");
      if (!response.ok) {
        contentStatus.textContent = "Não foi possível carregar o conteúdo.";
        return;
      }
      var data = await response.json();

      if (!window.JSONEditor) {
        contentStatus.textContent = "Editor de conteúdo não carregou. Recarregue a página.";
        return;
      }

      if (!editor) {
        editor = new window.JSONEditor(editorContainer, {
          schema: schema,
          theme: "html",
          disable_collapse: false,
          disable_edit_json: true,
          disable_properties: true,
          disable_array_reorder: false,
          required_by_default: false,
          show_errors: "always"
        });
        await editor.promise;
      }

      editor.setValue(data.content);
      contentStatus.textContent =
        "Última atualização: " +
        (data.updatedAt ? new Date(data.updatedAt).toLocaleString("pt-BR") : "—");
    } catch (error) {
      contentStatus.textContent = "Falha ao carregar o conteúdo.";
    }
  }

  saveButton.addEventListener("click", async function () {
    if (!editor) return;
    saveButton.disabled = true;
    contentStatus.textContent = "Salvando...";
    try {
      var response = await api("/api/admin/content", {
        method: "PUT",
        body: JSON.stringify({ content: editor.getValue() })
      });
      if (!response.ok) {
        var error = await response.json().catch(function () { return {}; });
        contentStatus.textContent = "Erro: " + (error.error || response.status);
        return;
      }
      var data = await response.json();
      contentStatus.textContent =
        "Salvo com sucesso em " + new Date(data.updatedAt).toLocaleString("pt-BR");
      loadStats();
    } catch (error) {
      contentStatus.textContent = "Falha ao salvar. Tente novamente.";
    } finally {
      saveButton.disabled = false;
    }
  });

  reloadButton.addEventListener("click", loadContent);

  var STATUS_LABELS = {
    new: "Nova",
    read: "Lida",
    replied: "Respondida",
    archived: "Arquivada"
  };

  async function updateMessage(id, status) {
    var response = await api("/api/admin/messages/" + id, {
      method: "PATCH",
      body: JSON.stringify({ status: status })
    });
    if (response.ok) {
      loadMessages();
      loadStats();
    }
  }

  async function deleteMessage(id) {
    if (!window.confirm("Excluir esta mensagem? Esta ação não pode ser desfeita.")) return;
    var response = await api("/api/admin/messages/" + id, { method: "DELETE" });
    if (response.ok) {
      loadMessages();
      loadStats();
    }
  }

  async function loadMessages() {
    messagesStatus.textContent = "Carregando mensagens...";
    try {
      var status = messageFilter.value;
      var response = await api(
        "/api/admin/messages?page=1&pageSize=50" +
          (status ? "&status=" + encodeURIComponent(status) : "")
      );
      if (!response.ok) {
        messagesStatus.textContent = "Não foi possível carregar as mensagens.";
        return;
      }
      var data = await response.json();
      messagesStatus.textContent = data.total + " mensagem(ns)";
      messagesBody.textContent = "";

      if (!data.items.length) {
        var emptyRow = document.createElement("tr");
        var emptyCell = document.createElement("td");
        emptyCell.colSpan = 6;
        emptyCell.className = "empty";
        emptyCell.textContent = "Nenhuma mensagem.";
        emptyRow.appendChild(emptyCell);
        messagesBody.appendChild(emptyRow);
        return;
      }

      data.items.forEach(function (message) {
        var row = document.createElement("tr");

        var statusCell = document.createElement("td");
        var badge = document.createElement("span");
        badge.className = "badge " + message.status;
        badge.textContent = STATUS_LABELS[message.status] || message.status;
        statusCell.appendChild(badge);
        row.appendChild(statusCell);

        var contactCell = document.createElement("td");
        var name = document.createElement("strong");
        name.textContent = message.name;
        var email = document.createElement("div");
        email.className = "muted";
        email.textContent = message.email;
        contactCell.appendChild(name);
        contactCell.appendChild(email);
        row.appendChild(contactCell);

        var subjectCell = document.createElement("td");
        subjectCell.textContent = message.subject;
        row.appendChild(subjectCell);

        var messageCell = document.createElement("td");
        var details = document.createElement("details");
        var summary = document.createElement("summary");
        summary.textContent = "Ver mensagem";
        var body = document.createElement("p");
        body.textContent = message.message;
        details.appendChild(summary);
        details.appendChild(body);
        messageCell.appendChild(details);
        row.appendChild(messageCell);

        var dateCell = document.createElement("td");
        dateCell.textContent = new Date(message.createdAt).toLocaleString("pt-BR");
        row.appendChild(dateCell);

        var actionsCell = document.createElement("td");
        var actions = document.createElement("div");
        actions.className = "row-actions";

        function addAction(label, nextStatus) {
          var button = document.createElement("button");
          button.type = "button";
          button.className = "btn ghost";
          button.textContent = label;
          button.addEventListener("click", function () {
            updateMessage(message.id, nextStatus);
          });
          actions.appendChild(button);
        }

        if (message.status !== "read") addAction("Marcar lida", "read");
        if (message.status !== "replied") addAction("Respondida", "replied");
        if (message.status !== "archived") addAction("Arquivar", "archived");

        var deleteButton = document.createElement("button");
        deleteButton.type = "button";
        deleteButton.className = "btn ghost";
        deleteButton.textContent = "Excluir";
        deleteButton.addEventListener("click", function () {
          deleteMessage(message.id);
        });
        actions.appendChild(deleteButton);
        actionsCell.appendChild(actions);
        row.appendChild(actionsCell);

        messagesBody.appendChild(row);
      });
    } catch (error) {
      messagesStatus.textContent = "Falha ao carregar as mensagens.";
    }
  }

  messageFilter.addEventListener("change", loadMessages);
  refreshMessages.addEventListener("click", loadMessages);

  changePasswordForm.addEventListener("submit", async function (event) {
    event.preventDefault();
    passwordStatus.style.color = "var(--muted)";
    var current = document.getElementById("current-password").value;
    var next = document.getElementById("new-password").value;
    var confirm = document.getElementById("confirm-password").value;

    if (next !== confirm) {
      passwordStatus.textContent = "A confirmação da nova senha não confere.";
      passwordStatus.style.color = "var(--danger)";
      return;
    }

    var response = await api("/api/admin/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword: current, newPassword: next })
    });

    var data = await response.json().catch(function () { return {}; });

    if (response.ok) {
      passwordStatus.textContent = "Senha alterada com sucesso.";
      passwordStatus.style.color = "var(--ok)";
      changePasswordForm.reset();
    } else {
      passwordStatus.textContent = data.error || "Não foi possível alterar a senha.";
      passwordStatus.style.color = "var(--danger)";
    }
  });

  if (token) {
    api("/api/admin/me")
      .then(function (response) {
        if (response.ok) showDashboard();
        else showLogin("");
      })
      .catch(function () {
        showLogin("");
      });
  } else {
    showLogin("");
  }
})();
