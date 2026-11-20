# Backlog do programa de documentação da API

Ordem de execução da Fase 0 à Fase 4. Cada item é um ticket com escopo fechado,
critério de aceite verificável e dependência explícita. Estimativa em dias de
uma pessoa.

Contexto: [ADR 0017](../adr/0017-portal-publico-da-api.md),
[inventário](inventario.md), [política editorial](politica-editorial.md) e
[arquitetura do portal](portal.md).

## Visão geral

| Fase | Objetivo | Entrega | Estimativa |
| --- | --- | --- | --- |
| **F0** | Decidir e mapear | este pacote de documentos | concluída |
| **F1** | Contrato que não mente | `openapi.json` completo e verificado | 4–5 d |
| **F2** | Portal no ar | `docs.fisga.abson.tech` publicado com referência completa | 6–8 d |
| **F3** | Console e qualidade | "Experimente" read-only, a11y e e2e | 3–4 d |
| **F4** | Alcance | sandbox, console autenticado, SDK, política de versão | 5–8 d |

F1 é bloqueio de F2: a referência é gerada do contrato, então publicar antes de
corrigir significa publicar erro. F2 e F3 podem começar em paralelo depois que o
gerador da F2.1 estiver de pé.

---

## F1 — Contrato verdadeiro

Fecha as 12 lacunas do [inventário](inventario.md#lacunas-de-contrato). Nenhuma
mudança de comportamento da API; só metadata, anotações e dois ajustes
aditivos de resposta.

### F1.1 — Esquemas de segurança

- **Issue:** [#63](https://github.com/AbsonDev/abson/issues/63)
- **Escopo:** declarar `cookieAuth` (`fisga_auth`), `adminToken`
  (`X-Admin-Token`) e `shareToken` (path/query); aplicar exigência por operação.
- **Aceite:** nenhuma operação autenticada sem `security`; `GET /api/auth/me` e
  `POST /api/auth/sair` explicitamente anônimos.
- **Est.:** 0,5 d · **Dep.:** —

### F1.2 — Status reais de sucesso

- **Issue:** [#64](https://github.com/AbsonDev/abson/issues/64)
- **Escopo:** `Produces<T>(201/202/204)` nos handlers que devolvem algo diferente
  de `200`; `Location` no `201` de pescaria, local, ponto e trilha.
- **Aceite:** o spec lista o status que o código devolve em todas as 54
  operações; teste de contrato compara spec × handler para os quatro `POST` de
  criação.
- **Est.:** 1 d · **Dep.:** —

### F1.3 — Um único formato de erro, documentado

- **Issue:** [#65](https://github.com/AbsonDev/abson/issues/65)
- **Escopo:** schema `ErroResponse` (`codigo`, `detalhe`), `ProducesProblem` por
  status em cada operação e catálogo de `codigo` em um único lugar do código.
- **Aceite:** todos os erros de handler e middleware apontam para o mesmo
  schema; o spec não mistura ProblemDetails do framework com `ErroResponse` sem
  dizer qual é qual; `429` documentado como corpo vazio.
- **Est.:** 1 d · **Dep.:** F1.1

### F1.4 — Tags, `operationId` e descrições

- **Issue:** [#62](https://github.com/AbsonDev/abson/issues/62)
- **Escopo:** 13 tags de domínio, `operationId` estável por operação e
  `summary`/`description` de negócio em português.
- **Aceite:** `operationId` congelado no mapa versionado; renomear rota exige
  atualizar o mapa; sem tag `Fisga.Api` residual.
- **Est.:** 1 d · **Dep.:** —

### F1.5 — Uploads com schema

- **Issue:** [#66](https://github.com/AbsonDev/abson/issues/66)
- **Escopo:** schema `multipart/form-data` para `/api/midia` e
  `/api/especies/sugerir`: campo `arquivo`, tipos aceitos e limite de 5 MB.
- **Aceite:** o spec descreve o campo e o limite; erro de tipo e de tamanho
  aparecem na tabela de erros.
- **Est.:** 0,5 d · **Dep.:** F1.3

### F1.6 — Rate limit e cache no contrato

- **Issue:** [#67](https://github.com/AbsonDev/abson/issues/67)
- **Escopo:** expor a política de rate limit e o `Cache-Control` de cada
  operação como extensão (`x-rate-limit`, `x-cache`) gerada da metadata real,
  não escrita à mão.
- **Aceite:** os valores do spec batem com `RateLimiting:*` e com
  `CachePolicy`; teste falha se um endpoint ganhar política sem refletir no
  spec.
- **Est.:** 1 d · **Dep.:** F1.4

### F1.7 — Identidade do documento

- **Issue:** [#68](https://github.com/AbsonDev/abson/issues/68)
- **Escopo:** `info.contact`, `info.license` (proprietária), `info.description`
  e `servers` por ambiente.
- **Aceite:** spec válido no lint de OpenAPI, sem `servers` apontando para
  ambiente morto.
- **Est.:** 0,5 d · **Dep.:** —

### F1.8 — `429` com `Retry-After`

- **Issue:** [#69](https://github.com/AbsonDev/abson/issues/69)
- **Escopo:** adicionar `Retry-After` na rejeição do rate limiter. Corpo
  permanece vazio.
- **Aceite:** resposta `429` traz `Retry-After` em segundos; teste cobre; portal
  documenta o header.
- **Est.:** 0,5 d · **Dep.:** —

### F1.9 — Comportamentos que surpreendem

- **Issue:** [#70](https://github.com/AbsonDev/abson/issues/70)
- **Escopo:** descrever no contrato: `404` proposital das rotas administrativas,
  `GET` de auth com efeito colateral e `302`, `200` com `autenticado:false` em
  `/api/auth/me`, `403 consentimento_pendente`, `403 origem_invalida`.
- **Aceite:** cada caso tem `description` e, quando aplicável, exemplo de
  resposta no spec.
- **Est.:** 0,5 d · **Dep.:** F1.4

### F1.10 — Impedir divergência entre spec, README e inventário

- **Issue:** [#71](https://github.com/AbsonDev/abson/issues/71)
- **Escopo:** script no job `docs` que extrai as rotas do `openapi.json`, do
  resumo do `README.md` e das linhas do inventário e falha listando a operação
  divergente. Hoje os três coincidem; a comparação é manual e nada quebra quando
  um endpoint novo entra só no código.
- **Aceite:** rota adicionada, removida ou renomeada no spec quebra o job com a
  lista do que falta em cada documento.
- **Est.:** 0,5 d · **Dep.:** F1.4

---

## F2 — Portal no ar

### F2.1 — Esqueleto do site e gerador da referência

- **Escopo:** `Projetos/fisga/docs-site/` em Astro estático; gerador
  `scripts/gerar-referencia.mjs` com as cinco falhas de build da
  [arquitetura](portal.md#pipeline-de-build); layout base com design system do
  Fisga.
- **Aceite:** `astro build` gera as 54 páginas de operação a partir do spec;
  build falha quando falta override ou quando um status do spec não está
  documentado.
- **Est.:** 2 d · **Dep.:** F1.4

### F2.2 — Conteúdo de prosa

- **Escopo:** `docs/publico/`: visão geral, guia rápido, 4 guias de conceito,
  erros, limites, glossário e privacidade.
- **Aceite:** guia rápido executado do zero por alguém fora do time; checklist
  da política editorial aplicado; `markdownlint` e `lychee` verdes.
- **Est.:** 3 d · **Dep.:** F2.1

### F2.3 — Busca, SEO e acessibilidade

- **Escopo:** Pagefind, `sitemap.xml`, `robots.txt`, metadados por página,
  revisão AA.
- **Aceite:** busca acha operação por nome de rota e por conceito; Lighthouse
  ≥ 95 em acessibilidade; um `h1` por página.
- **Est.:** 1 d · **Dep.:** F2.2

### F2.4 — CI do portal

- **Escopo:** output `portal` no job `changes`, job `docs-site` no
  `fisga-pipeline.yml`, `gate` dependendo dele.
- **Aceite:** PR que muda só documentação roda `docs-site` e não roda `api`;
  falha no gerador bloqueia merge; tempo do job ≤ 6 min.
- **Est.:** 1 d · **Dep.:** F2.1

### F2.5 — Deploy e smoke

- **Escopo:** app estático no Coolify, DNS `docs.fisga.abson.tech`, secret
  `COOLIFY_WEBHOOK_FISGA_DOCS`, job `deploy-docs` em paralelo a
  `deploy-api`/`deploy-web`, `/health.txt` com SHA.
- **Aceite:** push na `main` publica e o smoke confirma o SHA; rollback
  documentado; `docs/ci-cd.md` e `AGENTS.md` atualizados.
- **Est.:** 1 d · **Dep.:** F2.4

---

## F3 — Console e qualidade

### F3.1 — Console read-only

- **Escopo:** ilha JS com `fetch`, `GET` público apenas, `credentials: 'omit'`,
  cópia de `curl` para tudo, bloqueio explícito de escrita e de admin.
- **Aceite:** nenhuma escrita disparável; rate limit respeitado (1 chamada por
  clique); CORS de `docs.fisga.abson.tech` funcionando em produção.
- **Est.:** 2 d · **Dep.:** F2.5

### F3.2 — E2E do portal

- **Escopo:** Playwright no portal publicado: home, busca, referência, console em
  operação pública, estado de erro.
- **Aceite:** suíte roda no CI em ≤ 3 min; falha de console JS quebra o job.
- **Est.:** 1 d · **Dep.:** F3.1

### F3.3 — Observabilidade do portal

- **Escopo:** projeto Sentry `fisga-docs`, só erro de JS; alerta de falha de
  deploy.
- **Aceite:** erro provocado no console aparece no Sentry com release = SHA;
  nenhum dado pessoal no evento.
- **Est.:** 0,5 d · **Dep.:** F2.5

---

## F4 — Alcance

### F4.1 — Sandbox

- **Escopo:** ambiente de testes com banco efêmero, seed sintético, credencial
  de sandbox, reset periódico e limites próprios.
- **Aceite:** guia rápido rodável ponta a ponta no sandbox, inclusive escrita;
  nenhum dado real; custo e reset documentados.
- **Est.:** 3 d · **Dep.:** F3.1

### F4.2 — Console autenticado com opt-in

- **Escopo:** envio do cookie `fisga_auth` a partir do portal, com aviso
  explícito, `AllowCredentials` restrito e revisão de CSRF.
- **Aceite:** leitura autenticada funciona; escrita continua indisponível;
  revisão de segurança registrada em ADR.
- **Est.:** 1,5 d · **Dep.:** F4.1

### F4.3 — Política de versão e depreciação

- **Escopo:** janela de 90 dias, convivência `/v2`, cabeçalhos de depreciação e
  página de changelog explicando impacto.
- **Aceite:** ADR aprovada; template de anúncio; teste que garante que rota
  depreciada responde com aviso.
- **Est.:** 1 d · **Dep.:** F2.2

### F4.4 — Clientes e SDK

- **Escopo:** avaliar SDK TypeScript gerado do spec e coleção de exemplos
  (`curl`, `fetch`, `HttpClient`).
- **Aceite:** decisão registrada; se aprovado, pacote publicado com versão
  casada ao `info.version`.
- **Est.:** 2 d · **Dep.:** F1.3

### F4.5 — Documentação de operação administrativa

- **Escopo:** guia interno de newsletter (envio, preview, descadastro) com
  procedimento de incidente.
- **Aceite:** runbook executável por outra pessoa; sem token no texto.
- **Est.:** 0,5 d · **Dep.:** —

---

## Fora de escopo

- Documentação de produto (espécies, técnicas, locais) — vive no
  `web/src/content`.
- Tradução para outro idioma.
- Documentação interna de arquitetura, que já está em [`ddd.md`](../ddd.md).
- Qualquer mudança de comportamento da API além dos ajustes aditivos já listados
  na própria F1: `Location` nos `201` (F1.2) e `Retry-After` no `429` (F1.8).
  Nenhuma rota, campo de request ou regra de negócio muda de semântica.

## Riscos do programa

| Risco | Impacto | Mitigação |
| --- | --- | --- |
| F1 escorregar e travar F2 | alto | F1.4 primeiro; F2.1 pode ser construído com o spec atual |
| PR de documentação colidir com trabalho paralelo no mesmo repo | médio | worktree por issue e claim; `docs/api/**` é território desta issue |
| Portal divergir do design system | médio | `design-system/` versionado, revisão visual no PR da F2.1 |
| Referência pública de rotas administrativas | baixo | `404` sem token já é o comportamento; portal explica |
| Custo do terceiro app no Coolify | baixo | site estático; medir na F2.5 |
| Portal publicar contrato antes da API | médio | `deploy-docs` depende de `deploy-api`; portal nunca sai antes do contrato estar no ar |
