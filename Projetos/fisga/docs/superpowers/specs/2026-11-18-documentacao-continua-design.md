# Fisga — Design de Documentação Contínua

**Status:** aprovado para implementação
**Data:** 2026-11-18
**Stack:** Markdown + Git + GitHub Actions + Astro Starlight (fase 2) + ASP.NET Core 10 + OpenAPI
**Escopo:** documentação de desenvolvimento do Fisga. Não inclui a Wiki do Pescador, conteúdo editorial de produto nem a documentação de outros projetos do monorepo.

---

## 1. Contexto

O Fisga já tem documentação relevante, mas ela está espalhada e sem uma porta de entrada única:

- `Projetos/fisga/README.md` tem 654 linhas e mistura visão de produto, setup, testes, API, CI/CD e links.
- `Projetos/fisga/docs/` tem ADRs, DDD, CI/CD, review, roadmap, auditoria de UX, runbooks e referências.
- A seção `## Documentação` do README não lista `docs/ci-cd.md` nem `docs/review.md`.
- Não existe `AGENTS.md` dentro do repositório; as instruções que os agentes usam hoje vivem no arquivo global da máquina e não viajam com o código.
- Não existe um gate que impeça link quebrado, Markdown inválido ou documentação semanticamente desatualizada.
- A Wiki nativa do GitHub está habilitada, mas não tem conteúdo e não é docs-as-code: não tem PR, CI, nem entra automaticamente no contexto de agentes.

O objetivo não é criar mais uma wiki. É fazer a documentação evoluir junto com o código, com uma fonte única e superfícies geradas.

---

## 2. Decisão central

> **A fonte de verdade fica no repositório, em Markdown versionado. O site, o `llms.txt`, o OpenAPI e o contexto para agentes são superfícies geradas a partir dela. A IA aconselha, mas não é gate.**

Consequências:

1. Toda mudança de documentação passa por PR, revisão e CI.
2. O mesmo conteúdo serve humanos, code review e agentes.
3. Nada é editado em dois lugares: se um dado aparece no README e no índice, um dos dois é removido ou vira link.
4. A Wiki nativa fica de fora nesta fase. Ela pode ser usada depois como camada editorial humana, nunca como fonte canônica.
5. O job de IA comenta sugestões; o gate determinístico decide se o merge é seguro.

---

## 3. Objetivos

- Criar uma porta de entrada única para a documentação de desenvolvimento.
- Dar aos agentes um `AGENTS.md` versionado e um índice que eles consigam seguir.
- Impedir regressões baratas: link morto, Markdown inválido, workflow inválido.
- Expor o contrato real da API a partir do código, não de uma lista manual.
- Publicar uma versão navegável e pesquisável da documentação no GitHub Pages.
- Expor `llms.txt` e Markdown limpo para agentes remotos.
- Usar o `OPENCODE_API_KEY` já existente para análise automática de drift de documentação em todo PR do Fisga.
- Manter o modo sob demanda (`/docs`) para uma análise mais profunda quando necessário.

## 4. Não-objetivos

- Substituir a Wiki do Pescador ou o conteúdo de espécies, iscas, técnicas e locais.
- Criar uma plataforma SaaS de documentação (Mintlify, ReadMe, GitBook).
- Publicar documentação interna sensível, credenciais, runbooks de acesso ou detalhes de infraestrutura que não possam ser públicos.
- Fazer a IA escrever ou commitar documentação sozinha.
- Bloquear merge por falha de LLM, quota ou instabilidade de provider.
- Documentar outros projetos do monorepo neste primeiro corte.

## 5. Anti-objetivos

- Duplicar o mesmo conteúdo em README, índice, site e Wiki.
- Criar uma wiki paralela que ninguém atualiza.
- Gerar documentação de API a partir de uma lista mantida à mão.
- Usar `pull_request_target` para obter secrets em PRs de fork.
- Colocar a key do 9router ou do OpenCode Go em arquivo do repositório.
- Fazer o job de IA commitar, abrir branch ou alterar arquivos.

---

## 6. Invariantes

1. **Fonte única.** `Projetos/fisga/docs/` é a fonte canônica. O site e o `llms.txt` são gerados; não são editados à mão.
2. **Documentação no mesmo PR.** Mudança de comportamento, contrato, configuração ou operação atualiza a documentação no mesmo PR.
3. **Gate determinístico.** `markdownlint`, `lychee` e build do site são obrigatórios. IA não é.
4. **IA somente leitura.** O job de IA tem `contents: read`; ele comenta, não commita.
5. **Sem secrets em PR de fork.** O job de IA só roda em PRs do mesmo repositório e ignora bots.
6. **OpenAPI é gerado do código.** Nenhum endpoint é documentado à mão como fonte primária.
7. **Nada de conteúdo sensível no site público.** O repositório é público; o site também será.
8. **Português do Brasil.** Copy, títulos, guias e mensagens do job de IA seguem o idioma do produto.
9. **O gate existente não muda de nome.** O context obrigatório continua sendo `gate`.
10. **Documentação não bloqueia deploy.** O job `docs` roda só em PR; um merge só de documentação não dispara deploy de API nem de web.

---

## 7. Arquitetura

```text
FONTE CANÔNICA (Git, PR, CODEOWNERS)
├─ Projetos/fisga/docs/
│  ├─ INDEX.md                  ← porta de entrada
│  ├─ ddd.md, ci-cd.md, review.md, roadmap.md, ux-audit.md
│  ├─ guias/como-adicionar-endpoint.md
│  ├─ adr/*.md
│  ├─ superpowers/runbooks/*.md
│  └─ referencias/*.md
├─ Projetos/fisga/AGENTS.md     ← contexto versionado para agentes
├─ AGENTS.md                    ← mapa do monorepo
├─ .github/copilot-instructions.md
├─ código .NET + /// + OpenAPI gerado no build
└─ .github/workflows/fisga-pipeline.yml (gate docs)

        │ CI determinístico
        ▼
QUALIDADE
├─ markdownlint
├─ lychee (links)
└─ build do site (fase 2)

        │ publicação
        ▼
SUPERFÍCIES GERADAS
├─ GitHub Pages / Starlight (HTML + Pagefind)
├─ openapi.json publicado
├─ llms.txt + Markdown limpo
└─ contexto local para agentes (AGENTS.md + docs/)

        │ aconselhamento
        ▼
IA (não bloqueante)
├─ automática em todo PR do Fisga
├─ /docs sob demanda
└─ workflow_dispatch manual
```

---

## 8. Abordagens consideradas

### 8.1 GitHub Wiki como fonte canônica

**Prós:** edição rápida, interface familiar, histórico e diff por página.

**Contras:** repositório Git separado, sem PR no fluxo normal, sem CI integrado, fora do checkout dos agentes, fácil de desatualizar. Não atende ao requisito de documentação contínua nem ao contexto de IA.

**Decisão:** rejeitada como fonte. Pode ser camada editorial futura.

### 8.2 Plataforma SaaS (Mintlify, ReadMe, GitBook)

**Prós:** assistant, MCP, busca e hospedagem prontos; ótimo acabamento.

**Contras:** custo recorrente, lock-in, conteúdo público, integração com o monorepo e com o CI atual é mais uma dependência. Para um projeto de portfólio, o ganho não justifica o custo nesta fase.

**Decisão:** rejeitada no primeiro corte.

### 8.3 Docs no repositório + site gerado (escolhida)

**Prós:** fonte única, PR, CI, histórico, contexto para agentes, sem custo de plataforma, encaixa no stack Astro/.NET.

**Contras:** exige disciplina de atualização e um pipeline de publicação; Starlight precisa de um adaptador para ler os documentos existentes sem frontmatter.

**Decisão:** escolhida. Starlight é a primeira opção; DocFX é o fallback se a ingestão dos documentos existentes exigir mudanças demais.

---

## 9. Componentes

### 9.1 Índice e porta de entrada

`Projetos/fisga/docs/INDEX.md` passa a ser o mapa único:

- Comece aqui
- Visão geral e arquitetura
- Como adicionar um endpoint
- Testes
- CI/CD e deploy
- Runbooks
- ADRs
- Referências

O `README.md` mantém a visão de produto e aponta para o índice; a seção `## Documentação` lista o índice primeiro e inclui `ci-cd.md` e `review.md`. O README não duplica o conteúdo do índice.

### 9.2 Contexto para agentes

- `AGENTS.md` na raiz: mapa do monorepo e ponteiro para o Fisga.
- `Projetos/fisga/AGENTS.md`: comandos, arquitetura, invariantes de CI/CD, onde atualizar a documentação e o que nunca fazer (segredos, deploy manual, merge durante deploy).
- `.github/copilot-instructions.md`: ponteiro curto para o `AGENTS.md` e o índice, sem duplicar o conteúdo.

O `AGENTS.md` não copia o arquivo global da máquina. Ele contém apenas o que é verdadeiro para qualquer pessoa ou agente que clone o repositório.

### 9.3 Gate determinístico

Novo job `docs` em `.github/workflows/fisga-pipeline.yml`:

- `needs: changes`, condicionado aos outputs `documentacao` ou `fisga`.
- **Só roda em PR e `workflow_dispatch`.** Link externo que morre depois do merge não pode reprovar a `main` e bloquear o deploy de código já mergeado.
- `markdownlint-cli2` na documentação de desenvolvimento (`AGENTS.md`, `Projetos/fisga/README.md`, `Projetos/fisga/AGENTS.md`, `Projetos/fisga/docs/**`). Conteúdo de produto e `design-system/` ficam fora.
- `MD013` (linha > 80) desligado: é estilo, não erro.
- Corpus limpo uma vez antes de ligar o gate: 60 erros prévios em `ddd.md`, `review.md` e `ux-audit.md` (`MD060`, `MD036`, `MD029`, `MD040`). `docs/superpowers/**` fica fora: são artefatos de processo, não documentação canônica.
- `lychee` em links locais e externos, com retries e exclusões explícitas.
- Configuração versionada (`.markdownlint-cli2.jsonc`, `.lychee.toml`).
- Inclusão de `docs` em `gate.needs`.

O job não roda em PRs que não tocam documentação. O `gate` continua sendo o único check obrigatório e mantém o nome exato.

O job `changes` passa a distinguir **código** de **documentação**: `Projetos/fisga/docs/**`, `README.md`, `AGENTS.md`, `AGENTS.md` da raiz, `copilot-instructions.md` e as configs de lint não disparam mais `api`, `web`, `docker` nem deploy.

### 9.4 OpenAPI

- `Microsoft.Extensions.ApiDescription.Server` no `Fisga.Api.csproj`, com geração **opt-in** (`-p:GenerateOpenApiDocs=true`) para não exigir configuração em todo build local.
- `Projetos/fisga/scripts/gerar-openapi.sh` usa Development com SQLite temporário e Sentry desligado — verificado em 2026-11-18: 48 paths, 25 schemas, hash estável entre rebuilds.
- Saída commitada em `docs/api/openapi.json`.
- O job `api` regenera e roda `git diff --exit-code`; contrato alterado sem regenerar reprova o PR.
- O JSON também é publicado como artifact do CI.
- A documentação da API descreve o contrato e aponta para o JSON; não mantém lista manual de endpoints.

### 9.5 Site e superfícies para agentes

- `Projetos/fisga/docs-site/` com Astro Starlight.
- Um script de preparação copia os documentos selecionados para o content collection, deriva `title` do primeiro H1 e exclui `superpowers/plans` e `superpowers/specs`.
- Publicação no GitHub Pages via Actions.
- `llms.txt` e cópias `.md` geradas pelo mesmo script.
- URL inicial: GitHub Pages do projeto, sem dependência de DNS. Domínio próprio (`docs.fisga.abson.tech`) fica para depois, via Hostinger.

### 9.6 Job de IA

Workflow separado `.github/workflows/fisga-docs-ai.yml`:

- **Automático:** `pull_request` em `opened`, `synchronize`, `reopened`, `ready_for_review`, com `paths` para `Projetos/fisga/**`, `AGENTS.md` e `.github/copilot-instructions.md`.
- **Sob demanda:** comentário `/docs` em PR, restrito a `OWNER`, `MEMBER` ou `COLLABORATOR`.
- **Manual:** `workflow_dispatch` com número do PR.
- **Ignora:** draft, fork, Dependabot e outros bots.
- **Permissões:** `contents: read`, `pull-requests: write`, `issues: write`.
- **Modelo:** `opencode-go/deepseek-v4.1-flash`, variante `max` inicialmente.
- **Secret:** `OPENCODE_API_KEY`, já existente.
- **Concorrência:** automática cancela a anterior do mesmo PR; `/docs` tem grupo separado e não é cancelado pela automática.
- **Falha:** `continue-on-error: true`; o job fica fora de `gate.needs`.
- **Saída:** um comentário único no PR, marcado com `<!-- fisga-docs-ai -->`, **editado** a cada run. Sem drift, o comentário existente é apagado. Nunca review inline: thread inline vira pendência obrigatória e transformaria a IA em gate.
- **Execução:** usa o CLI `opencode run`, não a action `opencode/github` — a action só aceita eventos de comentário e empurra commit quando o workspace fica sujo.
- **Somente leitura:** `contents: read`, `persist-credentials: false` e `OPENCODE_PERMISSION` negando `edit`, `write`, `patch` e `bash`.
- **Prompt:** lê `AGENTS.md`, `Projetos/fisga/AGENTS.md` e `docs/INDEX.md`, recebe o diff anexado, aponta documentos e trechos, não inventa endpoints e responde `SEM_DRIFT` quando não há drift.

O 9router fica no agente local. O GitHub Actions não alcança `127.0.0.1:20128`; no CI, a credencial usada é o OpenCode Go já configurado.

---

## 10. Segurança e privacidade

- Nenhuma key, token ou nonce em Markdown, workflow, README ou `AGENTS.md`.
- O job de IA não usa `pull_request_target`.
- PRs de fork não recebem secrets e ficam fora do job de IA.
- O job de IA não tem `contents: write`; não pode commitar.
- O site é público porque o repositório é público. Runbooks com detalhes sensíveis não entram no site; permanecem no repositório apenas se não houver segredo.
- O prompt do job de IA não recebe variáveis de ambiente, tokens ou conteúdo de `.env`.
- Logs do job não imprimem a key; a action já recebe o secret por `env`.

---

## 11. Custos e limites

- O job automático consome cota do OpenCode Go a cada push de PR. O modelo flash é o mais barato; a variante `max` aumenta o custo.
- Se o consumo incomodar, a automática passa para `minimal` e o `/docs` continua em `max`.
- Links externos podem falhar por instabilidade; o `lychee` terá retries e exclusões explícitas, não `fail: false` global.
- O site gerado não substitui a documentação no repositório; ele é uma superfície de leitura.
- A wiki nativa não é atualizada por este pipeline.

---

## 12. Fases

### Fase 1 — Fundação, gate, filtros e IA (este plano)

- `docs/INDEX.md`, índice de ADRs, guia de endpoint, README, `AGENTS.md`, `copilot-instructions.md`, template de PR.
- Job `docs` com markdownlint e lychee, só em PR; inclusão em `gate.needs`.
- Separação entre mudança de código e de documentação nos filtros de CI e deploy.
- OpenAPI gerado do código, commitado e conferido no CI.
- Workflow de IA automático + `/docs` + dispatch.
- Validação comportamental em pushes no próprio PR.

**Critério de pronto:** um PR que quebra um link ou o Markdown falha no `gate`; um PR que muda um endpoint recebe comentário da IA; um PR sem impacto em docs não recebe comentário; falha de IA não bloqueia merge.

### Fase 2 — Publicação

- Site Starlight no GitHub Pages.
- `llms.txt` e Markdown limpo.
- Busca Pagefind e navegação por sidebar.
- Revisão de acessibilidade e responsividade do site.

**Critério de pronto:** URL pública abre, busca funciona, `openapi.json` corresponde ao código, `llms.txt` lista as páginas e um agente consegue navegar da entrada até a referência da API.

### Fase 3 — Evolução

- Domínio próprio via Hostinger.
- Docs evals com perguntas e citações.
- MCP server de documentação.
- Wiki nativa como camada editorial, se ainda fizer sentido.

---

## 13. Métricas

- Tempo para um novo dev sair do clone ao primeiro teste passando.
- Número de links quebrados encontrados por PR (meta: zero no merge).
- Número de PRs que mudam contrato sem atualizar docs (meta: zero).
- Comentários da IA aceitos vs. ignorados.
- Consumo mensal do OpenCode Go no job de docs.
- Páginas mais visitadas no site e buscas sem resultado.

---

## 14. Decisões e pontos em aberto

**Decidido:**

- Fonte canônica no repositório; site e `llms.txt` gerados.
- Starlight como primeira opção; DocFX como fallback.
- IA automática em todo PR do Fisga + `/docs` sob demanda + dispatch.
- IA fora do gate, sem permissão de escrita e com comentário único editável.
- `OPENCODE_API_KEY` existente; 9router apenas local.
- GitHub Pages sem domínio próprio na fase 1.
- Job `docs` só em PR; merge só de documentação não dispara deploy.
- `openapi.json` commitado e conferido por `git diff --exit-code` no CI.

**Em aberto para a fase 2:**

- O domínio `docs.fisga.abson.tech` será configurado no Hostinger ou o site fica no `github.io`.
- O site publicará os planos/specs do SuperPowers ou apenas ADRs, runbooks, guias e referências.
