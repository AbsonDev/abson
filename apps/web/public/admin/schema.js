(function () {
  'use strict';

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

  window.ABSON_ADMIN_SCHEMA = object("Conteúdo do site", {
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
})();
