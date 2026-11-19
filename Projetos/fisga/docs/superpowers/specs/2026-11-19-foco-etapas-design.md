# Fisga — foco de teclado entre etapas do `/registrar`

**Data:** 19/11/2026
**Status:** aprovado pelo usuário em 19/11/2026
**Issue:** #53 (promovida do draft privado do quadro 6 com acordo explícito)
**Escopo:** `/registrar` (web); sem mudança de API, contrato ou banco.

## Intenção

Hoje `mostrarEtapa()` (`web/src/pages/registrar.astro:1406-1421`) alterna o atributo
`hidden` dos quatro painéis, atualiza `aria-current` na navegação, recalcula o resumo
da etapa 4 e rola a página — mas não move o foco. Depois de "Continuar", o foco cai no
`<body>`: quem usa leitor de tela não ouve o novo painel e quem usa teclado recomeça do
topo. A meta é que o foco vá para o `<h2>` do painel visível e que o nome acessível
anuncie `Etapa N — <título>`.

## Objetivos

- `document.activeElement` seja o `<h2>` do painel exibido após cada transição.
- O nome acessível do título contenha o prefixo `Etapa N — ` em pt-BR.
- Avançar, voltar e clicar na navegação de passos tenham o mesmo comportamento.
- A validação da etapa 1 continue levando o foco ao campo inválido (`#local` ou `#data`).

## Não-objetivos

- Não alterar validação, scroll suave, rascunho, EXIF, upload ou qualquer outra issue.
- Não mudar o contrato da API, o OpenAPI, migrações ou configuração.
- Não criar anel de foco visual novo: o título recebe foco programático, os controles
  mantêm o `:focus-visible` que já existe.
- Não reabrir os outros drafts de acessibilidade do quadro (alvos de toque, defeso,
  pós-save).

## Anti-objetivos

- Focar um contêiner sem nome acessível no lugar do título.
- Gerar `aria-label` em JavaScript em runtime.
- Live region paralela que provoque dupla locução do título.

## Restrições

- Copy em pt-BR; prefixo exatamente `Etapa N — ` para bater no critério de aceite da
  issue #53.
- Referência visual lida nesta tarefa: `DESIGN.md` e `registrar/index.html` do projeto
  OpenDesign `fisga-produto`. Follow-up de design cobre o estado "troca de etapa".
- Os quatro títulos já são alvos de `aria-labelledby` das respectivas `<section>`.
- Cobertura E2E em `web/tests/`; comandos de validação do `AGENTS.md` do Fisga.
- Documentação de desenvolvimento atualizada no mesmo PR e indexada em `docs/INDEX.md`.

## Abordagens consideradas

| Opção | Trade-off |
| --- | --- |
| A — `tabindex="-1"` + prefixo oculto estático (escolhida) | Nome acessível estável e verificável, `activeElement` é o título, nenhuma mutação de `aria-label` em runtime. Custo: uma classe utilitária de ocultação no `global.css`. |
| B — `aria-label` dinâmico no título | Menos markup, mas o nome acessível passa a depender de JS e pode duplicar o prefixo em chamadas repetidas de `mostrarEtapa()`. |
| C — foco no título + live region | Anúncio mais robusto em alguns leitores, porém risco de dupla locução e mais estado no DOM sem necessidade. |

## Decisões

1. Os quatro `<h2>` (`titulo-pescaria`, `titulo-condicoes`, `titulo-capturas`,
   `titulo-revisar`) recebem `tabindex="-1"` e um `<span class="visually-hidden">`
   com `Etapa 1 — `, `Etapa 2 — `, `Etapa 3 — ` e `Etapa 4 — `, respectivamente.
2. O utilitário `.visually-hidden` entra em `web/src/styles/global.css`, seguindo o
   padrão já usado em `auth.css` (recuo de 1 px, `clip`, `white-space: nowrap`).
3. `mostrarEtapa()` foca o `<h2>` do painel ativo com `{ preventScroll: true }`,
   depois de alternar os painéis e antes do `window.scrollTo` existente.
4. `validarEtapa(1)` continua chamando `mostrarEtapa(1)` e, em seguida,
   `local.focus()` / `data.focus()` — o campo inválido vence o foco do título.
5. Não há chamada a `mostrarEtapa()` na carga inicial, então a página não rouba foco
   ao abrir.

## Deixado para o implementador

- Nome exato da classe utilitária (usar `.visually-hidden`).
- Estrutura dos testes E2E: nomes e asserções estão fixados no plano.
