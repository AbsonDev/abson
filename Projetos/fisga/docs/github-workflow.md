# Fluxo de trabalho no GitHub

Runbook do caminho que vai do achado ao código em produção no Fisga: draft, issue, branch, PR,
revisão, merge, deploy e `Done`. As camadas de CI e CD estão em [ci-cd.md](./ci-cd.md); o
julgamento do diff está em [review.md](./review.md). Aqui está o processo que liga os dois.

**Status em 19/11/2026:** templates de issue/PR e label `ux` criados; script e workflow
`project-sync` implementados e mesclados no PR #39 (`a6ca541`); o ruleset `main-protection` passou a
**exigir resolução de threads** em 18/11/2026. O secret `FISGA_PROJECT_TOKEN` foi criado com escopo
`project` e validado pelo `project-sync` disparado no merge (`44442041826`). O deploy continua
exclusivamente no `fisga-deploy`.

## Princípio

- Todo achado vira item rastreável. Nada fica só no chat.
- Draft é privado, issue é pública. Achado cru é triado antes de virar issue.
- Todo PR de trabalho fecha uma issue (`Closes #N`) e a branch carrega o número da issue.
- O quadro reflete o estado real; a automação preenche o que consegue e o resto é manual.
- Merge só com `gate` verde e **todas** as threads respondidas e resolvidas.
- Deploy só pelo `fisga-deploy`; o `Done` do sync marca **mergeado**, e o `/health` no SHA mergeado
  continua sendo checklist obrigatório. Ligar deploy→quadro é Fase B.

## Visão geral

```text
achado (uso real, auditoria, conversa)
└── draft no quadro 6 (privado)
    └── issue em AbsonDev/abson ................ project-sync grava Backlog
        └── branch fix/123-<slug> .............. número da issue
            └── PR com "Closes #123" ........... project-sync grava In review
                ├── gate ....................... check obrigatório (strict)
                ├── revisores automáticos ...... PR-Agent, CodeQL, dependency-review, reviewdog
                ├── /oc ........................ sob demanda, pode commitar
                └── threads respondidas e resolvidas (isResolved: true)
                    └── merge na main .......... item do PR vira Done (mergeado)
                        └── fisga-deploy ....... workflow_run, /health = SHA mergeado
                            └── verificação .... confirme o /health; deploy→quadro é Fase B
```

O que move o Status de cada item do quadro:

| Evento | Status gravado | Origem |
| --- | --- | --- |
| Issue aberta por pessoa | `Backlog` | `project-sync` |
| Draft promovido a issue | `Backlog` (sobrescreve o Status atual) | `project-sync` — ver limitações |
| PR aberto (inclusive draft) | `In review` | `project-sync` |
| PR mergeado e fechado | `Done` (mergeado) | `project-sync` |
| PR fechado sem merge | nada | limitação conhecida |
| PR de fork | nada | job ignorado |
| `Ready` e `In progress` | manual | quadro |
| Item da issue após o merge | manual ou automação nativa do Projects | o sync atualiza só o item do PR |

## Do achado ao draft

Um draft precisa responder quatro perguntas, na ordem:

1. **O que eu vi** — o problema em uma ou duas frases; é o título do draft.
2. **Evidência** — URL, `arquivo:linha`, print ou gravação. Sem evidência, o achado não entra.
3. **Impacto no usuário** — o que a pessoa deixa de fazer, erra ou sofre.
4. **Critério de aceite** — verificações objetivas de que está resolvido.

O corpo do draft segue o formato do script:

```markdown
**Área:** Web

## Evidência
https://fisga.abson.tech/condicoes
web/src/pages/condicoes.astro:42

## Impacto
No celular, a pessoa não vê a fonte do dado e não confia na condição.

## Critério de aceite
- Fonte e horário visíveis no mobile
- Sem regressão no desktop
```

O quadro 6 é privado e o repositório é público. Draft é o lugar de crítica crua, rascunho e
hipótese; a issue é o lugar do que foi acordado. Não publique o draft inteiro sem triagem.

**Promoção:** abra o item no quadro e use **Convert to issue**, escolhendo `AbsonDev/abson`. A issue
herda o item (com os campos já preenchidos), mas o evento `issues.opened` grava `Backlog`; ajuste
o Status manualmente depois.

## Issue

O template fica em `.github/ISSUE_TEMPLATE/fisga.yml` e pede resumo, área (`API`, `Web`, `UX`,
`Docs`), evidência, reprodução, esperado, critério de aceite e link de artefato do OpenDesign. O
template aplica a label `ux`.

Uma issue = um problema. Se o achado tem duas causas independentes, abra duas issues e ligue os
PRs correspondentes. Não reutilize uma issue para uma mudança que não fecha o critério de aceite
original.

## Quadro (Projects 6)

- Owner: `AbsonDev`; projeto número `6`; id `PVT_kwHODKV4_c4BsOkA`; quadro privado.
- Campos do projeto usados pelo script: `Status`, `Priority`, `Size` e `Iteration`.
- Título e corpo vêm do draft ou do template. `Área` não é campo do quadro: entra no corpo como
  `**Área:**` e no checkbox do template de PR.

| Campo | Valores aceitos | Quem preenche |
| --- | --- | --- |
| `Status` | `Backlog`, `Ready`, `In progress`, `In review`, `Done` | `project-sync` nos eventos; o resto manual |
| `Priority` | `P0`, `P1`, `P2` | `seed` e manual |
| `Size` | `XS`, `S`, `M`, `L`, `XL` | `seed` e manual |
| `Iteration` | `Iteration 1`, `Iteration 2`, `Iteration 3` | `seed` e manual |

Os nomes precisam ser exatamente os exibidos no quadro. Se uma opção for renomeada ou removida, o
script falha antes de escrever e lista as opções disponíveis — não invente valor novo no JSON.

Para ler o quadro pela CLI local é preciso escopo de leitura de projects:

```bash
gh auth refresh -s read:project

gh project field-list 6 --owner AbsonDev --format json --limit 100 \
  --jq '.fields[] | {name, type, options: [.options[]?.name]}'

gh project item-list 6 --owner AbsonDev --format json --limit 100 \
  --jq '.items[] | {
    title: (.content.title // .title),
    status,
    priority,
    size,
    iteration: (.iteration.title // "sem iteração")
  }'
```

### Limitações conhecidas do sync

1. **PR fechado sem merge fica em `In review`.** O script não rebaixa o Status; mova o item para
   `Backlog` ou `Ready` manualmente e registre o motivo no PR.
2. **PR de fork mesclado nunca vai para `Done` pelo sync.** O job ignora fork (o evento não recebe
   o secret); mova o item manualmente.
3. **`issues.opened` sempre grava `Backlog`**, inclusive ao promover um draft que já estava em
   `Ready` ou `In progress`. Reajuste o Status depois da promoção.
4. **O `project-sync` não atualiza o item da issue.** O merge atualiza o item do PR; o item da issue
   só muda com automação nativa do Projects ou ajuste manual. Mantenha a issue como o cartão de
   trabalho (`Ready` → `In progress` → `Done`) ou trabalhe só com o item do PR.
5. **`seed` não grava `Status`.** Drafts criados pelo seed entram sem Status até você definir
   manualmente. O seed também não atualiza itens existentes: título repetido é pulado.
6. **O `Done` do sync acontece no merge, não no deploy.** Use o `Done` do item do PR como sinal de
   "mergeado"; o `/health` no SHA mergeado continua sendo verificação obrigatória. Mover o item para
   `Done` só depois do `/health` é automação da Fase B; se o deploy falhar, reabra a issue e ajuste o
   Status manualmente.
7. **PR aberto como draft entra em `In review`.** O sync não olha `pull_request.draft`; enquanto o
   trabalho estiver em andamento, mantenha o item em `In progress` e marque o PR como ready só
   quando for revisar.

## Branches

- `fix/123-<slug>` para correção de bug.
- `feat/123-<slug>` para funcionalidade ou melhoria.
- `123` é o número da issue; `<slug>` é curto, em kebab-case, e descreve a mudança.
- Uma branch por issue. Não misture correções independentes no mesmo PR.
- Antes do merge, atualize a branch com a `main`; a proteção exige branch atualizada (`strict`).

Commits seguem o histórico do repositório (`tipo(escopo): resumo`, por exemplo
`fix(fisga): ...`), pequenos e com o porquê no corpo quando a mudança não for óbvia.

## Pull request

O template fica em `.github/PULL_REQUEST_TEMPLATE.md` e começa com `Closes #<n>`. Preencha:

- o número da issue que o PR fecha;
- resumo da mudança (o que muda e por quê, em uma a três frases);
- área afetada (`API`, `Web`, `UX`, `Docs`);
- checklist de review, incluindo screenshots antes/depois quando a mudança for visual.

PR de trabalho sem issue ligada não entra no fluxo. Se a mudança não fecha a issue inteira, use
`Refs #<n>` no corpo e mantenha a issue aberta. PRs automáticos do Dependabot são a exceção: não têm
issue, não entram no quadro porque evento de bot é ignorado e não seguem o fluxo de issue. Ainda
assim passam pelos checks do repositório — `gate`, dependency-review, CodeQL e reviewdog.

## Revisão automática e comandos

O PR-Agent roda sozinho em `opened`, `reopened` e `ready_for_review` e responde em pt-BR. Os
comandos são comentários no PR, restritos a `OWNER`, `MEMBER` e `COLLABORATOR`:

| Comando | Onde comentar | O que faz |
| --- | --- | --- |
| `/review` | PR | refaz a revisão completa do diff |
| `/describe` | PR | reescreve a descrição do PR a partir do diff |
| `/ask <pergunta>` | PR | responde uma pergunta específica no contexto do PR |
| `/improve` | PR | sugere melhorias para o diff |
| `/oc` | PR (comentário) ou linha do diff | agente OpenCode: explica, corrige, implementa e pode commitar |

Regras de uso:

- `/ask` precisa do espaço depois do comando (`/ask por que ...`); sem argumento o job não roda.
- `/oc` gasta muito mais que os outros comandos e altera código. Use quando precisar de execução,
  não como primeiro review.
- PR-Agent e `/oc` são advisory. Não existe required check de LLM; o único check obrigatório é
  `gate`.
- PR de fork não recebe secrets no evento `pull_request`: o review automático do PR-Agent é pulado.
- O `/oc` não roda no evento `pull_request`: ele escuta `issue_comment` (comentário de conversa) e
  `pull_request_review_comment` (resposta em linha do diff). Em `issue_comment`, o workflow e o
  checkout vêm da default branch, e o job tem `contents: write` e `OPENCODE_API_KEY`. Em
  `pull_request_review_comment`, o GitHub roda o **workflow e o checkout do merge da PR**; em PR de
  fork ele retém os secrets e entrega um `GITHUB_TOKEN` read-only, mas o código do fork ainda vai
  para o runner. Em PR do próprio repo, o merge pode trazer um `oc.yml` alterado: revise mudanças em
  `.github/workflows/**` antes de comentar.
- A frase de [review.md](./review.md) de que `/oc` é pulado em PR de fork se aplica ao evento
  `pull_request` do review automático; o `/oc` é acionado por comentário, não por `pull_request`.
  Em `issue_comment` ele roda com os privilégios da default branch; em `pull_request_review_comment`
  de fork roda sem secrets e com token read-only, mas faz checkout do merge da PR. Não use `/oc` em
  fork não confiável sem revisar o diff.
- Comentário de bot não dispara o PR-Agent.
- Leia os achados, corrija ou responda **no thread**. Achado sem resposta e sem resolução bloqueia
  o merge.

As camadas e o custo de cada uma estão em [review.md](./review.md).

## Checklist de merge

Antes de apertar o botão:

- [ ] `Closes #<n>` no PR e a issue certa ligada.
- [ ] `gate` verde e branch atualizada com a `main` (a proteção exige os dois).
- [ ] Todas as threads respondidas e resolvidas; a verificação por API devolve `[]`.
- [ ] Screenshots antes/depois anexados (desktop e mobile) quando a mudança for visual.
- [ ] Nenhum token, PAT ou valor sensível no diff, no log ou na descrição.
- [ ] Mudança de workflow com action pinada e `gh actions-lock` atualizado.
- [ ] `/health` da produção saudável e **nenhum deploy em andamento** no momento do merge.

O ruleset aceita merge commit, squash e rebase (`allowed_merge_methods`). Em projeto solo, o squash
mantém a `main` legível; escolha uma e mantenha o padrão no PR.

### Verificar threads via API

O ruleset só libera o merge com `required_review_thread_resolution = true`; a UI pode esconder
thread antiga. Confirme no head atual:

```bash
gh api graphql -f query='query {
  repository(owner: "AbsonDev", name: "abson") {
    pullRequest(number: <N>) {
      reviewThreads(first: 100) {
        nodes {
          id
          isResolved
          comments(first: 1) { nodes { path line body } }
        }
      }
    }
  }
}' --jq '[.data.repository.pullRequest.reviewThreads.nodes[]
         | select(.isResolved == false)
         | {id, path: .comments.nodes[0].path, line: .comments.nodes[0].line}]'
```

Saída esperada: `[]`. Se houver thread aberta, responda e resolva. Responder no thread e resolver
são ações diferentes; um comentário geral no PR não substitui nenhuma das duas.

Responder e resolver pela API, depois de corrigir ou justificar:

```bash
# Responder no thread (o texto vai no corpo do comentário)
gh api graphql -f query='mutation($thread: ID!, $body: String!) {
  addPullRequestReviewThreadReply(
    input: {pullRequestReviewThreadId: $thread, body: $body}
  ) { comment { url } }
}' -f thread="$THREAD_ID" -f body="Corrigido em <commit>; <o que mudou>."

# Marcar como resolvido
gh api graphql -f query='mutation($thread: ID!) {
  resolveReviewThread(input: {threadId: $thread}) { thread { isResolved } }
}' -f thread="$THREAD_ID"
```

Repita a query de verificação no fim. `isResolved: true` em todas é o estado que o ruleset exige.

## Ruleset e resolução de threads

Estado em 18/11/2026, ruleset **`main-protection`** (id `27557615`, enforcement `active`, target
`main`):

- Require a pull request before merging, com 0 aprovações (projeto solo).
- **`required_review_thread_resolution = true` desde 18/11/2026.**
- Require status checks: **`gate`**, strict (branch atualizada).
- Block force pushes e restrict deletions.
- `bypass_actors: []`: ninguém contorna, inclusive o admin do repositório.

A mudança foi aplicada com `PUT` no ruleset, não `PATCH`. A API de rulesets responde `404` a
`PATCH`; o verbo documentado é `PUT`, que substitui o ruleset inteiro. Por isso o payload é
reduzido aos campos graváveis `{name, target, enforcement, bypass_actors, conditions, rules}`,
derivados do estado capturado antes da escrita:

```bash
TMP="${TMPDIR:-/tmp}/fisga-ruleset-$$"
mkdir -p "$TMP"
gh api repos/AbsonDev/abson/rulesets/27557615 > "$TMP/before.json"
jq '{name,target,enforcement,bypass_actors,conditions,rules}
    | .rules |= map(if .type == "pull_request"
                    then .parameters.required_review_thread_resolution = true
                    else . end)' "$TMP/before.json" > "$TMP/put.json"
gh api -X PUT repos/AbsonDev/abson/rulesets/27557615 --input "$TMP/put.json"
rm -rf "$TMP"
```

Como o `PUT` substitui tudo, capture o estado antes e compare: a única diferença esperada é o
campo alterado. Verificação:

```bash
gh api repos/AbsonDev/abson/rulesets/27557615 \
  --jq '{enforcement,
         threads: (.rules[] | select(.type=="pull_request")
                   | .parameters.required_review_thread_resolution),
         contexts: [.rules[] | select(.type=="required_status_checks")
                    | .parameters.required_status_checks[].context]}'
```

Saída esperada: `enforcement: "active"`, `threads: true`, `contexts: ["gate"]`. A regra efetiva na
branch também pode ser conferida:

```bash
gh api repos/AbsonDev/abson/rules/branches/main \
  --jq '.[] | select(.type == "pull_request")
        | {type, thread_resolution: .parameters.required_review_thread_resolution}'
```

Para reverter a exigência (não recomendado), repita o procedimento com `false`; não há bypass para
usar como atalho.

## Deploy e Done

Depois do merge, o `fisga-deploy` dispara por `workflow_run` quando o `fisga-pipeline` conclui
`success` na `main`. Ele acompanha o deployment no Coolify e exige `/health` com
`version == head_sha` (o SHA que o CI validou); depois roda o smoke de `/ready`, `/` e `/condicoes`.
O detalhe operacional está em [ci-cd.md](./ci-cd.md).

Verificação depois do merge:

```bash
gh run list --repo AbsonDev/abson --workflow fisga-deploy.yml --branch main --limit 1 \
  --json databaseId,headSha,status,conclusion

curl -fsS https://api.fisga.abson.tech/health
curl -fsS https://fisga.abson.tech/health

curl -fsS -o /dev/null -w '%{http_code}\n' https://api.fisga.abson.tech/ready
curl -fsS -o /dev/null -w '%{http_code}\n' https://fisga.abson.tech/
curl -fsS -o /dev/null -w '%{http_code}\n' https://fisga.abson.tech/condicoes
```

- `/health` com o SHA do merge e smoke 200: deploy concluído. O `Done` do item do PR significa
  "mergeado"; ligar deploy→quadro é Fase B.
- Deploy falhou ou `/health` não bate: reabra a issue, volte o Status para `In progress` e registre
  o erro no PR/issue. O `Done` do sync não substitui esta verificação.
- Não mergeie outro PR durante um deploy: o Coolify builda o HEAD da branch e o health check compara
  com o SHA do run. Espere o run terminar.

## Automação do quadro

### Secret `FISGA_PROJECT_TOKEN`

O workflow `project-sync` usa um token com escopo **`project`** guardado no secret
`FISGA_PROJECT_TOKEN` do repositório. Ele é o caminho de escrita automatizada no quadro criado por
este fluxo; o `GITHUB_TOKEN` do run fica em `contents: read` e não serve para Projects.

Configuração (o valor nunca aparece no chat, no repositório ou no log):

```bash
gh secret set FISGA_PROJECT_TOKEN --repo AbsonDev/abson
# cole o token no prompt; o comando não ecoa o valor
gh secret list --repo AbsonDev/abson   # confirma só o nome
```

Sem o secret, o job falha no primeiro evento com a mensagem de token ausente do script. Para
validar o escopo antes de confiar no sync, faça uma leitura com o mesmo token (não uma escrita):

```bash
read -rs GH_TOKEN
export GH_TOKEN
gh project item-list 6 --owner AbsonDev --format json --limit 1
unset GH_TOKEN
```

Rotação: crie o token novo com escopo `project`, atualize o secret, revogue o antigo e valide no
próximo evento de issue/PR conferindo o run `project-sync`. Token exposto se revoga e se recria no
mesmo dia — nunca se reutiliza.

### Script

`scripts/github-project-sync.mjs` é um módulo ESM sem dependências externas, usando a CLI `gh`.
Requisitos: **Node 24+** e **`gh >= 2.103.3`** (versão usada na validação). Os comandos abaixo
assumem a raiz do repositório.

```bash
node scripts/github-project-sync.mjs event-status
node scripts/github-project-sync.mjs add-or-sync "<url>" "<status>"
node scripts/github-project-sync.mjs seed "<arquivo.json>"
node scripts/github-project-sync.mjs --help
```

| Comando | O que faz | Exige token? |
| --- | --- | --- |
| `event-status` | lê `GITHUB_EVENT_PATH`, mapeia o evento e imprime o Status (ou nada) | não |
| `add-or-sync <url> <status>` | adiciona a issue/PR ao quadro (idempotente) e grava o Status | sim |
| `seed <arquivo.json>` | cria drafts com Priority, Size e Iteration, pulando títulos já existentes | sim |

Ambiente: `GH_TOKEN` (ou `GITHUB_TOKEN`) para `add-or-sync` e `seed`; `GITHUB_EVENT_PATH` para
`event-status`. No workflow, `GH_TOKEN` vem de `secrets.FISGA_PROJECT_TOKEN`.

Uso local seguro (o token só existe na sessão do shell):

```bash
read -rs GH_TOKEN
export GH_TOKEN
node scripts/github-project-sync.mjs seed "<seed-privado>.json"
unset GH_TOKEN
```

O seed com achados crus não é versionado: o repositório é público e o quadro é privado. Guarde o
JSON em diretório git-ignorado (por exemplo, `.superpowers/sdd/<data>/github-project-seed.json`) e
confirme com `git check-ignore -v <arquivo>` antes de rodar.

Formato do seed: array JSON, sem títulos repetidos. Oito campos obrigatórios e não vazios:

```json
[
  {
    "title": "Fonte e horário invisíveis na lista de condições",
    "area": "Web",
    "evidencia": "https://fisga.abson.tech/condicoes; condicoes.astro:42",
    "impacto": "No celular, a pessoa não confia na condição exibida",
    "aceite": "Fonte e horário visíveis no mobile, sem regressão no desktop",
    "priority": "P1",
    "size": "S",
    "iteration": "Iteration 1"
  }
]
```

Regras do seed:

- `priority` ∈ `P0/P1/P2`; `size` ∈ `XS/S/M/L/XL`; `iteration` ∈ `Iteration 1/2/3`.
- `area` é texto livre no seed, mas use `API`, `Web`, `UX` ou `Docs` para casar com o template.
- O script valida o arquivo inteiro antes de criar qualquer draft; erro de campo/opção não deixa
  escrita parcial.
- Título repetido é pulado (comparação sem diferenciar caixa nem espaços nas pontas).
- A checagem de duplicados lê até 100 itens do quadro; acima disso, o script avisa no log. O seed
  não atualiza item existente; para corrigir, ajuste no quadro ou remova e rode de novo.
- Se uma edição falhar depois do `item-create`, o draft fica incompleto e o erro instrui o ajuste
  manual. Não existe rollback automático.

### Workflow `project-sync`

`.github/workflows/project-sync.yml` roda em `issues: opened` e `pull_request: opened, closed`.

- Ignora evento aberto por bot (`sender.type == 'Bot'`).
- Ignora PR de fork (`head.repo.full_name != github.repository`): o secret não é entregue.
- Faz checkout da **default branch**, não do merge da PR; o script executado é sempre o da `main`.
- `permissions: contents: read`; a escrita no quadro vem do `GH_TOKEN` com o secret.
- Concorrência por número de issue/PR, sem cancelar o run em andamento.
- Job `sync`, Node 24, timeout de 10 minutos.

O step lê o Status com `event-status`; se o evento não muda Status, loga e sai com sucesso. Se muda,
chama `add-or-sync` com a URL do evento. Falha do script derruba o job com a mensagem em pt-BR.

## Labels

| Label | Uso | Origem |
| --- | --- | --- |
| `ux` | achado de UX/UI do Fisga | template de issue |
| `bug` | algo não funciona | label existente; aplique à mão |
| `enhancement` | melhoria ou funcionalidade | label existente; aplique à mão |
| `accessibility` | barreira de acessibilidade | label existente; aplique à mão |
| `documentation` | mudança de documentação | label existente; aplique à mão |
| `dependencies` | atualização de dependência | Dependabot |
| `docker`, `javascript`, `.NET` | ecossistema da atualização | Dependabot |

Label classifica a natureza; área fica no campo do template/checkbox do PR. Não crie label nova sem
atualizar este documento e o template; label órfã não é usada por ninguém.

## Métricas

Revise uma vez por iteração, não por PR. A linha de base vem das primeiras medições; meta só depois
de 2–3 iterações.

| Métrica | Como medir | Referência |
| --- | --- | --- |
| Tempo issue → PR | `createdAt` do PR que fecha a issue − `createdAt` da issue | tendência de queda |
| Tempo PR → deploy | `updatedAt` do run verde do `fisga-deploy` com `headSha == mergeCommit.oid` − `mergedAt` do PR | linha de base da Fase A |
| Itens por iteração | itens do quadro agrupados por `Iteration` | capacidade acordada da iteração |
| Merges com thread aberta | PRs `MERGED` com alguma `reviewThreads` com `isResolved: false` | 0 |

Issue → PR:

```bash
gh issue list --repo AbsonDev/abson --state all --limit 100 \
  --json number,createdAt,closedByPullRequestsReferences
gh pr list --repo AbsonDev/abson --state merged --limit 100 \
  --json number,createdAt,mergedAt,closingIssuesReferences,mergeCommit
```

Compare os timestamps da issue e do PR que a fecha. PR → deploy, juntando os dois arquivos:

```bash
TMP="${TMPDIR:-/tmp}/fisga-metrics-$$"
mkdir -p "$TMP"
gh pr list --repo AbsonDev/abson --state merged --limit 50 \
  --json number,mergedAt,mergeCommit > "$TMP/prs.json"
gh run list --repo AbsonDev/abson --workflow fisga-deploy.yml --branch main --limit 50 \
  --json databaseId,headSha,updatedAt,conclusion > "$TMP/runs.json"
jq -n --slurpfile prs "$TMP/prs.json" --slurpfile runs "$TMP/runs.json" '
  [$prs[0][] as $pr
   | ([$runs[0][] | select(.headSha == $pr.mergeCommit.oid and .conclusion == "success")]
      | sort_by(.updatedAt) | last) as $run
   | select($run != null)
   | (($run.updatedAt | fromdateiso8601) - ($pr.mergedAt | fromdateiso8601)) as $s
   | {pr: $pr.number, run: $run.databaseId, minutos_merge_deploy: ($s / 60)}]'
rm -rf "$TMP"
```

Itens por iteração (exige `read:project`):

```bash
gh project item-list 6 --owner AbsonDev --format json --limit 100 \
  --jq '[.items[] | (.iteration.title // "sem iteração")]
        | group_by(.) | map({iteration: .[0], itens: length})'
```

Merges com thread aberta:

```bash
gh api graphql -f query='query {
  repository(owner: "AbsonDev", name: "abson") {
    pullRequests(states: MERGED, last: 50) {
      nodes {
        number
        mergedAt
        reviewThreads(first: 100) { nodes { isResolved } }
      }
    }
  }
}' --jq '[.data.repository.pullRequests.nodes[]
         | select(any(.reviewThreads.nodes[]; .isResolved == false))
         | {number, mergedAt}]'
```

Qualquer resultado diferente de `[]` indica thread aberta em PR mergeado: ou o merge é anterior a
18/11/2026, ou o ruleset foi contornado. A query é uma foto do estado atual, então uma thread criada
depois do merge também aparece; confira a data do comentário contra o `mergedAt` antes de tratar
como falha de processo. Em qualquer caso, abra o thread, responda e corrija o processo.

## Segurança e privacidade

- **Token nunca em chat, issue, PR, arquivo, commit ou log.** Só no secret do Actions. Se exposto,
  revogue e recrie imediatamente.
- O quadro é privado; o repositório é público. Crítica crua de UX, print com dado pessoal e rascunho
  ficam no draft, não na issue.
- PR de fork, no evento `pull_request`, roda com `GITHUB_TOKEN` read-only e sem secrets. Não use
  `pull_request_target` com checkout do código do PR.
- **`project-sync` em PR do próprio repositório:** o job faz checkout da default branch e executa o
  script versionado na `main`, mas o GitHub usa o YAML do merge da PR. Alterar
  `.github/workflows/project-sync.yml` na PR muda o job que roda com o secret do quadro; por isso
  esse arquivo exige write access e revisão explícita. Em fork, o secret não é entregue.
- **`/oc` em fork é um caminho separado.** Em `issue_comment`, ele roda da default branch com
  `contents: write` e `OPENCODE_API_KEY`; em `pull_request_review_comment`, o GitHub roda o workflow
  e o checkout do merge da PR e, em fork, retém os secrets e rebaixa o token para leitura. Nos dois
  casos, trate o PR como entrada não confiável: não use `/oc` em fork sem revisar o diff e, em PR do
  próprio repo, revise mudanças em `.github/workflows/**` antes de comentar.
- Conteúdo de PR é entrada não confiável: título, corpo e diff podem conter instrução para o modelo.
  PR-Agent roda em modo restrito e `/oc` só atende quem tem write.
- `/oc` pode commitar. Revise o commit como qualquer outro; se errar, reverta no branch e responda
  no thread.
- Push que altera `.github/workflows/**` exige token com escopo `workflow`; nunca cole PAT em chat
  ou arquivo. Se o `gh` local for OAuth (`gho_`), use `gh auth refresh -s workflow`.

## Troubleshooting

| Sintoma | Causa provável | Ação |
| --- | --- | --- |
| `project-sync` vermelho com "Token do quadro ausente" | secret `FISGA_PROJECT_TOKEN` não existe ou está vazio | criar/atualizar o secret; rodar de novo o job |
| `project-sync` vermelho com erro de escopo/401 | token sem escopo `project` ou revogado | gerar token novo, atualizar o secret, validar com leitura |
| Issue/PR não aparece no quadro | evento de bot, PR de fork ou workflow desabilitado | conferir o `if` do job, o remetente e o run mais recente |
| PR fechado sem merge continua `In review` | limitação do sync | mover o Status manualmente |
| PR de fork mergeado não vira `Done` | job ignora fork | mover o Status manualmente |
| Draft promovido voltou para `Backlog` | `issues.opened` sempre grava `Backlog` | reajustar o Status |
| Merge bloqueado com `gate` verde | thread aberta, branch desatualizada ou check "Expected" | rodar a query de threads; atualizar a branch; conferir o contexto `gate` |
| Thread resolvida na UI, merge ainda bloqueado | thread antiga não resolvida ou resolução em outro head | repetir a query no head atual e resolver o que faltar |
| `Campo "..." não encontrado` ou `Opção "..." não existe` no seed | quadro renomeou campo/opção | conferir `field-list`; corrigir o JSON ou o script |
| Seed pulou um item | título duplicado (caixa/espaços ignorados) | conferir o quadro; remover o antigo ou ajustar o título |
| Seed leu só parte do quadro | mais de 100 itens | o script avisa no log; revisar duplicados manualmente |
| `gh project` local com `missing required scopes [read:project]` | CLI sem escopo de leitura de Projects | `gh auth refresh -s read:project` |

## Pendências conhecidas (Fase B)

- ~~Criar o secret `FISGA_PROJECT_TOKEN`~~ — resolvido em 19/11/2026: criado e validado no merge do
  PR #39. Migrar para token fine-grained só de Projects (o atual é OAuth amplo).
- Ligar o deploy ao quadro: hoje `Done` = mergeado; mover para `Done` só depois de `/health` no SHA
  mergeado é automação da Fase B.
- Fechar as lacunas do sync: PR fechado sem merge, PR de fork e Status do item da issue após o merge.
- Fazer o seed gravar `Status` e permitir atualização de item existente.
- Automatizar as métricas em um dashboard; hoje a coleta é manual por iteração.
- Fase B do CI/CD (GHCR, E2E real, preview deployments, merge queue) em [ci-cd.md](./ci-cd.md).
