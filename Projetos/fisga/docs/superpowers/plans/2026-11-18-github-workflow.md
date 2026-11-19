# GitHub Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer o GitHub Projects/Issues/PRs ser o centro de comando do Fisga, com entrada privada por drafts, promoção para issues, PRs ligados, sincronização de Status e merge disciplinado com os revisores automáticos já existentes.

**Architecture:** Um script Node sem dependências (`scripts/github-project-sync.mjs`) usa a CLI `gh` com um secret `FISGA_PROJECT_TOKEN` (escopo `project`) para adicionar itens ao quadro 6 e ajustar campos. Um workflow leve dispara em `issues.opened` e `pull_request.opened/closed`. Templates de issue/PR e documentação fecham o ciclo; o ruleset passa a exigir resolução de threads.

**Tech Stack:** GitHub Projects v2 + `gh` CLI, GitHub Actions (Node 24, `node:test`), `jq`, Ruby (validação YAML local), `gh actions-lock`, pt-BR.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-18-github-workflow-design.md`

## Global Constraints

- Todo código de produção do Fisga continua em `Projetos/fisga/**`; CI sem filtro `paths:`.
- O check obrigatório da `main` é o contexto **`gate`** — nunca `fisga-pipeline / gate`.
- `fisga-deploy` continua disparando só por `workflow_run` com `conclusion == success` na `main`.
- Nunca mergear outro PR durante um deploy; nunca usar `workflow_dispatch` de deploy.
- Nenhum token, PAT ou segredo em arquivo versionado, log ou chat. O secret do quadro chama-se `FISGA_PROJECT_TOKEN`.
- Todo workflow novo precisa ter actions pinadas e `gh actions-lock` executado.
- Copy, comentários e documentação em pt-BR.
- Quadros: `owner = AbsonDev`, `project number = 6`, id `PVT_kwHODKV4_c4BsOkA`.
- Status/Priority/Size/Iteration devem ser preenchidos pelos nomes exibidos no quadro.

## Review Focus

- **Issue aberta por bot** — não pode criar item nem disparar loop; workflow ignora `sender.type == 'Bot'`. Teste: Task 3, validação do `if:` no workflow.
- **PR de fork** — não pode receber o secret; workflow só roda quando `head.repo.full_name == github.repository`. Teste: Task 3, validação do `if:` no workflow.
- **Rerun do seed** — não pode duplicar drafts; `findDuplicateTitles()` é testado em Task 2 e usado em Task 6.
- **PR fechado sem merge** — não pode virar Done; `mapEventToStatus()` só retorna `Done` com `merged === true` (Task 2).
- **Token sem escopo `project`** — deve falhar com mensagem clara, não silenciosamente; `assertProjectToken()` é testado em Task 2 e chamado no início do CLI.

---

### Task 1: Label `ux` e templates de issue/PR

**Files:**
- Create: `.github/ISSUE_TEMPLATE/fisga.yml`
- Create: `.github/PULL_REQUEST_TEMPLATE.md`

**Interfaces:**
- Consumes: nada.
- Produces: label `ux` no repo; template de issue com campos `resumo`, `area`, `evidencia`, `reproducao`, `esperado`, `aceite`, `artefato`; template de PR com `Closes #`, checklist de review.

- [ ] **Step 1: Criar a label `ux` via API**

Run:
```bash
gh label create ux --repo AbsonDev/abson --color "D9773F" --description "Achado de UX/UI do Fisga" 2>/dev/null || true
```
Expected: comando termina sem erro; se a label já existir, nada muda.

- [ ] **Step 2: Escrever o template de issue**

Crie `.github/ISSUE_TEMPLATE/fisga.yml` como GitHub issue form (`name`, `description`, `title`, `labels: [ux]`, `body`) com os campos, em pt-BR: resumo (textarea), área (dropdown `API, Web, UX, Docs`), evidência (textarea: URL, `arquivo:linha`, print), reprodução (textarea), esperado (textarea), critério de aceite (textarea), artefato (input opcional: link do OpenDesign).

- [ ] **Step 3: Escrever o template de PR**

Crie `.github/PULL_REQUEST_TEMPLATE.md` com: link `Closes #123`, resumo da mudança, área afetada, checklist `- [ ] gate verde`, `- [ ] threads respondidas e resolvidas`, `- [ ] screenshot antes/depois (quando visual)`, `- [ ] /health conferido após deploy`.

- [ ] **Step 4: Validar os arquivos**

Run:
```bash
ruby -ryaml -e 'YAML.load_file(ARGV[0]); puts "issue form ok"' .github/ISSUE_TEMPLATE/fisga.yml
test -s .github/PULL_REQUEST_TEMPLATE.md && echo "PR template ok"
gh api repos/AbsonDev/abson/labels/ux --jq '.name'
```
Expected: `issue form ok`, `PR template ok`, `ux`.

- [ ] **Step 5: Commit**

```bash
git add .github/ISSUE_TEMPLATE/fisga.yml .github/PULL_REQUEST_TEMPLATE.md
git commit -m "chore(github): add Fisga issue/PR templates and ux label"
```

---

### Task 2: Script de sincronização do quadro com testes

**Files:**
- Create: `scripts/github-project-sync.mjs`
- Create: `scripts/github-project-sync.test.mjs`

**Interfaces:**
- Consumes: `gh` CLI autenticado com `FISGA_PROJECT_TOKEN`.
- Produces:
  - `mapEventToStatus(eventName: string, payload: object): 'Backlog' | 'In review' | 'Done' | null`
  - `findDuplicateTitles(existingTitles: string[], incomingTitles: string[]): string[]`
  - `parseSeed(raw: string): SeedItem[]` onde `SeedItem = { title, area, evidencia, impacto, aceite, priority, size, iteration }`
  - `buildDraftBody(item: SeedItem): string` com seções `## Evidência`, `## Impacto`, `## Critério de aceite`
  - `assertProjectToken(): void` — lê `GH_TOKEN`/`GITHUB_TOKEN` do ambiente e lança erro com mensagem pt-BR se ausente
  - CLI: `event-status` (lê `GITHUB_EVENT_PATH`, imprime o Status), `add-or-sync <url> <status>`, `seed <arquivo.json>`

- [ ] **Step 1: Escrever os testes que falham**

Crie `scripts/github-project-sync.test.mjs` com `node:test` e asserções:
- `mapEventToStatus('issues', {action:'opened'}) === 'Backlog'`
- `mapEventToStatus('pull_request', {action:'opened'}) === 'In review'`
- `mapEventToStatus('pull_request', {action:'closed', merged:true}) === 'Done'`
- `mapEventToStatus('pull_request', {action:'closed', merged:false}) === null`
- `findDuplicateTitles(['A','B'], ['B','C']) deepEqual ['B']`
- `parseSeed(JSON.stringify([{title:'X', area:'Web', evidencia:'e', impacto:'i', aceite:'a', priority:'P0', size:'S', iteration:'Iteration 1'}]))` retorna 1 item com `title === 'X'`
- `parseSeed('[{"title":"X"}]')` lança erro contendo `evidencia`
- `buildDraftBody(item)` contém `## Evidência`, `## Impacto`, `## Critério de aceite` e o valor de `aceite`
- `assertProjectToken()` lança erro contendo `FISGA_PROJECT_TOKEN` quando `delete process.env.GH_TOKEN` e `delete process.env.GITHUB_TOKEN`

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `node --test scripts/github-project-sync.test.mjs`
Expected: FAIL com módulo/exportações ausentes.

- [ ] **Step 3: Implementar o script**

Implemente em `scripts/github-project-sync.mjs`:
- Constantes `OWNER = 'AbsonDev'`, `PROJECT_NUMBER = 6`; mapa `ITERATION_IDS` com `Iteration 1 → 381c7c80`, `Iteration 2 → 54cf5c95`, `Iteration 3 → d2c335bc` (IDs lidos do GraphQL do quadro em 18/11/2026).
- `add-or-sync`: `gh project item-add 6 --owner AbsonDev --url <url> --format json` para obter o `id`; depois gravar o Status pelo caminho de IDs da CLI: `gh project item-edit --id <id> --field-id <statusFieldId> --project-id <PROJECT_ID> --single-select-option-id <optionId>`. Se o item já existir, reutilizar o id retornado em vez de criar outro.
- `seed`: usar `gh project item-list 6 --owner AbsonDev --format json` (forma `{items, totalCount}`, título em `.items[].content.title`) com `findDuplicateTitles()` para idempotência; resolver `field-list` e os IDs de Status/Priority/Size/Iteration **antes** de qualquer `item-create`; para cada item novo, `gh project item-create 6 --owner AbsonDev --title ... --body ... --format json`, depois `item-edit` por `--id/--field-id/--project-id/--single-select-option-id` (Status/Priority/Size) e `--iteration-id` (Iteration). `PROJECT_ID = PVT_kwHODKV4_c4BsOkA`.
- `assertProjectToken()` chamado no início de `add-or-sync` e `seed`.
- `event-status` não precisa de token.

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `node --test scripts/github-project-sync.test.mjs`
Expected: PASS (9 testes).

- [ ] **Step 5: Commit**

```bash
git add scripts/github-project-sync.mjs scripts/github-project-sync.test.mjs
git commit -m "feat(github): sync Fisga project items with gh CLI"
```

---

### Task 3: Workflow `project-sync`

**Files:**
- Create: `.github/workflows/project-sync.yml`
- Modify: `.github/workflows/actions.lock` (gerado por `gh actions-lock`)

**Interfaces:**
- Consumes: script e secret da Task 2; `GH_TOKEN: ${{ secrets.FISGA_PROJECT_TOKEN }}`.
- Produces: itens adicionados/atualizados no quadro 6 quando issue abre ou PR abre/fecha.

- [ ] **Step 1: Escrever o workflow**

Crie `.github/workflows/project-sync.yml` com:
- `name: project-sync`
- `on: issues: {types: [opened]}`, `pull_request: {types: [opened, closed]}`
- `permissions: contents: read`
- `concurrency: group: project-sync-${{ github.event.issue.number || github.event.pull_request.number }}`, `cancel-in-progress: false`
- job `sync` com `if: github.event.sender.type != 'Bot' && (github.event_name == 'issues' || github.event.pull_request.head.repo.full_name == github.repository)`
- steps: `actions/checkout@v7.0.1`, `actions/setup-node@v7.1.0` com `node-version: 24`, e um step `run` que:
  1. `STATUS="$(node scripts/github-project-sync.mjs event-status)"`
  2. se não vazio, chama `node scripts/github-project-sync.mjs add-or-sync "$URL" "$STATUS"` com `URL="${{ github.event.issue.html_url || github.event.pull_request.html_url }}"`.
- `env` do step: `GH_TOKEN: ${{ secrets.FISGA_PROJECT_TOKEN }}`, `GITHUB_EVENT_PATH: ${{ github.event_path }}`.

- [ ] **Step 2: Validar YAML e o `if:`**

Run:
```bash
ruby -ryaml -e 'YAML.load_file(ARGV[0]); puts "workflow ok"' .github/workflows/project-sync.yml
grep -q "github.event.sender.type != 'Bot'" .github/workflows/project-sync.yml
grep -q "head.repo.full_name == github.repository" .github/workflows/project-sync.yml
```
Expected: `workflow ok` e os dois greps sem erro.

- [ ] **Step 3: Pinar as actions**

Run: `gh actions-lock`
Expected: lockfile atualizado sem erro.

- [ ] **Step 4: Verificar idempotência do lock**

Run: `gh actions-lock --no-fix && echo "lock ok"`
Expected: `lock ok` e nenhuma alteração no lockfile.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/project-sync.yml .github/workflows/actions.lock
git commit -m "ci: add project-sync workflow with pinned actions"
```

---

### Task 4: Ruleset exigir resolução de threads

**Files:** nenhum (mudança remota de configuração). Registro em `Projetos/fisga/docs/github-workflow.md` (Task 5).

**Interfaces:**
- Consumes: ruleset `main-protection` id `27557615`.
- Produces: `required_review_thread_resolution = true`, com as demais regras intactas.

- [ ] **Step 1: Gravar o estado atual e aplicar o patch**

Run:
```bash
TMPDIR_WORK="${TMPDIR:-/tmp}/fisga-ruleset-$$"
mkdir -p "$TMPDIR_WORK"
gh api repos/AbsonDev/abson/rulesets/27557615 > "$TMPDIR_WORK/before.json"
jq '{name,target,enforcement,bypass_actors,conditions,rules}
    | .rules |= map(if .type == "pull_request"
                    then .parameters.required_review_thread_resolution = true
                    else . end)' "$TMPDIR_WORK/before.json" > "$TMPDIR_WORK/after.json"
gh api -X PUT repos/AbsonDev/abson/rulesets/27557615 --input "$TMPDIR_WORK/after.json"
rm -rf "$TMPDIR_WORK"
```
Expected: resposta 200 com o ruleset atualizado.

- [ ] **Step 2: Verificar sem afetar outras regras**

Run:
```bash
gh api repos/AbsonDev/abson/rulesets/27557615 \
  --jq '{enforcement, required: .rules[] | select(.type=="pull_request") | .parameters.required_review_thread_resolution, contexts: [.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context], types: [.rules[].type]}'
```
Expected: `enforcement = active`, `required = true`, `contexts` contém `gate`, `types` contém `pull_request`, `required_status_checks`, `non_fast_forward`, `deletion`.

- [ ] **Step 3: Registrar**

Sem commit: mudança é remota. Anotar em `docs/github-workflow.md` que a resolução de threads passou a ser obrigatória em 18/11/2026.

---

### Task 5: Documentação do fluxo

**Files:**
- Create: `Projetos/fisga/docs/github-workflow.md`
- Modify: `Projetos/fisga/docs/ci-cd.md` (linha 8, após o parágrafo de validações)
- Modify: `Projetos/fisga/docs/review.md` (seção de revisores automáticos)

**Interfaces:**
- Consumes: decisões da spec; ruleset da Task 4; script da Task 2.
- Produces: runbook do fluxo para humanos e agentes.

- [ ] **Step 1: Escrever o documento**

Crie `Projetos/fisga/docs/github-workflow.md` com: diagrama do fluxo (draft → issue → branch → PR → CI/review → merge → deploy → Done), convenção de branches (`fix/123-<slug>` / `feat/123-<slug>`), convenção de labels, preenchimento de campos do quadro, checklist obrigatório de merge (gate, threads resolvidas via API com `isResolved: true`, screenshots, `/health`), como acionar `/review`, `/ask`, `/describe`, `/improve` e `/oc`, comandos do script (`event-status`, `add-or-sync`, `seed`), configuração do secret `FISGA_PROJECT_TOKEN` (escopo `project`), métricas (tempo issue→PR, PR→deploy, itens por iteração, merges com thread aberta) e a regra de nunca colar token em chat/arquivo.

- [ ] **Step 2: Linkar a partir dos docs existentes**

Adicione uma linha em `ci-cd.md` após o parágrafo das validações de referência e uma linha em `review.md` na seção de revisores automáticos, ambas apontando para `github-workflow.md`.

- [ ] **Step 3: Verificar os links**

Run:
```bash
grep -q "github-workflow.md" Projetos/fisga/docs/ci-cd.md
grep -q "github-workflow.md" Projetos/fisga/docs/review.md
test -s Projetos/fisga/docs/github-workflow.md && echo "docs ok"
```
Expected: dois greps sem erro e `docs ok`.

- [ ] **Step 4: Commit**

```bash
git add Projetos/fisga/docs/github-workflow.md Projetos/fisga/docs/ci-cd.md Projetos/fisga/docs/review.md
git commit -m "docs(fisga): document GitHub workflow and thread resolution"
```

---

### Task 6: Semear o quadro com os achados do `/registrar`

**Files:**
- Create (privado, fora do versionamento): `.superpowers/sdd/2026-11-18-github-workflow/github-project-seed.json`

**Interfaces:**
- Consumes: `parseSeed()`, `buildDraftBody()`, `findDuplicateTitles()` e comando `seed` da Task 2.
- Produces: 10 drafts no quadro 6, com Priority/Size/Iteration.

- [ ] **Step 1: Escrever o seed privado com os 10 achados**

O repositório é público e o quadro é privado: títulos, evidências e impacto crus não entram em
arquivo versionado. Crie o JSON em
`.superpowers/sdd/2026-11-18-github-workflow/github-project-seed.json` (diretório git-ignorado),
usando os 10 achados da auditoria do `/registrar` registrados no brief da Task 6. Cada item tem os
oito campos obrigatórios `title`, `area`, `evidencia`, `impacto`, `aceite`, `priority`, `size` e
`iteration`; cada `aceite` deve ser verificável.

- [ ] **Step 2: Validar o seed localmente**

Run: `node -e "import('./scripts/github-project-sync.mjs').then(m => console.log(m.parseSeed(require('fs').readFileSync('.superpowers/sdd/2026-11-18-github-workflow/github-project-seed.json','utf8')).length))"`
Expected: `10`.

- [ ] **Step 3: Confirmar que o seed não entra no repositório**

Run:
```bash
git check-ignore -v .superpowers/sdd/2026-11-18-github-workflow/github-project-seed.json
git ls-files | grep github-project-seed || echo "nenhum seed versionado"
```
Expected: o caminho aparece como ignorado por `.superpowers/sdd/.gitignore` e a segunda linha imprime
`nenhum seed versionado`.

- [ ] **Step 4: Rodar o seed no quadro real**

Pré-requisito: `FISGA_PROJECT_TOKEN` com escopo `project` disponível no ambiente. Se não estiver, pare e peça ao usuário — nunca reutilize token de chat.

Run: `node scripts/github-project-sync.mjs seed .superpowers/sdd/2026-11-18-github-workflow/github-project-seed.json`
Expected: 10 itens criados; segunda execução não cria duplicados.

- [ ] **Step 5: Verificar no quadro**

Run:
```bash
gh project item-list 6 --owner AbsonDev --format json \
  --jq '{total: .totalCount, titles: [.items[] | (.content.title // .title)]}'
```
Expected: `total >= 10` e os 10 títulos do seed presentes.

- [ ] **Step 6: Confirmar que nada foi versionado**

Run: `git status --short`
Expected: sem saída; o seed permanece só no diretório git-ignorado.
