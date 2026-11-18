# CI/CD do Fisga

Este documento descreve o pipeline, o deploy no Coolify, o rollback e as pendências da Fase B.

## Visão geral

```text
PR
└── fisga-pipeline
    ├── changes   (detecta mudanças em Projetos/fisga/**)
    ├── api       (restore locked + format + build + 82 testes)
    ├── web       (npm ci + astro check + build)
    ├── docker    (build das imagens sem push, em PR)
    └── gate      (check obrigatório)

push em main
└── fisga-pipeline
    ├── changes + api + web + gate
    ├── deploy-api  (Coolify -> aguarda /health com o SHA)
    └── deploy-web  (Coolify -> aguarda /health com o SHA + smoke)
```

O workflow fica em `.github/workflows/fisga-pipeline.yml`. O check obrigatório da `main` é
**`fisga-pipeline / gate`**.

## Jobs de CI

### `changes`

Roda em todo PR e push para `main`, sem `paths:` no gatilho. Isso evita o problema de um workflow
pulado não reportar o check obrigatório. O job usa `git diff` para decidir se o Fisga mudou e expõe
`needs.changes.outputs.fisga`.

### `api`

- `dotnet restore Fisga.slnx --locked-mode`
- `dotnet format Fisga.slnx --verify-no-changes --no-restore`
- `dotnet build Fisga.slnx -c Release --no-restore -warnaserror`
- `dotnet test Fisga.slnx -c Release --no-build`

Os resultados TRX são enviados como artifact `fisga-test-results`.

### `web`

- `npm ci`
- `npm run check` (`astro check`)
- `npm run build` (Astro + Pagefind + precache offline)

### `docker`

Roda em PR e em `workflow_dispatch`. Constrói as imagens da API e do web sem push, para validar
Dockerfiles, `.dockerignore` e o secret mount do Sentry. Não recebe `SENTRY_AUTH_TOKEN` em PRs de
fork; nesse build os source maps ficam desabilitados.

### `gate`

Sempre roda (`if: always()`) e falha se qualquer job necessário falhar ou for cancelado. É o único
check que precisa ser obrigatório na proteção da `main`.

## Deploy

O deploy só roda em:

- push para `main`; ou
- `workflow_dispatch` com `deploy=true`.

Os apps usados são `fisga-api` e `fisga-web` no Coolify. O CI dispara o webhook autenticado de cada
app, espera o `/health` retornar o SHA do commit e roda smoke tests.

### Ordem

1. `deploy-api`: dispara o Coolify, aguarda `https://api.fisga.abson.tech/health` com
   `version == github.sha`, valida `/ready`.
2. `deploy-web`: dispara o Coolify, aguarda `https://fisga.abson.tech/health` com
   `version == github.sha`, valida `/` e `/condicoes`.

Se a API falhar, o web não é deployado. Se o web falhar, a API nova continua no ar; as mudanças
precisam ser compatíveis com a versão anterior do web.

### Secrets e variáveis do GitHub

Configurar em **Settings → Secrets and variables → Actions** (repo ou environment `production`):

| Nome | Tipo | Uso |
| --- | --- | --- |
| `COOLIFY_TOKEN` | secret | Bearer token com permissão `deploy`; `read` se for usar rollback via API |
| `COOLIFY_WEBHOOK_FISGA_API` | secret | Deploy webhook autenticado do app `fisga-api` |
| `COOLIFY_WEBHOOK_FISGA_WEB` | secret | Deploy webhook autenticado do app `fisga-web` |
| `FISGA_PUBLIC_SENTRY_DSN` | variable | DSN público do Sentry usado no build do browser |

Os segredos de runtime (banco, Resend, Sentry server, mídia, newsletter) continuam no Coolify, não
no GitHub. O `SENTRY_AUTH_TOKEN` é usado no build do web; na Fase A ele fica no Coolify (se o build
secret estiver disponível) ou os source maps ficam desabilitados. Nunca passar esse token como
`ARG`/`ENV` de Dockerfile.

### Configuração do Coolify

Para `fisga-api` e `fisga-web`:

- **Auto deploy**: desligado. O deploy é disparado só pelo CI.
- **Health check**: API em `/ready`; web em `/health`.
- **Portas**: usar a porta interna e o Traefik. Não mapear porta no host, para não bloquear o
  rolling update.
- **Container name**: não usar nome fixo/consistente, pelo mesmo motivo.
- **Rolling update**: confirmar no log do deployment que o Coolify iniciou o novo container antes
  de remover o antigo.
- **Build arg**: confirmar que o Coolify passa `SOURCE_COMMIT`. O Dockerfile mapeia
  `SOURCE_COMMIT` para `GIT_SHA`; o `/health` usa esse valor.
- **Volume**: manter o volume de mídia do Fisga; ele não é afetado pelo deploy.

O Coolify atualiza a imagem a partir do commit; o health check precisa retornar o SHA exato. Se
`/health` retornar `dev`, o `SOURCE_COMMIT` não chegou ao build.

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
curl -fsS "$COOLIFY_URL/api/v1/applications/$APP_UUID/rollback-images" \
  -H "Authorization: Bearer $COOLIFY_TOKEN"

# Voltar para uma tag/commit anterior
curl --fail-with-body -sS -X POST \
  "$COOLIFY_URL/api/v1/applications/$APP_UUID/rollback" \
  -H "Authorization: Bearer $COOLIFY_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"commit":"sha-<commit-anterior>"}'
```

Rollback de imagem **não** desfaz migrations, volumes ou variáveis. Para mudanças de schema, usar
expand/contract e backup.

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

## Fase B (pendente)

- Build/push de imagens no GHCR com tags `sha-<commit>`.
- Migrar `fisga-api` e `fisga-web` para Docker Image apps no Coolify (build once).
- Rollback automatizado por SHA.
- EF Core migration bundle no entrypoint.
- Playwright smoke no CI.
- Preview deployments para PRs de UI.
- Merge queue.
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

# Workflow
gh actions-lock --verify .github/workflows/fisga-pipeline.yml
actionlint .github/workflows/fisga-pipeline.yml
```

O Docker CLI não está disponível no ambiente do agente deste host; a validação de imagem é feita no
job `docker` do GitHub Actions.

## Proteção da main

Depois que o workflow estiver na `main` e tiver rodado pelo menos uma vez:

1. Abrir **Settings → Rules → Rulesets → New branch ruleset**.
2. Target: `main`.
3. Ativar **Require a pull request before merging**.
4. Ativar **Require status checks to pass** e selecionar `fisga-pipeline / gate`.
5. Ativar **Require branches to be up to date before merging**.
6. Ativar **Block force pushes**.
7. Salvar.

O check precisa existir no repositório antes de ser exigido. Se o GitHub não listar
`fisga-pipeline / gate`, rodar o workflow em um PR primeiro.

## Checklist de ativação (primeira vez)

- [ ] Configurar `COOLIFY_TOKEN`, `COOLIFY_WEBHOOK_FISGA_API`, `COOLIFY_WEBHOOK_FISGA_WEB` e a
  variável `FISGA_PUBLIC_SENTRY_DSN`.
- [ ] No Coolify, desligar auto deploy, configurar health checks, remover portas no host e confirmar
  que `SOURCE_COMMIT` chega ao build.
- [ ] Fazer push do branch e abrir PR.
- [ ] Confirmar que `fisga-pipeline / gate` aparece e passa.
- [ ] Mergear o PR.
- [ ] Criar a proteção da `main` exigindo `fisga-pipeline / gate`.
- [ ] Rodar `workflow_dispatch` com `deploy=true`.
- [ ] Confirmar `/health` com o SHA do commit e smoke tests.
- [ ] Testar rollback uma vez e registrar o tempo.

## Troubleshooting

### `/health` retorna `dev`

O build não recebeu `SOURCE_COMMIT`/`GIT_SHA`. Conferir o build arg no Coolify ou o `ARG` no
Dockerfile. O deploy job vai falhar no poll de versão até isso ser corrigido.

### Deploy passou, mas o site não atualizou

Conferir:

- log do deployment no Coolify;
- se o webhook chamado é do app correto;
- se o health check do app está em `/ready` (API) ou `/health` (web);
- se o Traefik está roteando para o container novo;
- se o `version` no `/health` mudou.

### Check obrigatório não aparece no PR

O workflow precisa rodar em todo PR (sem `paths:` no gatilho) e o job `gate` precisa executar
sempre. Se o workflow foi pulado, o required check fica pendente.

### Docker build falhou no PR

Conferir o log do job `docker`. O build da API usa contexto `Projetos/fisga`; o do web usa
`Projetos/fisga/web`. O `.dockerignore` do web precisa excluir `node_modules`, `dist` e `.astro`.

### Rollback não volta o banco

Rollback de imagem não reverte migration. Restaurar backup ou aplicar uma migration corretiva.
