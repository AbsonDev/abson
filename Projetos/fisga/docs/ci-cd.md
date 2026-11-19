# CI/CD do Fisga

Pipeline da Fase A: CI obrigatório em todo PR, CD separado no Coolify e verificação pelo SHA real
em produção.

**Validações de referência:** 18/11/2026 — primeiro deploy automático em `725afd5` (run
`44338998771`) e consolidação da documentação em `f50560a` (run `44348792718`). Em ambas, API e web
responderam `/health` com o SHA do merge, além de `/ready`, `/` e `/condicoes` 200.

O fluxo de trabalho completo (draft → issue → branch → PR → revisão → merge → deploy → Done), com
convenções de branch, labels, campos do quadro e checklist de merge, está em
[github-workflow.md](./github-workflow.md).

## Visão geral

```text
PR
└── fisga-pipeline
    ├── changes   (separa mudança de código de mudança de documentação)
    ├── api       (restore locked + format + build + 348 testes)
    ├── web       (npm ci + astro check + build + 155 Playwright mockados)
    ├── docs      (markdownlint + links, em PR de código ou de documentação)
    ├── docker    (build das imagens sem push, em PR e workflow_dispatch)
    └── gate      (check obrigatório)

push em main
└── fisga-pipeline (CI: changes + o que o filtro exigir + gate)
    └── fisga-deploy (workflow_run, só com CI verde e mudança de código na main)
        ├── changes   (diff do commit que o CI validou)
        ├── deploy-api  (Coolify -> /health = head_sha -> /ready -> smoke do web atual)
        └── deploy-web  (Coolify -> /health = head_sha -> / e /condicoes)
```

Os workflows ficam em `.github/workflows/fisga-pipeline.yml` (CI),
`.github/workflows/fisga-deploy.yml` (CD) e `.github/workflows/fisga-docs-ai.yml` (revisão de
documentação, advisory). O check obrigatório da `main` é o job **`gate`** (a UI
pode exibir `fisga-pipeline / gate`; confirme o contexto exato em
`gh api repos/AbsonDev/abson/commits/<sha>/check-runs`).

O fluxo de revisão de PRs (camadas determinísticas, LLM e agente) está em
[review.md](./review.md).

## Gatilhos e decisões de desenho

### Por que não há `paths:` no CI

Um workflow filtrado por `paths:` não roda em PRs que não tocam no Fisga; sem run, o check
obrigatório fica "Expected" e o merge trava. Por isso o `fisga-pipeline` roda em **todo** PR e push
para `main`, e o job `changes` decide o que executa.

### Por que o deploy é um workflow separado

Na primeira versão, `deploy-api` e `deploy-web` ficavam no fim do `fisga-pipeline`, com
`if: github.ref == 'refs/heads/main'`. Mesmo com o `if` mínimo, os jobs eram **pulados** no evento
`push`. O CD foi movido para `fisga-deploy.yml`, disparado por `workflow_run`. Ver
[Histórico de incidentes](#histórico-de-incidentes-e-lições).

Consequências do desenho atual:

- O deploy não depende do contexto do evento `push`; usa `github.event.workflow_run.*`.
- O `workflow_run` só existe depois que o CI termina; o deploy nunca começa antes do `gate`.
- O SHA deployado é `github.event.workflow_run.head_sha`, o mesmo que o CI validou.
- Se o CI falhar ou for cancelado na `main`, o deploy não roda.
- Um novo push na `main` **não cancela** um deploy em andamento: o grupo `fisga-production` usa
  `cancel-in-progress: false` e o próximo run espera na fila.

## Jobs de CI

### `changes`

Roda em todo PR e push para `main`, sem `paths:` no gatilho. O job usa `git diff` para decidir se o
Fisga mudou e expõe dois outputs: `needs.changes.outputs.fisga` (código) e
`needs.changes.outputs.documentacao` (só documentação).

- PR: diff entre `base.sha` e `head.sha`.
- Push: diff entre `github.event.before` e `github.sha`, com fallback para o commit raiz quando o
  `before` não existe (branch nova ou force push).
- `workflow_dispatch`: assume `fisga=true` e `documentacao=true`.

O filtro separa código de documentação:

- **Código** (`fisga=true`): qualquer caminho em `Projetos/fisga/` **exceto** `docs/**`,
  `README.md` e `AGENTS.md`, mais o próprio `fisga-pipeline.yml`.
- **Documentação** (`documentacao=true`): `Projetos/fisga/docs/**`, `Projetos/fisga/README.md`,
  `Projetos/fisga/AGENTS.md`, `AGENTS.md` da raiz, `.github/copilot-instructions.md`,
  `.github/PULL_REQUEST_TEMPLATE.md`, `.markdownlint-cli2.jsonc` e `.lychee.toml`.
- **Exceção deliberada:** `Projetos/fisga/docs/api/openapi.json` (o contrato gerado) conta como
  **código**, não como documentação. O contrato é gerado; editar `openapi.json` à mão precisa passar
  pelo job `api`, que regenera e compara. Os demais arquivos de `docs/api/` — como
  `docs/api/README.md` — são documentação comum.

### `api`

- `dotnet restore Fisga.slnx --locked-mode`
- `dotnet format Fisga.slnx --verify-no-changes --no-restore`
- `dotnet build Fisga.slnx -c Release --no-restore -warnaserror`
- `dotnet test Fisga.slnx -c Release --no-build` (348 testes)
- `./scripts/gerar-openapi.sh` (regenera `docs/api/openapi.json` com SQLite temporário, ambiente
  Development e Sentry desligado)
- `git diff --exit-code -- docs/api/openapi.json` (contrato desatualizado falha o job com a instrução
  de rodar o script e commitar)

Os resultados TRX são enviados como artifact `fisga-test-results` (retenção de 7 dias).
`docs/api/openapi.json` também é publicado como artifact `fisga-openapi` (retenção de 30 dias). O job
usa `actions/setup-dotnet` com `global-json-file` e cache baseado nos `packages.lock.json`.

### `web`

- `npm ci`
- `npm run check` (`astro check`)
- `npm run build` (Astro + Pagefind + precache offline)
- `npm run test:e2e` (155 testes Playwright mockados, Chromium)

O E2E real (6 testes) fica no script `./scripts/e2e-real.sh`, fora do CI por exigir backend,
SQLite, chaves e outbox temporários; pode virar um job nightly na Fase B.

### `docker`

Roda em PR e em `workflow_dispatch`. Constrói as imagens da API e do web sem push, para validar
Dockerfiles, `.dockerignore` e o secret mount do Sentry. Não recebe `SENTRY_AUTH_TOKEN` em PRs de
fork; nesse build os source maps ficam desabilitados. O job também falha se `SENTRY_AUTH_TOKEN`
aparecer como `ARG`/`ENV` no Dockerfile.

### `docs`

Roda em PR de código **e** em PR só de documentação (`documentacao=true`), mas não no `push`:
link externo que morre depois do merge não pode bloquear o deploy de código já mergeado.

- `npx --yes markdownlint-cli2@0.24.1` (config em `.markdownlint-cli2.jsonc`).
- `lycheeverse/lychee-action` com `.lychee.toml` em `AGENTS.md`, `Projetos/fisga/README.md`,
  `Projetos/fisga/AGENTS.md` e `Projetos/fisga/docs/**/*.md`.

O `gate` depende dele: link quebrado ou Markdown inválido bloqueia o merge.

### `gate`

Sempre roda (`if: always()`) e falha se qualquer job necessário falhar ou for cancelado. É o único
check que precisa ser obrigatório na proteção da `main`.

## Deploy

O `fisga-deploy.yml` dispara com:

```yaml
on:
  workflow_run:
    workflows: [fisga-pipeline]
    types: [completed]
    branches: [main]
```

Os jobs de deploy só executam quando **todas** as condições são verdadeiras:

- `needs.changes.outputs.fisga == 'true'`;
- `github.event.workflow_run.conclusion == 'success'`;
- `github.event.workflow_run.head_branch == 'main'`.

O job `changes` do deploy faz checkout de `head_sha` com `fetch-depth: 0` e diffa `HEAD^..HEAD`
(ou o commit raiz, no primeiro commit). Ele aplica o mesmo critério de código do CI, com uma
diferença: qualquer caminho em `Projetos/fisga/` exceto `docs/**`, `README.md` e `AGENTS.md`, mais
os próprios `fisga-pipeline.yml`/`fisga-deploy.yml`. Como `docs/api/` fica sob `docs/`, mudança só do
contrato gerado não deploya — nada muda em produção, e o job `api` do CI já regenera e compara.

### Documentação não deploya

O filtro de código exclui `Projetos/fisga/docs/**`, `README.md` e `AGENTS.md`.
Um merge só de documentação roda o job `docs` no PR e **não** dispara deploy.
Mudou código, configuração ou container? Deploy normal. O job de deploy continua
sendo o mesmo `workflow_run`, sem `paths:`.

O deploy não usa `inputs`, `github.ref` nem `github.event_name` no `if`:

> **Nota de manutenção:** não reintroduzir jobs de deploy no `fisga-pipeline` nem trocar o `if` por
> `github.ref`. O contexto correto é `github.event.workflow_run.*`; `workflow_run` aninhado e
> `inputs` não existem nesse evento.

### Ordem e verificações

1. `deploy-api`: dispara o Coolify, acompanha o deployment até `finished`/`success` (até 20 min),
   aguarda `https://api.fisga.abson.tech/health` com `version == workflow_run.head_sha` (até 5 min)
   e valida `/ready`.
2. Antes de trocar o web, o job da API valida o **web atual** em `/` e `/condicoes`. Se o web
   antigo quebrar com a API nova, o deploy do web não acontece e o rollback da API é obrigatório.
3. `deploy-web`: dispara o Coolify, acompanha o deployment (até 20 min), aguarda o `/health` com
   `version == workflow_run.head_sha` e valida `/` e `/condicoes`.

Se a API falhar, o web não é deployado. Se o web falhar no smoke, faça rollback do web pelo Coolify;
a API nova continua no ar e precisa ser compatível com a versão anterior do web.

Para deploy manual fora do fluxo (emergência), use o painel/API do Coolify. Não crie um
`workflow_dispatch` paralelo no deploy: dois caminhos de deploy com o mesmo grupo de concorrência
só aumentam a chance de SHA divergente.

## Secrets e variáveis do GitHub

Configurar em **Settings → Secrets and variables → Actions** (repositório):

| Nome | Tipo | Uso |
| --- | --- | --- |
| `COOLIFY_URL` | secret | Base HTTPS do Coolify (ex.: `https://manager.abson.tech`), sem `/api/v1` |
| `COOLIFY_TOKEN` | secret | Token com permissões `read` + `deploy` (status do deployment, rollback e deploy) |
| `COOLIFY_WEBHOOK_FISGA_API` | secret | Deploy webhook autenticado do app `fisga-api` (HTTPS) |
| `COOLIFY_WEBHOOK_FISGA_WEB` | secret | Deploy webhook autenticado do app `fisga-web` (HTTPS) |
| `FISGA_PUBLIC_SENTRY_DSN` | variable | DSN público do Sentry usado no build do browser; variável de repositório |

Os segredos de runtime (banco, Resend, Sentry server, mídia, newsletter) continuam no Coolify, não
no GitHub. O `SENTRY_AUTH_TOKEN` é usado no build do web; na Fase A ele fica no Coolify como BuildKit
secret `sentry_auth_token` ou os source maps ficam desabilitados. Nunca passar esse token como
`ARG`/`ENV` de Dockerfile nem como build variable comum.

### Token do Coolify: escopo e rotação

O token `github-actions-fisga-ci` foi criado escopado **apenas** aos apps `fisga-api` e `fisga-web`,
com `read` + `deploy`, validade de 90 dias. Ele é o único token do Coolify que o GitHub usa.

Rotação:

1. No Coolify, criar um token novo com o mesmo escopo (`fisga-api` + `fisga-web`, read + deploy).
2. Atualizar o secret `COOLIFY_TOKEN` no GitHub (`gh secret set COOLIFY_TOKEN`).
3. Apagar o token antigo no Coolify (Keys & Tokens).
4. Rodar um deploy de validação (merge na `main` ou deploy manual controlado) e confirmar o
   `/health` com o SHA.

Nunca colar token em chat, issue, arquivo do repositório ou log. Se um token for exposto, revogar
imediatamente e rotacionar o secret.

## Configuração do Coolify

Para `fisga-api` e `fisga-web`:

- **Auto deploy**: desligado. O deploy é disparado só pelo CI.
- **Health check**: API em `/ready`; web em `/health`.
- **Portas**: usar a porta interna e o Traefik. Não mapear porta no host, para não bloquear o
  rolling update.
- **Container name**: não usar nome fixo/consistente, pelo mesmo motivo.
- **Rolling update**: confirmar no log do deployment que o Coolify iniciou o novo container antes
  de remover o antigo. Configuração atual: stop grace 30s e rolling drain 30s.
- **Retenção de imagens**: 10 imagens, suficiente para rollback sem inflar o disco do VPS.
- **Source commit**: em **Advanced → Build → Source commit availability**, selecionar
  **Available during build**. O Dockerfile mapeia `SOURCE_COMMIT` para `GIT_SHA`; o `/health` usa
  esse valor.
- **Sentry build secret**: usar BuildKit secret com ID `sentry_auth_token`; nunca configurar
  `SENTRY_AUTH_TOKEN` como build variable/ARG. Na Fase A o token está desabilitado como
  buildtime/runtime no Coolify e os source maps ficam desligados.
- **Docker da API**: o Dockerfile copia `Directory.Build.props`, `global.json` e os
  `packages.lock.json` e roda `dotnet restore --locked-mode`, para a imagem usar o mesmo grafo de
  dependências do CI.
- **Runtime da API**: `aspnet:10.0` (o app target é `net10.0`). Não usar `aspnet:11.0`.
- **Volume**: manter o volume de mídia do Fisga; ele não é afetado pelo deploy.

O Coolify atualiza a imagem a partir do commit; o health check precisa retornar o SHA exato. Se
`/health` retornar `dev`, o `SOURCE_COMMIT` não chegou ao build.

## Verificação do estado atual

```bash
# Último CI e último deploy na main
gh run list --repo AbsonDev/abson --workflow fisga-pipeline.yml --branch main --limit 1 \
  --json databaseId,headSha,status,conclusion
gh run list --repo AbsonDev/abson --workflow fisga-deploy.yml --branch main --limit 1 \
  --json databaseId,headSha,status,conclusion

# SHA em produção (deve ser igual ao merge da main)
curl -fsS https://api.fisga.abson.tech/health
curl -fsS https://fisga.abson.tech/health

# Smoke
curl -fsS -o /dev/null -w '%{http_code}\n' https://api.fisga.abson.tech/ready
curl -fsS -o /dev/null -w '%{http_code}\n' https://fisga.abson.tech/
curl -fsS -o /dev/null -w '%{http_code}\n' https://fisga.abson.tech/condicoes
```

Interpretação:

- `version` igual ao SHA do merge → deploy concluído e health check do container novo.
- `version` de um commit anterior → deploy ainda em andamento, falhou ou o Coolify não reconstruiu.
- `version: dev` → `SOURCE_COMMIT`/`GIT_SHA` não chegou ao build.

## Evidências de validação (18/11/2026)

| O quê | Resultado |
| --- | --- |
| CI do PR #14 (`44338126881`) | `changes`, `api`, `web`, `docker`, `gate` verdes |
| Merge do PR #14 | `725afd5` na `main` |
| CI da `main` (`44338671367`) | verde |
| Deploy automático (`44338998771`) | `changes` 12s, `deploy-api` 2m03s, `deploy-web` 2m46s, todos verdes |
| API em produção | `/health` `version=725afd5`; `/ready` 200 |
| Web em produção | `/health` `version=725afd5`; `/` e `/condicoes` 200 |
| Deploy manual anterior (`44335514188`) | API/web em `9bc18e0`, usado para validar token/webhooks |
| Deploy da consolidação dos docs (`44348792718`) | `f50560a`: `deploy-api` 1m58s, `deploy-web` 2m49s, verdes |

## Proteção da main

Estado atual: ruleset **`main-protection`** (id `27557615`), enforcement ativo, target `main`.

- Require a pull request before merging (0 aprovações; projeto solo).
- Require status checks: **`gate`**, strict (branch precisa estar atualizada).
- Block force pushes.
- Restrict deletions.
- Merge por merge commit, squash ou rebase.

Para recriar/verificar:

```bash
gh api repos/AbsonDev/abson/rulesets/27557615 \
  --jq '{name,enforcement,rules:[.rules[]|{type,parameters}]}'
```

O check precisa existir no repositório antes de ser exigido. Confirme o nome real com:

```bash
gh api "repos/AbsonDev/abson/commits/$(git rev-parse HEAD)/check-runs" \
  --jq '.check_runs[] | "\(.name)\t\(.app.slug)"' | sort -u
```

Use exatamente o `name` retornado (`gate`, salvo se o job for renomeado). O rótulo visual
`fisga-pipeline / gate` não é o contexto do check.

## Rollback

### Pelo painel

1. Abrir o app no Coolify.
2. Ir em **Rollback**.
3. Escolher a imagem anterior e confirmar.
4. Verificar `https://api.fisga.abson.tech/health` ou `https://fisga.abson.tech/health`.
5. Rodar o smoke: `/ready`, `/`, `/condicoes`.

### Pela API do Coolify

O token precisa de `read` para listar e `deploy` para executar.

```bash
# Listar imagens disponíveis para rollback
curl -fsS --proto '=https' "$COOLIFY_URL/api/v1/applications/$APP_UUID/rollback-images" \
  -H "Authorization: Bearer $COOLIFY_TOKEN"

# Voltar para uma tag/commit anterior
curl --fail-with-body -sS --proto '=https' -X POST \
  "$COOLIFY_URL/api/v1/applications/$APP_UUID/rollback" \
  -H "Authorization: Bearer $COOLIFY_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"commit":"sha-<commit-anterior>"}'
```

`COOLIFY_URL` é a base HTTPS, sem `/api/v1`. Rollback de imagem **não** desfaz migrations, volumes
ou variáveis. Para mudanças de schema, usar expand/contract e backup.

Se o smoke do web falhar, faça rollback do web. Se o web atual falhar depois do deploy da API, faça
rollback da API antes de tentar o web de novo.

## Migrations na Fase A

O Fisga ainda aplica migrations no startup (`InicializarBancoAsync` → `Database.MigrateAsync`).

Regras enquanto isso:

- Só deployar migrations aditivas e compatíveis com a versão anterior.
- Mudanças destrutivas (drop/rename/coluna NOT NULL sem default) exigem expand/contract em
  releases separados.
- Fazer backup antes de migrations de risco.
- O EF Core 10 usa migration lock, mas o deploy ainda depende de compatibilidade entre versões.

A Fase B troca isso por um migration bundle executado antes da aplicação, com usuário de schema
separado.

## Histórico de incidentes e lições

### 1. Deploy pulado no `push` mesmo com `if` de `main`

**Sintoma:** o `fisga-pipeline` terminava verde, mas `deploy-api` e `deploy-web` apareciam como
`skipped` no push para `main`.

**Tentativas:** `if` com `github.ref == 'refs/heads/main'` (PR #11), remoção do input `deploy`
(PR #12) e `if` mínimo só com ref (PR #13). Todas continuaram pulando.

**Causa:** o CD estava acoplado ao workflow de CI e ao contexto do evento `push`; os jobs de deploy
não eram avaliados como esperado naquele grafo.

**Correção:** PR #14 separou o CD em `fisga-deploy.yml`, disparado por `workflow_run` e usando
`github.event.workflow_run.conclusion`, `head_branch` e `head_sha`.

**Lição:** deploy não é job de CI. Se o deploy precisa do resultado do CI, use `workflow_run` e
trate o SHA do run como fonte de verdade. Não reintroduza deploy no pipeline de CI.

### 2. API não subia com runtime `aspnet:11.0`

**Sintoma:** build da imagem passava, mas o container da API falhava no startup.

**Causa:** o app target é `net10.0`; `aspnet:11.0` só contém runtimes .NET 11 e não tem o runtime
10. O CI não pega isso porque `dotnet publish` valida o TFM, não a imagem base.

**Correção:** voltar o stage final para `aspnet:10.0`.

**Lição:** a imagem base do runtime precisa corresponder ao major do TFM. Ao subir major de .NET,
atualizar TFM e imagem juntos.

### 3. `SENTRY_AUTH_TOKEN` como build arg

**Sintoma:** risco de o token aparecer em `docker history`/provenance e em cache de build.

**Correção:** Dockerfile do web usa `RUN --mount=type=secret,id=sentry_auth_token`; o job `docker`
falha se `SENTRY_AUTH_TOKEN` aparecer como `ARG`/`ENV`. Na Fase A o token está desabilitado no
Coolify (buildtime/runtime `false`) e os source maps ficam desligados.

**Lição:** segredo de build só entra por BuildKit secret. Build arg é público por definição.

### 4. `/health` retornando `dev`

**Sintoma:** o deploy pollava a versão e dava timeout; `/health` respondia `version: dev`.

**Causa:** o build não recebia `SOURCE_COMMIT`.

**Correção:** **Source commit availability = Available during build** no Coolify; Dockerfiles
mapeiam `SOURCE_COMMIT` para `GIT_SHA`. Confirmado em `725afd5`.

**Lição:** o health check só é confiável se a versão vier do build, não de um valor fixo.

### 5. Push de workflow exige escopo `workflow`

**Sintoma:** `git push` de `.github/workflows/**` era rejeitado com o token OAuth do `gh`
(escopos `gist`, `read:org`, `repo`).

**Correção:** usar PAT clássico com `repo` + `workflow` (efêmero, via `GH_TOKEN` e credential
helper isolado) ou `gh auth refresh -s repo -s workflow -s read:org`. Nunca colar token em chat ou
arquivo; se exposto, revogar e recriar.

**Lição:** workflow é área sensível do GitHub; trate o token como credencial de curta duração.

### 6. Dependabot com majors indesejados

**Sintoma:** PRs de major de NuGet/npm/Docker abertos sem contexto.

**Correção:** majors ignorados no `.github/dependabot.yml`; PRs #2/#3/#5/#8/#9/#10 fechados (e #6,
lote de testes). Atualizações de major agora são decisão manual.

**Lição:** em projeto solo, ruído de dependabot custa mais que benefício; deixar minors/patch
automáticos e majors sob controle.

### 7. Check obrigatório "Expected" com jobs verdes

**Causa:** contexto errado (`fisga-pipeline / gate` em vez de `gate`).

**Correção:** usar o `name` exato retornado por `check-runs`. O ruleset atual exige `gate`.

## Troubleshooting

### O `fisga-deploy` não disparou

Conferir, nesta ordem:

1. O `fisga-pipeline` da `main` concluiu `success`? `workflow_run` não dispara em `failure`,
   `cancelled` ou `skipped`.
2. O evento foi push na `main`? O filtro `branches: [main]` do `workflow_run` olha a branch do run
   de CI.
3. O nome do workflow de CI ainda é `fisga-pipeline`? Renomear o `name:` quebra o gatilho.
4. O run de deploy existe mas os jobs estão `skipped`? Conferir `conclusion`, `head_branch` e o
   output `changes.fisga`.
5. Ações do GitHub fora do ar ou fila lenta: ver **Actions → fisga-deploy**.

### O deploy rodou, mas `changes.fisga=false`

O diff do commit não tocou em `Projetos/fisga/**` nem nos workflows do Fisga. Se o merge era de um
PR do Fisga, confira se o checkout usou `head_sha` correto e se o commit tem pai (merge commit).
Mudança só de documentação (`docs/**`, `README.md`, `AGENTS.md`) também resulta em `fisga=false` — é
o comportamento esperado (ver [Documentação não deploya](#documentação-não-deploya)).

### `/health` retorna `dev`

O build não recebeu `SOURCE_COMMIT`/`GIT_SHA`. No Coolify, conferir
**Advanced → Build → Source commit availability = Available during build**. O deploy job vai
falhar no poll de versão até isso ser corrigido.

### Check obrigatório fica “Expected / Waiting” mesmo com jobs verdes

O nome exigido precisa ser exatamente o `name` do job (`gate`). `fisga-pipeline / gate` é apenas o
rótulo visual do workflow. Confira o contexto real:

```bash
gh api "repos/AbsonDev/abson/commits/$(git rev-parse HEAD)/check-runs" \
  --jq '.check_runs[] | "\(.name)\t\(.app.slug)"' | sort -u
```

Se a regra estiver com `fisga-pipeline / gate`, remova e selecione `gate`.

### Deploy falha com 403 ao consultar o status

O `COOLIFY_TOKEN` precisa de `read` **e** `deploy`. Token só com `deploy` consegue disparar o
webhook, mas não consultar `/api/v1/deployments/{uuid}` nem listar rollback. Token escopado nos
apps errados também retorna 403/404.

### Webhook recusado por HTTP

O workflow exige HTTPS para `COOLIFY_URL` e para os dois webhooks. Use o domínio HTTPS do Coolify
(ex.: `https://manager.abson.tech`), não o IP/porta HTTP.

### Deploy fica em timeout

O workflow acompanha o deployment por até 20 minutos e depois o `/health` por até 5 minutos.
Conferir o log do deployment no Coolify, o build do Dockerfile e se o health check está em
`/ready` (API) ou `/health` (web).

### Deploy passou, mas o site não atualizou

Conferir:

- log do deployment no Coolify;
- se o webhook chamado é do app correto;
- se o health check do app está em `/ready` (API) ou `/health` (web);
- se o Traefik está roteando para o container novo;
- se o `version` no `/health` mudou.

### Dois deploys ao mesmo tempo

O workflow inteiro usa `concurrency: fisga-production` com `cancel-in-progress: false`: runs são
enfileirados, não cancelados. Se aparecer deploy concorrente, alguém disparou pelo painel do
Coolify; espere o primeiro terminar ou cancele o manual.

### Docker build falhou no PR

Conferir o log do job `docker`. O build da API usa contexto `Projetos/fisga`; o do web usa
`Projetos/fisga/web`. O `.dockerignore` do web precisa excluir `node_modules`, `dist` e `.astro`.
O job também falha se `SENTRY_AUTH_TOKEN` aparecer como `ARG`/`ENV` no Dockerfile.

### Rollback não volta o banco

Rollback de imagem não reverte migration. Restaurar backup ou aplicar uma migration corretiva.

## Checklist de verificação ponta a ponta

Feito em 18/11/2026:

- [x] Secrets e variável configurados (`COOLIFY_URL`, `COOLIFY_TOKEN`, os dois webhooks,
  `FISGA_PUBLIC_SENTRY_DSN`).
- [x] Coolify: auto deploy off, health checks, sem porta no host, source commit disponível,
  retenção e rolling update ajustados.
- [x] Push do branch e PR com `gate` verde.
- [x] Merge na `main`.
- [x] Ruleset `main-protection` exigindo `gate`.
- [x] `fisga-deploy` disparado por `workflow_run` e verde.
- [x] `/health` da API e do web com o SHA do merge.
- [x] Smoke `/ready`, `/`, `/condicoes`.
- [ ] Testar rollback uma vez e registrar o tempo (procedimento documentado, execução pendente).
- [x] Limpar token de setup do Coolify, chave SSH temporária e arquivos locais.

## Fase B (pendente)

- Build/push de imagens no GHCR com tags `sha-<commit>`.
- Migrar `fisga-api` e `fisga-web` para Docker Image apps no Coolify (build once).
- Rollback automatizado por SHA.
- EF Core migration bundle no entrypoint.
- E2E real (6 testes) como job nightly/CI dedicado.
- Preview deployments para PRs de UI.
- Merge queue.
- Pin de commit no deploy: PATCH `git_commit_sha` no Coolify antes do webhook, para o build não
  pegar um HEAD mais novo que o SHA validado.
- Dashboard simples de DORA metrics.

## Comandos úteis

```bash
# Backend
export PATH="$HOME/.dotnet:$PATH"   # neste host o SDK fica em ~/.dotnet
cd Projetos/fisga
dotnet restore Fisga.slnx --locked-mode
dotnet format Fisga.slnx --verify-no-changes --no-restore
dotnet build Fisga.slnx -c Release --no-restore -warnaserror
dotnet test Fisga.slnx -c Release --no-build

# Web
cd Projetos/fisga/web
npm ci
npm run check
npm run build
npm run test:e2e   # 155 testes mockados; o build precisa existir

# Workflows
gh actions-lock --verify .github/workflows/fisga-pipeline.yml \
  .github/workflows/fisga-deploy.yml .github/workflows/fisga-docs-ai.yml
actionlint .github/workflows/fisga-pipeline.yml .github/workflows/fisga-deploy.yml \
  .github/workflows/fisga-docs-ai.yml

# Deploy e produção
gh run list --repo AbsonDev/abson --workflow fisga-deploy.yml --branch main --limit 3
curl -fsS https://api.fisga.abson.tech/health
curl -fsS https://fisga.abson.tech/health
```

O Docker CLI não está disponível no ambiente do agente deste host; a validação de imagem é feita no
job `docker` do GitHub Actions e no build do Coolify.
