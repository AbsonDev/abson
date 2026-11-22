# AGENTS.md — Fisga

Contexto versionado para agentes de código que trabalham no Fisga. Leia junto
com [`docs/INDEX.md`](docs/INDEX.md) antes de propor mudanças.

## Visão em uma frase

O Fisga é um app de pesca que registra capturas com identidade própria, diário
privado, condições do mar e um mapa particular do pescador.

Stack: ASP.NET Core 10 (Minimal APIs, EF Core 10) + PostgreSQL 17 em produção
(SQLite no desenvolvimento) + Astro SSR para o web.

## Comandos base

Na raiz de `Projetos/fisga`:

```bash
export PATH="$HOME/.dotnet:$PATH"   # o SDK fica em ~/.dotnet/dotnet

dotnet restore Fisga.slnx --locked-mode
dotnet build Fisga.slnx -c Release --no-restore -warnaserror
dotnet test Fisga.slnx -c Release --no-build

cd web
npm ci
npm run check
npm run build
```

## Arquitetura em 5 linhas

1. `Domain` — agregados, invariantes e linguagem ubíqua; não conhece HTTP.
2. `Application` — casos de uso e serviços que orquestram o domínio.
3. `Infrastructure` — EF Core, PostgreSQL e integrações externas.
4. `Api` — Minimal APIs em `src/Fisga.Api/Endpoints/`, contratos em `Contracts/`.
5. `web/` — Astro SSR, consome a API por `PUBLIC_API_URL`.

## Invariantes de CI/CD

Resumo do que está em [`docs/ci-cd.md`](docs/ci-cd.md) (o runbook é a fonte):

- **Deploy é `workflow_run`, não job de CI.** `fisga-deploy.yml` dispara
  quando o `fisga-pipeline` conclui com sucesso na `main`, e usa
  `github.event.workflow_run.head_sha`; nada de `inputs` ou `github.ref` no
  `if` do deploy.
- **`gate` é o único check obrigatório.** O contexto exigido pela proteção da
  `main` é exatamente `gate`; o CI não usa `paths:` para nunca deixar o check
  "Expected".
- **Documentação não builda nem deploya.** O filtro de código ignora
  `Projetos/fisga/docs/**`, `docs-site/**`, `README.md` e `AGENTS.md`; PR só de docs não roda
  nenhum job de build da API/web; roda o job `docs` e, quando toca `docs-site/**`,
  `docs/publico/**` ou `docs/api/openapi.json`, roda o job `docs-site` (gerador e verificação pós-build).
  No CI, `Projetos/fisga/docs/api/openapi.json` também conta como código para o job `api`.
  No deploy, documentação continua sob `docs/**` e `docs-site/**` e não dispara os jobs de deploy
  (o deploy do portal estático é escopo da F2.5).
- **Runtime da API é `aspnet:10.0`.** O TFM é `net10.0`; imagem base de major
  diferente não sobe.
- **`SENTRY_AUTH_TOKEN` só entra por BuildKit secret** (`sentry_auth_token`).
  Nunca como `ARG`/`ENV` ou build variable.
- **Não mergeie durante um deploy.** O Coolify builda o HEAD da branch e o
  health check compara com o SHA do run; mergear no meio quebra a verificação.

## Documentação

- A fonte é `docs/`; a porta de entrada é [`docs/INDEX.md`](docs/INDEX.md).
- `docs/api/openapi.json` é gerado por `scripts/gerar-openapi.sh` — nunca
  edite à mão.
- Mudou comportamento, contrato, configuração ou operação? Atualize a
  documentação no mesmo PR e indexe o documento novo no `INDEX.md`.

## Nunca

- Commitar segredo (`.env`, token, DSN, chave, senha).
- Usar `pull_request_target` em workflow.
- Documentar endpoint à mão em vez de regenerar o contrato: o
  `docs/api/openapi.json` é a fonte da verdade. A tabela `## API` do `README.md`
  e o inventário listam **todas** as operações, e `scripts/verificar-rotas.mjs`
  guarda os três no job `docs` — rota nova entra no contrato, no `## API` e no
  inventário no mesmo PR.
- Mergear com thread de review aberta.

## Antes de abrir PR

Rode os comandos de build/teste acima e o lint de Markdown:

```bash
npx --yes markdownlint-cli2@0.24.1
```
