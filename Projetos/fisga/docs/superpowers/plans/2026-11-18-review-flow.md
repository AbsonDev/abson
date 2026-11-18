# Fisga — Fluxo de review: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (native) ou
> superpowers:subagent-driven-development. Passos usam checkbox (`- [ ]`).

**Goal:** Documentar e depois implementar o fluxo de review do Fisga em camadas — determinístico
(CodeQL, dependency-review, reviewdog) → LLM (PR-Agent) → agente (`/oc`) — começando por
`Projetos/fisga/docs/review.md`, que passa a ser a fonte da verdade do processo.

**Architecture:** O documento é escrito primeiro e vira o contrato: tabela de status
(`implementado`/`planejado`), o que bloqueia merge e o que é advisory, e critérios de promoção. Cada
camada seguinte entra como workflow separado, sem tocar no job `gate`, que continua sendo o único
status check obrigatório. CodeQL bloqueia pela regra nativa `code_scanning` do ruleset, não por
status check.

**Tech Stack:** GitHub Actions, CodeQL Action v4, dependency-review-action v5, reviewdog v1.4,
PR-Agent v0.47 (Docker), OpenCode Action, OpenCode Go (`OPENCODE_API_KEY`), Coolify (inalterado).

**Spec:** desenho aprovado nesta sessão (pirâmide de review + 10 seções + fluxo alvo com status).
O próprio `review.md` passa a ser a spec das fases seguintes.

## Global Constraints

- Texto em pt-BR; seguir o formato de `docs/ci-cd.md` (título, visão geral, diagrama ASCII, tabelas,
  ponteiros para os arquivos reais).
- `gate` continua o **único** check obrigatório por status check. Nada de novo required check.
- Toda action nova entra pinada e registrada em `.github/workflows/actions.lock` via `gh actions-lock`.
- Push que altera `.github/workflows/**` exige token com escopo `workflow` (procedimento do AGENTS.md).
- Nenhum segredo, token ou ID interno no documento ou no repositório. Só nomes de secrets.
- Repo público: todo workflow que usa secrets precisa de gate de `author_association` e/ou de
  `head.repo.full_name == github.repository`.
- Não usar `paths:`/`paths-ignore` no gatilho de workflow que precise reportar check (invariante 3).
- Não mergear durante deploy; mudanças de workflow vão em PR próprio.

## Review Focus

- **PR só de docs** + CodeQL com `paths-ignore` no gatilho + merge protection: o workflow é pulado e
  o merge trava por "análise não executada". Teste: abrir PR só de markdown e confirmar que passa.
- **PR de fork** em repo público: secrets ausentes e token read-only. Teste: confirmar que o job é
  pulado com mensagem clara, não que falha.
- **`.astro` não é analisado** pelo CodeQL: o documento não pode prometer cobertura de frontend.
  Teste: conferir que o doc diz "`.ts`/`.js` em `web/src`, não `.astro`".
- **AI review ruidoso** vira check desligado na primeira semana. Teste: PR de smoke com bug
  plantado; medir quantos comentários são aceitos.
- **Franquia Go queimada** por `/oc` em repo público. Teste: comentar `/oc` com conta sem permissão e
  confirmar que o job não roda.

---

## File Structure

| Arquivo | Responsabilidade |
|---|---|
| `Projetos/fisga/docs/review.md` (novo) | Fonte da verdade do fluxo de review |
| `Projetos/fisga/docs/ci-cd.md` (editar) | Link para o review.md na visão geral |
| `.github/workflows/dependency-review.yml` | Supply chain no diff de dependências |
| `.github/codeql/codeql-config.yml` + `.github/workflows/codeql.yml` | Análise estática |
| `.github/workflows/reviewdog.yml` | Reporter de linter inline |
| `.pr_agent.toml` + `.github/workflows/pr-review.yml` | Revisor LLM |
| `.github/workflows/oc.yml` | Agente sob demanda |
| `.github/workflows/actions.lock` | Pins (gerado) |

---

## Task 0: Preparar execução

**Files:**
- Create: `Projetos/fisga/docs/superpowers/plans/2026-11-18-review-flow.md` (cópia deste plano)

**Interfaces:**
- Produces: branch `docs/fisga-review-flow` a partir de `main` atualizada.

- [ ] **Step 1: Atualizar main e criar branch**

```bash
git fetch origin
git switch main && git pull --ff-only
git switch -c docs/fisga-review-flow
```

- [ ] **Step 2: Copiar o plano para o repo e commitar**

```bash
cp ~/.opencode/plan/2026-11-18-fisga-review-flow.md \
   Projetos/fisga/docs/superpowers/plans/2026-11-18-review-flow.md
git add Projetos/fisga/docs/superpowers/plans/2026-11-18-review-flow.md
git commit -m "docs(fisga): plan review flow implementation"
```

Expected: `git status` limpo, branch com 1 commit.

---

## Task 1: Escrever `docs/review.md`

**Files:**
- Create: `Projetos/fisga/docs/review.md`

**Interfaces:**
- Consumes: fatos desta conversa (versões, custos, pegadinhas).
- Produces: 10 seções com títulos exatos listados abaixo; os workflows das fases seguintes citam
  este arquivo.

- [ ] **Step 1: Criar o arquivo com as 10 seções**

Título: `# Review do Fisga`. Seções, nesta ordem:

1. `## Princípio` — pirâmide determinístico → LLM → agente; regra "se a máquina decide com
   certeza, não gaste token"; o que cada camada pega que a anterior não pega.
2. `## Estado atual` — tabela `camada | ferramenta | status | bloqueia? | custo` com:
   gate (implementado, bloqueia, R$0), dependency-review (planejado, bloqueia, R$0),
   CodeQL (planejado, bloqueia via ruleset, R$0), reviewdog (planejado, advisory, R$0),
   PR-Agent (planejado, advisory, franquia Go), `/oc` (planejado, sob demanda, franquia Go).
3. `## Fluxo de um PR` — diagrama ASCII do autor até o merge, incluindo o caminho docs-only e fork.
4. `## O que bloqueia e o que é advisory` — razão de cada escolha; AI review nunca required.
5. `## Camadas` — subseção por camada com: quando roda, onde configura, como ler o resultado,
   comando de re-execução. Incluir os valores reais: `codeql-action@v4`,
   `dependency-review-action@v5` (`fail-on-severity: high`, `fail-on-scopes: runtime`),
   `reviewdog/action-setup@v1.4.0`, `the-pr-agent/pr-agent@v0.47.0`,
   `anomalyco/opencode/github` com `opencode-go/deepseek-v4.1-flash`.
6. `## Triagem` — tabela sintoma → ação para cada camada (`/review`, `/ask`, `/oc`, re-run,
   dismiss de alerta, `wontfix`).
7. `## Segurança do fluxo` — fork sem secrets, prompt injection (sem checkout em `pull_request_target`,
   `restricted_mode`, só branches internas), gasto de franquia, permissões mínimas por workflow,
   `author_association` para `/oc`.
8. `## Operação` — secrets (nomes), `actions.lock`, escopo `workflow`, custo real (repo público =
   runners grátis; CodeQL grátis; Go = franquia), como rodar `gh actions-lock`.
9. `## Métricas e promoção` — 2–3 semanas advisory; taxa de comentários aceitos; critérios para
   promover (≥50% útil) ou remover (<20%); critério explícito de nunca promover AI review a required.
10. `## Rollback e pendências` — deletar workflow, manter secret, e a lista da Fase B.

Regras de conteúdo:
- Sem prometer cobertura de `.astro` pelo CodeQL.
- Sem `paths-ignore` no gatilho do CodeQL se o merge protection estiver ativo; explicar o porquê.
- Nenhum valor de secret, token ou ID interno.

- [ ] **Step 2: Validar que as 10 seções existem**

```bash
grep -c '^## ' Projetos/fisga/docs/review.md
```

Expected: `10`.

- [ ] **Step 3: Validar os blocos YAML**

```bash
ruby -ryaml -e '
  text = File.read("Projetos/fisga/docs/review.md")
  blocks = text.scan(/```yaml\n(.*?)```/m).flatten
  blocks.each_with_index { |b, i| YAML.load(b); puts "bloco #{i + 1}: ok" }
  puts "total: #{blocks.size}"
'
```

Expected: todos `ok`, sem exceção.

- [ ] **Step 4: Validar links relativos**

```bash
cd Projetos/fisga/docs
grep -oE '\]\([^)#]+\)' review.md | sed 's/](//;s/)//' | while read -r f; do
  case "$f" in http*) continue;; esac
  [ -e "$f" ] || echo "quebrado: $f"
done
```

Expected: nenhuma linha `quebrado:`.

- [ ] **Step 5: Commit**

```bash
git add Projetos/fisga/docs/review.md
git commit -m "docs(fisga): add review flow runbook"
```

---

## Task 2: Linkar no `ci-cd.md`

**Files:**
- Modify: `Projetos/fisga/docs/ci-cd.md` (após a seção "Visão geral")

- [ ] **Step 1: Adicionar a linha de link**

```markdown
O fluxo de revisão de PRs (camadas determinísticas, LLM e agente) está em
[review.md](./review.md).
```

- [ ] **Step 2: Verificar o link e commitar**

```bash
test -f Projetos/fisga/docs/review.md && echo ok
rg -n "review.md" Projetos/fisga/docs/ci-cd.md
git add Projetos/fisga/docs/ci-cd.md
git commit -m "docs(fisga): link review flow from ci-cd"
```

Expected: `ok` e uma linha com `review.md`.

- [ ] **Step 3: Abrir PR e mergear (fase doc)**

```bash
git push -u origin docs/fisga-review-flow
gh pr create --repo AbsonDev/abson --base main --title "docs(fisga): review flow" \
  --body "Documenta o fluxo de review em camadas e o plano de implementação."
gh pr checks --repo AbsonDev/abson --watch
```

Expected: `gate` verde. Merge só depois de o deploy anterior terminar (invariante 7).

---

## Task 3: dependency-review (Fase 2)

**Files:**
- Create: `.github/workflows/dependency-review.yml`

**Interfaces:**
- Produces: check `dependency-review / review`; candidato a required check.

- [ ] **Step 1: Criar o workflow**

```yaml
name: dependency-review
on: [pull_request]
permissions:
  contents: read
  pull-requests: write
jobs:
  review:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: actions/dependency-review-action@v5
        with:
          fail-on-severity: high
          fail-on-scopes: runtime
          license-check: false
          comment-summary-in-pr: on-failure
```

- [ ] **Step 2: Atualizar o lock e validar**

```bash
gh actions-lock
git diff --stat .github/workflows/actions.lock
```

Expected: lock inclui `actions/dependency-review-action@v5` e o workflow.

- [ ] **Step 3: Testar em PR real**

Abrir PR de teste alterando `Projetos/fisga/web/package-lock.json` para uma versão com CVE conhecida
(ex.: pacote `lodash` antigo) só na branch de teste. Expected: check falha com a CVE listada; depois
fechar o PR sem mergear.

- [ ] **Step 4: Commit e PR**

```bash
git add .github/workflows/dependency-review.yml .github/workflows/actions.lock
git commit -m "ci: add dependency review"
git push -u origin ci/dependency-review
gh pr create --repo AbsonDev/abson --base main --title "ci: add dependency review" \
  --body "Bloqueia CVE high+ e licencas invalidas introduzidas no PR."
```

- [ ] **Step 5: Promover a required (após 1 semana advisory)**

```bash
gh api repos/AbsonDev/abson/rulesets/27557615 --jq '.rules[].type'
```

Adicionar `dependency-review / review` como required status check no ruleset `main-protection`.
Expected: `gh api .../rulesets/27557615` lista o novo contexto.

---

## Task 4: CodeQL — linha de base (Fase 3)

**Files:**
- Create: `.github/codeql/codeql-config.yml`
- Create: `.github/workflows/codeql.yml`

**Interfaces:**
- Produces: categorias `/language:csharp`, `/language:javascript-typescript`, `/language:actions`.
- Consumes: nada das tarefas anteriores.

- [ ] **Step 1: Criar a config**

```yaml
paths-ignore:
  - Projetos/cardapio-no-lucro
  - Projetos/precifica
  - apps
  - '**/node_modules'
  - '**/bin'
  - '**/obj'
  - '**/dist'
queries:
  - uses: security-extended
```

- [ ] **Step 2: Criar o workflow (só push + schedule nesta fase)**

```yaml
name: codeql
on:
  push:
    branches: [main]
  schedule:
    - cron: '17 6 * * 1'
permissions:
  contents: read
  security-events: write
  actions: read
concurrency:
  group: codeql-${{ github.ref }}
  cancel-in-progress: true
jobs:
  analyze:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    strategy:
      fail-fast: false
      matrix:
        include:
          - language: csharp
            build-mode: none
          - language: javascript-typescript
            build-mode: none
          - language: actions
            build-mode: none
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: github/codeql-action/init@v4
        with:
          languages: ${{ matrix.language }}
          build-mode: ${{ matrix.build-mode }}
          config-file: ./.github/codeql/codeql-config.yml
      - uses: github/codeql-action/analyze@v4
        with:
          category: /language:${{ matrix.language }}
```

- [ ] **Step 3: Validar YAML e lock**

```bash
ruby -ryaml -e 'YAML.load_file(".github/workflows/codeql.yml"); YAML.load_file(".github/codeql/codeql-config.yml"); puts "ok"'
gh actions-lock && git diff --stat .github/workflows/actions.lock
```

Expected: `ok`, lock atualizado.

- [ ] **Step 4: Push e observar o primeiro run**

```bash
git add .github/workflows/codeql.yml .github/codeql/codeql-config.yml .github/workflows/actions.lock
git commit -m "ci: add codeql baseline scan"
git push -u origin ci/codeql-baseline
gh run list --repo AbsonDev/abson --workflow codeql.yml --limit 3
```

Expected: 3 jobs `analyze` concluídos; se `csharp` falhar com "no source code seen", trocar
`build-mode` para `manual` e adicionar `dotnet build` (SDK 10 já é o do CI).

- [ ] **Step 5: Triagem da linha de base**

Listar alertas:

```bash
gh api "repos/AbsonDev/abson/code-scanning/alerts?state=open&per_page=100" \
  --jq '.[] | "\(.rule.severity) \(.rule.id) \(.most_recent_instance.location.path)"'
```

Expected: lista de alertas. Triar cada um como `corrigir`, `dismiss (false positive | won't fix)` ou
`aceitar`. Registrar a contagem no fim do `review.md` (seção Estado atual).

---

## Task 5: CodeQL em PR + merge protection (Fase 4)

**Files:**
- Modify: `.github/workflows/codeql.yml` (adicionar gatilho `pull_request`)

- [ ] **Step 1: Adicionar o gatilho**

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    - cron: '17 6 * * 1'
```

Não adicionar `paths-ignore` no gatilho. O motivo vai no `review.md`.

- [ ] **Step 2: Adicionar a regra `code_scanning` ao ruleset**

```bash
gh api -X PUT repos/AbsonDev/abson/rulesets/27557615 \
  --input <(gh api repos/AbsonDev/abson/rulesets/27557615 | jq '.rules += [{
    "type": "code_scanning",
    "parameters": {"code_scanning_tools": [{
      "tool": "CodeQL",
      "security_alerts_threshold": "high_or_higher",
      "alerts_threshold": "errors"
    }]}
  }]')
```

Expected: `gh api repos/AbsonDev/abson/rulesets/27557615 --jq '.rules[].type'` inclui `code_scanning`.

- [ ] **Step 3: Testar os três caminhos**

1. PR com bug plantado (ex.: concatenação de SQL) → deve bloquear.
2. PR só de markdown → deve permitir (prova que não há `paths-ignore` no gatilho).
3. PR de fork → deve rodar e reportar sem secrets.

Expected: 1 bloqueia, 2 passa, 3 reporta.

---

## Task 6: reviewdog (Fase 5)

**Files:**
- Create: `.github/workflows/reviewdog.yml`

- [ ] **Step 1: Criar o workflow com actionlint e sugestões**

```yaml
name: reviewdog
on: pull_request
permissions:
  contents: read
  pull-requests: write
jobs:
  lint:
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v7.0.1
        with:
          fetch-depth: 0
      - uses: reviewdog/action-setup@v1.4.0
      - uses: reviewdog/action-actionlint@v1
        with:
          reporter: github-pr-review
          fail_level: error
```

- [ ] **Step 2: Validar e commitar**

```bash
ruby -ryaml -e 'YAML.load_file(".github/workflows/reviewdog.yml"); puts "ok"'
gh actions-lock
git add .github/workflows/reviewdog.yml .github/workflows/actions.lock
git commit -m "ci: add reviewdog for workflow lint"
git push -u origin ci/reviewdog
```

Expected: `ok`; PR mostra anotação inline se houver achado no diff.

- [ ] **Step 3: Validar que não bloqueia**

Expected: check pode ficar vermelho, mas o merge continua possível (não é required). Confirmar em
`gh pr checks`.

---

## Task 7: PR-Agent + OpenCode Go (Fase 6)

**Files:**
- Create: `.pr_agent.toml`
- Create: `.github/workflows/pr-review.yml`

**Interfaces:**
- Consumes: secret `OPENCODE_API_KEY` (criado manualmente pelo humano).
- Produces: comando `/review`, `/describe`, `/ask` no PR.

- [ ] **Step 0 (humano): criar a chave**

Console do OpenCode → Account → Personal API keys → workspace com Go ativo. Salvar como secret:

```bash
gh secret set OPENCODE_API_KEY --repo AbsonDev/abson
```

Confirmar o id do modelo:

```bash
curl -fsS https://opencode.ai/inference/go/v1/models \
  -H "Authorization: Bearer $OPENCODE_API_KEY" | jq -r '.data[].id' | grep -i deepseek
```

Expected: um id contendo `deepseek-v4.1-flash`. Se o workspace não tiver `Regions: Global`, o modelo
vem bloqueado — habilitar em Settings → Privacy.

- [ ] **Step 1: Criar `.pr_agent.toml`**

```toml
[config]
restricted_mode = true
response_language = "pt-BR"

[pr_reviewer]
num_max_findings = 6
require_tests_review = false
extra_instructions = """
Monorepo Abson. Só Projetos/fisga/** está em produção.
Priorize bugs de lógica, segurança (OAuth/callback, secrets), contratos de API,
migrações e violação das invariantes de CI/CD do AGENTS.md.
Ignore estilo já coberto por dotnet format / astro check. Não comente em docs/**.
"""
```

- [ ] **Step 2: Criar o workflow**

Antes de pinar, confirmar que a tag existe: `git ls-remote --tags https://github.com/The-PR-Agent/pr-agent 'refs/tags/v0.47.*'`.
Se não existir, usar a última tag de release publicada e ajustar o pin.

```yaml
name: pr-review
on:
  pull_request:
    types: [opened, reopened, ready_for_review]
  issue_comment:
    types: [created]
permissions:
  contents: read
  issues: write
  pull-requests: write
concurrency:
  group: pr-review-${{ github.event.pull_request.number || github.event.issue.number }}
  cancel-in-progress: true
jobs:
  review:
    if: |
      github.event.sender.type != 'Bot' &&
      (github.event_name == 'pull_request' || github.event.issue.pull_request) &&
      (github.event_name != 'pull_request' ||
       github.event.pull_request.head.repo.full_name == github.repository)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: the-pr-agent/pr-agent@v0.47.0
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          OPENAI.KEY: ${{ secrets.OPENCODE_API_KEY }}
          OPENAI.API_BASE: https://opencode.ai/inference/go/v1
          config.model: openai/deepseek-v4.1-flash
          config.custom_model_max_tokens: "128000"
          config.fallback_models: '[]'
          github_action_config.auto_review: "true"
          github_action_config.auto_describe: "false"
          github_action_config.auto_improve: "false"
```

- [ ] **Step 3: Spike em PR de teste (prova de compatibilidade Go ↔ LiteLLM)**

Abrir PR `test/pr-review-smoke` com bug plantado. Expected: comentário em < 3 min.
Se o container falhar com erro de provider/modelo, registrar no `review.md` que a fase 6 está
bloqueada e aplicar o fallback (OpenCode Action como revisor, Task 8).

- [ ] **Step 4: Rodar os comandos no PR real**

```bash
gh pr comment 18 --repo AbsonDev/abson --body "/review"
gh pr comment 18 --repo AbsonDev/abson --body "/ask este PR trata o callback do Google sem 500?"
```

Expected: dois comentários de resposta do bot.

- [ ] **Step 5: Commit e PR**

```bash
gh actions-lock
git add .pr_agent.toml .github/workflows/pr-review.yml .github/workflows/actions.lock
git commit -m "ci: add pr-agent review with opencode go"
git push -u origin ci/pr-review
```

---

## Task 8: OpenCode `/oc` (Fase 7)

**Files:**
- Create: `.github/workflows/oc.yml`

- [ ] **Step 1: Criar o workflow com gate de permissão**

```yaml
name: oc
on:
  issue_comment:
    types: [created]
  pull_request_review_comment:
    types: [created]
permissions:
  contents: write
  issues: write
  pull-requests: write
jobs:
  oc:
    if: |
      contains(github.event.comment.body, '/oc') &&
      contains(fromJSON('["OWNER","MEMBER","COLLABORATOR"]'), github.event.comment.author_association)
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v7.0.1
      - run: |
          git config --global user.name "github-actions[bot]"
          git config --global user.email "41898282+github-actions[bot]@users.noreply.github.com"
      - uses: anomalyco/opencode/github@latest
        env:
          OPENCODE_API_KEY: ${{ secrets.OPENCODE_API_KEY }}
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        with:
          model: opencode-go/deepseek-v4.1-flash
          variant: max
          use_github_token: true
```

- [ ] **Step 2: Pinar a action no lock e validar**

```bash
gh actions-lock
git diff --stat .github/workflows/actions.lock
```

Expected: `anomalyco/opencode/github` pinada por SHA.

- [ ] **Step 3: Testar o gate de permissão**

Comentar `/oc` numa conta sem permissão (ou simular `author_association: NONE` num PR de teste).
Expected: job **não** roda.

- [ ] **Step 4: Testar o fluxo feliz**

Comentar `/oc explique o diff deste PR` num PR interno. Expected: resposta em até 30 min, sem commit
inesperado.

- [ ] **Step 5: Commit e PR**

```bash
git add .github/workflows/oc.yml .github/workflows/actions.lock
git commit -m "ci: add on-demand opencode agent"
git push -u origin ci/opencode-oc
```

---

## Task 9: Fechar o ciclo (2 semanas depois)

- [ ] **Step 1: Coletar números**

```bash
gh pr list --repo AbsonDev/abson --state merged --limit 20 --json number,title,reviews \
  --jq '.[] | "\(.number) \(.reviews | length)"'
```

Contar comentários do PR-Agent aceitos vs ignorados manualmente. Registrar no `review.md`.

- [ ] **Step 2: Decidir**

| Taxa de aceitação | Ação |
|---|---|
| ≥ 50% | manter; considerar subir `num_max_findings` |
| 20–50% | ajustar `extra_instructions`, reduzir findings |
| < 20% | remover a camada (deletar workflow) |

- [ ] **Step 3: Atualizar a tabela de status do `review.md` e commitar**

```bash
git add Projetos/fisga/docs/review.md
git commit -m "docs(fisga): record review flow metrics"
```

---

## Verification Before Completion

Antes de dizer que qualquer fase terminou:

```bash
ruby -ryaml -e 'Dir[".github/workflows/*.yml"].each { |f| YAML.load_file(f) }; puts "workflows ok"'
gh actions-lock && git diff --exit-code .github/workflows/actions.lock
gh pr checks --repo AbsonDev/abson --watch
curl -fsS https://api.fisga.abson.tech/health
curl -fsS https://fisga.abson.tech/health
```

Expected: YAML válido, lock em dia, `gate` verde, `/health` respondendo com o SHA esperado.

## Rollback

| Camada | Reversão |
|---|---|
| dependency-review | deletar `.github/workflows/dependency-review.yml`; remover required check do ruleset |
| CodeQL | deletar `codeql.yml`; remover regra `code_scanning` do ruleset; alertas ficam no histórico |
| reviewdog | deletar `.github/workflows/reviewdog.yml` |
| PR-Agent | deletar `.github/workflows/pr-review.yml` e `.pr_agent.toml`; revogar `OPENCODE_API_KEY` se não for usar no `/oc` |
| `/oc` | deletar `.github/workflows/oc.yml` |

Nenhum rollback exige mudança no `fisga-pipeline.yml` ou no `gate`.
