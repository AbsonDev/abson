# Fisga CI/CD — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar ao Fisga CI obrigatório em todo PR e deploy automatizado e verificável no Coolify, sem tocar nos outros projetos.

**Architecture:** Um workflow `fisga-pipeline.yml` roda em todo PR e push para `main`. O job `changes` detecta se algo do Fisga mudou; `api` e `web` rodam só quando necessário; `gate` sempre roda e é o único check obrigatório. No push para `main`, após o gate, o workflow dispara o deploy do `fisga-api` e depois do `fisga-web` no Coolify e valida a versão real no ar.

**Tech Stack:** GitHub Actions, .NET 10, xUnit, Astro 7, Coolify API, Sentry.

**Spec:** pesquisa desta conversa + `Projetos/fisga/README.md`.

## Global Constraints

- Só alterar `Projetos/fisga/**`, `.github/workflows/fisga-pipeline.yml`, `.github/dependabot.yml` e, se necessário, `.github/CODEOWNERS`.
- Não alterar `apps/**`, `Projetos/precifica/**` ou `Projetos/cardapio-no-lucro/**`.
- Não implementar Identity/Google.
- Não implementar migration bundle nem GHCR na Fase A.
- Não usar self-hosted runner.
- Não usar `pull_request_target`.
- Deploy só em push para `main` ou `workflow_dispatch` com `deploy=true`.
- Nenhum segredo em arquivo, log ou build arg.
- Actions pinadas por SHA ou lockfile; permissões mínimas.
- Commits pequenos, uma task por commit.
- Código novo com teste; infra validada por comando.

## Review Focus

- PR de fork não pode acessar `COOLIFY_TOKEN` nem disparar deploy.
- PR que não mexe no Fisga não pode ficar bloqueado por check obrigatório ausente.
- Health check não pode reportar container antigo como se fosse a versão nova.
- `SENTRY_AUTH_TOKEN` não pode aparecer em `docker history`, provenance ou cache.
- Deploy API → web precisa continuar compatível; falha no web não pode deixar o site quebrado.
- Startup migration + rolling update: só migrations aditivas na Fase A.
- Coolify pode desabilitar rolling update silenciosamente se houver porta no host ou nome fixo de container; validar no log.

---

### Task 0: Baseline .NET reproduzível

**Files:**
- Create: `Projetos/fisga/global.json`
- Create: `Projetos/fisga/Directory.Build.props`
- Generate: `Projetos/fisga/**/packages.lock.json`
- Create: `.github/dependabot.yml`

- [ ] Criar `global.json` com SDK `10.0.100` e `rollForward: latestFeature` (o host tem 10.0.403).
- [ ] Criar `Directory.Build.props` com `RestorePackagesWithLockFile`, `ContinuousIntegrationBuild`, `Deterministic` e `TreatWarningsAsErrors`.
- [ ] Rodar `dotnet restore Fisga.slnx` e commitar os `packages.lock.json`.
- [ ] Rodar `dotnet build Fisga.slnx -c Release` e `dotnet test Fisga.slnx -c Release`.
- [ ] Se `TreatWarningsAsErrors` quebrar, corrigir os warnings ou listar exceções justificadas em `WarningsNotAsErrors`.
- [ ] Rodar `dotnet format Fisga.slnx` uma vez, commitar o baseline e validar `dotnet format Fisga.slnx --verify-no-changes`.
- [ ] Criar `.github/dependabot.yml` para `github-actions`, `nuget` em `/Projetos/fisga`, `npm` em `/Projetos/fisga/web` e `docker` em `/Projetos/fisga`, `/Projetos/fisga/src/Fisga.Api` e `/Projetos/fisga/web`.
- [ ] Commit: `chore(fisga): add reproducible dotnet build baseline`.

**Verificação:** `dotnet restore Fisga.slnx --locked-mode`, build e os 82 testes passam; `dotnet format --verify-no-changes` passa.

---

### Task 1: Versão real no `/health`

**Files:**
- Modify: `Projetos/fisga/src/Fisga.Api/Program.cs`
- Modify: `Projetos/fisga/src/Fisga.Api/Dockerfile`
- Modify: `Projetos/fisga/web/src/pages/health.ts`
- Modify: `Projetos/fisga/web/Dockerfile`

**Interfaces:**
- `GET /health` da API e do web passam a retornar `{ "status": "ok", "version": "<commit>" }`.
- Build args: `GIT_SHA` e `SOURCE_COMMIT`.
- Env: `SENTRY_RELEASE`.

- [ ] Na API, calcular `versao` a partir de `GIT_SHA`, depois `SOURCE_COMMIT`, com fallback `dev`.
- [ ] Configurar `opcoes.Release = versao` no `UseSentry`.
- [ ] Alterar `/health` para retornar `{ status = "ok", version = versao }`.
- [ ] No web, `/health` retorna `version: process.env.GIT_SHA || process.env.SOURCE_COMMIT || 'dev'`.
- [ ] Nos dois Dockerfiles, adicionar no stage final `ARG GIT_SHA=`, `ARG SOURCE_COMMIT=`, `ENV GIT_SHA=${GIT_SHA:-$SOURCE_COMMIT}`.
- [ ] No web, adicionar `ENV SENTRY_RELEASE=$GIT_SHA` e `ARG PUBLIC_SENTRY_RELEASE=` + `ENV PUBLIC_SENTRY_RELEASE=$PUBLIC_SENTRY_RELEASE` no build.
- [ ] Verificar com `GIT_SHA=test123` via `dotnet run` e `npm run start`.
- [ ] Commit: `feat(fisga): expose build version in health checks`.

---

### Task 2: Sentry token fora do build arg

**Files:**
- Modify: `Projetos/fisga/web/Dockerfile`

- [ ] Adicionar `# syntax=docker/dockerfile:1` no topo.
- [ ] Remover `ARG SENTRY_AUTH_TOKEN` e `ENV SENTRY_AUTH_TOKEN`.
- [ ] Usar `RUN --mount=type=secret,id=sentry_auth_token,env=SENTRY_AUTH_TOKEN npm run build`.
- [ ] Confirmar que o Dockerfile não tem `ARG`/`ENV SENTRY_AUTH_TOKEN` e usa `RUN --mount=type=secret,id=sentry_auth_token,env=SENTRY_AUTH_TOKEN`.
- [ ] Quando houver Docker, confirmar que o **valor** do token não aparece em `docker history`/provenance; não usar `grep -i sentry`, porque o nome do secret aparece na linha do `RUN`.
- [ ] Commit: `fix(fisga): mount sentry token as build secret`.

---

### Task 3: Workflow de CI

**Files:**
- Create: `.github/workflows/fisga-pipeline.yml`

- [ ] Criar jobs `changes`, `api`, `web`, `docker` e `gate`.
- [ ] Não usar `paths:` no gatilho; o job `changes` decide o que executa.
- [ ] `permissions: contents: read` no topo; `concurrency` cancela runs antigos só em PR.
- [ ] Usar `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-node@v7`, `actions/upload-artifact@v7`.
- [ ] No `api`: `dotnet restore Fisga.slnx --locked-mode`, `dotnet format Fisga.slnx --verify-no-changes --no-restore`, `dotnet build Fisga.slnx -c Release --no-restore -warnaserror`, `dotnet test Fisga.slnx -c Release --no-build --logger "trx;LogFileName=fisga-tests.trx" --results-directory artifacts/test-results`.
- [ ] No `web`: `npm ci`, `npm run check`, `npm run build`.
- [ ] No `gate`: `if: always()`, `needs: [changes, api, web, docker]`, falhar em `failure` ou `cancelled`.
- [ ] Adicionar `workflow_dispatch` com input booleano `deploy` default `false`.
- [ ] Validar com `actionlint`.
- [ ] Abrir PR de teste: mudança no Fisga roda tudo; mudança fora do Fisga skipa `api`/`web` e o `gate` passa.
- [ ] Commit: `ci(fisga): add pull request pipeline`.

---

### Task 4: Deploy automatizado no Coolify

**Files:**
- Modify: `.github/workflows/fisga-pipeline.yml`

- [ ] Adicionar `deploy-api` e `deploy-web`, só em `main` (push ou `workflow_dispatch` na `main` com `deploy=true`).
- [ ] Usar `environment: production`, `concurrency: fisga-production`, `cancel-in-progress: false` e timeout de 30 min.
- [ ] Secrets: `COOLIFY_URL` (HTTPS), `COOLIFY_TOKEN` (read+deploy), `COOLIFY_WEBHOOK_FISGA_API`, `COOLIFY_WEBHOOK_FISGA_WEB`.
- [ ] Var: `FISGA_PUBLIC_SENTRY_DSN` (variável de repositório).
- [ ] Exigir HTTPS nos webhooks e no `COOLIFY_URL`; usar `curl --proto '=https'`.
- [ ] Disparar webhook com `POST`, capturar `deployment_uuid` e pollar o status do deployment no Coolify (até 20 min).
- [ ] Depois do deployment, pollar `/health` até `version == github.sha` (até 5 min).
- [ ] Smoke: `/ready`, `/`, `/condicoes`; após a API, validar o web atual antes de trocar o web.
- [ ] No Coolify: desligar deploy on push, health check `/ready` na API e `/health` no web, remover portas no host, desligar consistent container names, confirmar rolling update e **Source commit availability = Available during build**. O secret do Sentry deve ser BuildKit secret `sentry_auth_token`, nunca build variable.
- [ ] Testar com `workflow_dispatch` na `main` e `deploy=true`.
- [ ] Commit: `ci(fisga): deploy to coolify after ci`.

---

### Task 5: Proteção da main + primeiro deploy real

- [ ] Criar ruleset na `main`: require PR, require check `gate`, require branches up to date, block force pushes, delete head branches. Confirmar o contexto exato em `gh api repos/AbsonDev/abson/commits/<sha>/check-runs`.
- [ ] Testar PR pequeno: merge bloqueado até o gate verde.
- [ ] Fazer o primeiro merge real e acompanhar o deploy.
- [ ] Registrar duração do CI e do deploy.
- [ ] Testar rollback no Coolify e documentar o tempo.
- [ ] Verificar `/health` da API e do web com o SHA do commit.

---

### Task 6: Documentação e runbook

**Files:**
- Modify: `Projetos/fisga/README.md`
- Create: `Projetos/fisga/docs/ci-cd.md`
- Modify: `Projetos/README.md`

- [ ] Documentar fluxo PR → CI → gate → merge → deploy API → deploy web → smoke.
- [ ] Documentar check obrigatório, secrets/vars, configuração do Coolify e rollback.
- [ ] Documentar política de migrations: só aditivas na Fase A; expand/contract para mudanças quebráveis.
- [ ] Documentar troubleshooting e roadmap da Fase B.
- [ ] Atualizar o README de `Projetos` com o estado de CI/CD do Fisga.
- [ ] Commit: `docs(fisga): document ci cd and rollback runbook`.

---

### Task 7: Atualizar o prompt principal

**Files:**
- Modify: `~/.config/opencode/AGENTS.md`

- [ ] Adicionar uma seção “Fisga — CI/CD” com workflow, check obrigatório, secrets (apenas nomes), apps Coolify, comandos locais e pendências da Fase B.
- [ ] Registrar que `dotnet` fica em `~/.dotnet/dotnet` e não está no `PATH` padrão deste host.
- [ ] Registrar que o Docker CLI não está disponível neste host; builds de imagem são validados no CI.
- [ ] Não incluir valores de secrets, tokens ou DSNs.
- [ ] Commit não se aplica (arquivo fora do repositório); validar com leitura.

---

### Task 8: Fase B (backlog, não implementar agora)

- [ ] Build/push de imagens no GHCR com tags `sha-<commit>`.
- [ ] Migrar `fisga-api` e `fisga-web` para Docker Image apps no Coolify.
- [ ] Rollback automatizado por SHA.
- [ ] EF Core migration bundle no entrypoint.
- [ ] Playwright smoke no CI.
- [ ] Preview deployments para PRs de UI.
- [ ] Merge queue.
- [ ] Dashboard simples de DORA metrics.
