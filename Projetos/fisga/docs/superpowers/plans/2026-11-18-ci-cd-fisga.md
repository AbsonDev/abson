# Fisga CI/CD — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar ao Fisga CI obrigatório em todo PR e deploy automatizado e verificável no Coolify, sem tocar nos outros projetos.

**Architecture:** Dois workflows. O `fisga-pipeline.yml` roda em todo PR e push para `main`; o job `changes` detecta se algo do Fisga mudou, `api` e `web` rodam só quando necessário, e `gate` sempre roda e é o único check obrigatório. O deploy vive em `fisga-deploy.yml`, disparado por `workflow_run` quando o CI conclui verde na `main`; ele dispara o `fisga-api` e depois o `fisga-web` no Coolify e valida a versão real no ar.

**Tech Stack:** GitHub Actions, .NET 10, xUnit, Astro 7, Coolify API, Sentry.

**Spec:** pesquisa desta conversa + `Projetos/fisga/README.md`.

## Global Constraints

- Só alterar `Projetos/fisga/**`, `.github/workflows/fisga-pipeline.yml`,
  `.github/workflows/fisga-deploy.yml`, `.github/dependabot.yml` e, se necessário,
  `.github/CODEOWNERS`.
- Não alterar `apps/**`, `Projetos/precifica/**` ou `Projetos/cardapio-no-lucro/**`.
- Não implementar Identity/Google.
- Não implementar migration bundle nem GHCR na Fase A.
- Não usar self-hosted runner.
- Não usar `pull_request_target`.
- Deploy só na `main`, via `workflow_run` do `fisga-pipeline` (não usar `push` nem `inputs` no
  `if` do deploy).
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

- [x] Criar `global.json` com SDK `10.0.100` e `rollForward: latestFeature` (o host tem 10.0.403).
- [x] Criar `Directory.Build.props` com `RestorePackagesWithLockFile`, `ContinuousIntegrationBuild`, `Deterministic` e `TreatWarningsAsErrors`.
- [x] Rodar `dotnet restore Fisga.slnx` e commitar os `packages.lock.json`.
- [x] Rodar `dotnet build Fisga.slnx -c Release` e `dotnet test Fisga.slnx -c Release`.
- [x] Rodar `dotnet format Fisga.slnx` uma vez, commitar o baseline e validar `dotnet format Fisga.slnx --verify-no-changes`.
- [x] Criar `.github/dependabot.yml` para `github-actions`, `nuget` em `/Projetos/fisga`, `npm` em `/Projetos/fisga/web` e `docker` em `/Projetos/fisga`, `/Projetos/fisga/src/Fisga.Api` e `/Projetos/fisga/web`.
- [x] Commit: `chore(fisga): add reproducible dotnet build baseline`.

**Verificação:** `dotnet restore Fisga.slnx --locked-mode`, build e os 250 testes passam; `dotnet format --verify-no-changes` passa.

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

- [x] Na API, calcular `versao` a partir de `GIT_SHA`, depois `SOURCE_COMMIT`, com fallback `dev`.
- [x] Configurar `opcoes.Release = versao` no `UseSentry`.
- [x] Alterar `/health` para retornar `{ status = "ok", version = versao }`.
- [x] No web, `/health` retorna `version: process.env.GIT_SHA || process.env.SOURCE_COMMIT || 'dev'`.
- [x] Nos dois Dockerfiles, adicionar no stage final `ARG GIT_SHA=`, `ARG SOURCE_COMMIT=`, `ENV GIT_SHA=${GIT_SHA:-$SOURCE_COMMIT}`.
- [x] No web, adicionar `ENV SENTRY_RELEASE=$GIT_SHA` e `ARG PUBLIC_SENTRY_RELEASE=` + `ENV PUBLIC_SENTRY_RELEASE=$PUBLIC_SENTRY_RELEASE` no build.
- [x] Verificar com `GIT_SHA=test123` via `dotnet run` e `npm run start`.
- [x] Commit: `feat(fisga): expose build version in health checks`.

---

### Task 2: Sentry token fora do build arg

**Files:**
- Modify: `Projetos/fisga/web/Dockerfile`

- [x] Adicionar `# syntax=docker/dockerfile:1` no topo.
- [x] Remover `ARG SENTRY_AUTH_TOKEN` e `ENV SENTRY_AUTH_TOKEN`.
- [x] Usar `RUN --mount=type=secret,id=sentry_auth_token,env=SENTRY_AUTH_TOKEN npm run build`.
- [x] Confirmar que o Dockerfile não tem `ARG`/`ENV SENTRY_AUTH_TOKEN` e usa `RUN --mount=type=secret,id=sentry_auth_token,env=SENTRY_AUTH_TOKEN`.
- [x] O job `docker` do CI valida o Dockerfile; a inspeção do **valor** em `docker history`/provenance fica pendente no host sem Docker (Fase B, no primeiro build com token).
- [x] Commit: `fix(fisga): mount sentry token as build secret`.

---

### Task 3: Workflow de CI

**Files:**
- Create: `.github/workflows/fisga-pipeline.yml`

- [x] Criar jobs `changes`, `api`, `web`, `docker` e `gate`.
- [x] Não usar `paths:` no gatilho; o job `changes` decide o que executa.
- [x] `permissions: contents: read` no topo; `concurrency` cancela runs antigos só em PR.
- [x] Usar `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-node@v7`, `actions/upload-artifact@v7`.
- [x] No `api`: `dotnet restore Fisga.slnx --locked-mode`, `dotnet format Fisga.slnx --verify-no-changes --no-restore`, `dotnet build Fisga.slnx -c Release --no-restore -warnaserror`, `dotnet test Fisga.slnx -c Release --no-build --logger "trx;LogFileName=fisga-tests.trx" --results-directory artifacts/test-results`.
- [x] No `web`: `npm ci`, `npm run check`, `npm run build`, `npx playwright install --with-deps chromium` e `npm run test:e2e` (61 mockados).
- [x] No `gate`: `if: always()`, `needs: [changes, api, web, docker]`, falhar em `failure` ou `cancelled`.
- [x] ~~Adicionar `workflow_dispatch` com input booleano `deploy` default `false`~~ — descartado: o deploy virou `workflow_run` separado e não usa input.
- [x] Validar com `actionlint`.
- [x] Abrir PR de teste: mudança no Fisga roda tudo; mudança fora do Fisga skipa `api`/`web` e o `gate` passa.
- [x] Commit: `ci(fisga): add pull request pipeline`.

---

### Task 4: Deploy automatizado no Coolify

> **Implementação final:** o CD foi separado em `.github/workflows/fisga-deploy.yml`, disparado por
> `workflow_run` quando `fisga-pipeline` conclui verde na `main`. O `fisga-pipeline` ficou só com
> CI/gate, sem jobs de deploy.

**Files:**
- Modify: `.github/workflows/fisga-pipeline.yml`
- Create: `.github/workflows/fisga-deploy.yml`

- [x] Criar `deploy-api` e `deploy-web` no workflow separado `fisga-deploy.yml`, disparado por `workflow_run` (`fisga-pipeline`, `completed`, `branches: [main]`).
- [x] Usar `environment: production`, `concurrency: fisga-production` no workflow, `cancel-in-progress: false` e timeout de 30 min por job.
- [x] Secrets: `COOLIFY_URL` (HTTPS), `COOLIFY_TOKEN` (read+deploy), `COOLIFY_WEBHOOK_FISGA_API`, `COOLIFY_WEBHOOK_FISGA_WEB`.
- [x] Var: `FISGA_PUBLIC_SENTRY_DSN` (variável de repositório).
- [x] Exigir HTTPS nos webhooks e no `COOLIFY_URL`; usar `curl --proto '=https'`.
- [x] Disparar webhook com `POST`, capturar `deployment_uuid` e pollar o status do deployment no Coolify (até 20 min).
- [x] Depois do deployment, pollar `/health` até `version == workflow_run.head_sha` (até 5 min).
- [x] Smoke: `/ready`, `/`, `/condicoes`; após a API, validar o web atual antes de trocar o web.
- [x] No Coolify: desligar deploy on push, health check `/ready` na API e `/health` no web, remover portas no host, desligar consistent container names, confirmar rolling update e **Source commit availability = Available during build**. O secret do Sentry deve ser BuildKit secret `sentry_auth_token`, nunca build variable. Também: retenção de 10 imagens, stop grace 30s e rolling drain 30s.
- [x] Testar com merge na `main` (deploy automático `44338998771`, verde).
- [x] Commit: `ci(fisga): split deploy into workflow_run`.

---

### Task 5: Proteção da main + primeiro deploy real

- [x] Criar ruleset na `main`: require PR, require check `gate`, require branches up to date, block force pushes. Confirmar o contexto exato em `gh api repos/AbsonDev/abson/commits/<sha>/check-runs`. Ruleset `main-protection` id `27557615`; 0 aprovações (projeto solo).
- [x] Testar PR pequeno: merge bloqueado até o gate verde (PRs #11–#14).
- [x] Fazer o primeiro merge real e acompanhar o deploy: manual `44335514188` (API/web em `9bc18e0`) e automático `44338998771` (`725afd5`).
- [x] Registrar duração do CI e do deploy: deploy `changes` 12s, `deploy-api` 2m03s, `deploy-web` 2m46s.
- [ ] Testar rollback no Coolify e documentar o tempo (procedimento documentado; execução pendente).
- [x] Verificar `/health` da API e do web com o SHA do commit (`725afd5`; `/ready`, `/` e `/condicoes` 200).

---

### Task 6: Documentação e runbook

**Files:**
- Modify: `Projetos/fisga/README.md`
- Create: `Projetos/fisga/docs/ci-cd.md`
- Modify: `Projetos/README.md`

- [x] Documentar fluxo PR → CI → gate → merge → deploy API → deploy web → smoke.
- [x] Documentar check obrigatório, secrets/vars, configuração do Coolify e rollback.
- [x] Documentar política de migrations: só aditivas na Fase A; expand/contract para mudanças quebráveis.
- [x] Documentar troubleshooting e roadmap da Fase B.
- [x] Atualizar o README de `Projetos` com o estado de CI/CD do Fisga.
- [x] Commit: `docs(fisga): document ci cd and rollback runbook`.

---

### Task 7: Atualizar o prompt principal

**Files:**
- Modify: `~/.config/opencode/AGENTS.md`

- [x] Adicionar uma seção “Fisga — CI/CD” com workflow, check obrigatório, secrets (apenas nomes), apps Coolify, comandos locais e pendências da Fase B.
- [x] Registrar que `dotnet` fica em `~/.dotnet/dotnet` e não está no `PATH` padrão deste host.
- [x] Registrar que o Docker CLI não está disponível neste host; builds de imagem são validados no CI.
- [x] Não incluir valores de secrets, tokens ou DSNs.
- [x] Commit não se aplica (arquivo fora do repositório); validar com leitura.

---

### Task 8: Fase B (backlog, não implementar agora)

- [ ] Build/push de imagens no GHCR com tags `sha-<commit>`.
- [ ] Migrar `fisga-api` e `fisga-web` para Docker Image apps no Coolify.
- [ ] Rollback automatizado por SHA.
- [ ] EF Core migration bundle no entrypoint.
- [ ] E2E real (6 testes) no CI/nightly.
- [ ] Preview deployments para PRs de UI.
- [ ] Merge queue.
- [ ] Excluir `docs/**` e markdown do filtro do deploy (hoje mudança só de doc gera deploy idempotente).
- [ ] Pin de commit no deploy (PATCH `git_commit_sha` antes do webhook).
- [ ] Dashboard simples de DORA metrics.

---

## Resultado (18/11/2026)

**Fase A concluída e validada em produção no commit `725afd5`.**

- CI: `fisga-pipeline` com `changes`, `api` (250 testes), `web` (61 Playwright mockados), `docker`
  e `gate`; PR #14 com todos os checks verdes no run `44338126881`.
- CD: `fisga-deploy` via `workflow_run`; deploy automático `44338998771` verde (`changes` 12s,
  `deploy-api` 2m03s, `deploy-web` 2m46s).
- Produção: API e web em `725afd5`; `/health` com o SHA, `/ready`, `/` e `/condicoes` 200.
- Proteção: ruleset `main-protection` (id `27557615`) exigindo `gate`, sem aprovação extra.
- Coolify: auto deploy off, source commit no build, retenção 10, stop grace 30s, rolling drain 30s.
- Segurança: `COOLIFY_TOKEN` escopado aos dois apps (read+deploy, 90 dias); token de setup e chave
  SSH temporária removidos; backups mantidos no VPS.

**Desvios do plano original:**

- O deploy não ficou no `fisga-pipeline`: virou `fisga-deploy.yml` por `workflow_run` (PR #14).
- O `workflow_dispatch` do CI não tem input de deploy; deploy manual é feito pelo painel/API do
  Coolify.
- O teste de rollback ainda não foi executado; o procedimento está no runbook.
- A inspeção do valor do `SENTRY_AUTH_TOKEN` em `docker history` fica para a Fase B; na Fase A o
  token está desabilitado no Coolify e os source maps estão desligados.
