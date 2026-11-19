# Fisga — Plano de Implementação: Documentação Contínua

**Design:** [`../specs/2026-11-18-documentacao-continua-design.md`](../specs/2026-11-18-documentacao-continua-design.md)
**Data:** 2026-11-18
**Branch:** `feat/fisga-documentacao-continua`
**Worktree:** `/Users/absondutragalvao/.local/share/opencode/worktree/72e3a4/fisga-documentacao-continua`

---

## Decisão de entrega

O design previa cinco PRs. A execução será **um PR com seis commits**:

1. fundação documental;
2. gate determinístico (`markdownlint` + `lychee`);
3. filtros de CI e deploy (documentação não builda nem deploya aplicação);
4. OpenAPI gerado do código;
5. revisão de documentação por IA;
6. spec e plano desta entrega (registrados antes do PR).

Razões:

- Os cortes tocam os mesmos arquivos (`README.md`, `AGENTS.md`, `fisga-pipeline.yml`) e seriam rebases em série.
- Cada merge na `main` dispara um deploy; um PR = um deploy.
- O PR único permite validar o workflow de IA **nele mesmo**: o último commit cria o workflow e o push seguinte o executa.

Os commits continuam atômicos e revisáveis individualmente.

---

## Restrições globais (valem para todas as tarefas)

- Toda documentação e copy em **português do Brasil**.
- **Nenhum segredo** em Markdown, workflow, `AGENTS.md` ou README. Nunca imprimir `OPENCODE_API_KEY`, tokens do Coolify ou DSNs.
- O context obrigatório do ruleset continua sendo **`gate`**. Não criar check obrigatório novo.
- Ações de workflow **pinadas** e registradas em `.github/workflows/actions.lock`. Após qualquer mudança em `.github/workflows/**`, rodar `gh actions-lock` e commitar o lock atualizado.
- `dotnet format --verify-no-changes` e `-warnaserror` continuam valendo.
- Não usar `pull_request_target`.
- Commits convencionais, em português, um por tarefa.

## Comandos base

```bash
export PATH="$HOME/.dotnet:$PATH"   # SDK fora do PATH padrão

cd Projetos/fisga
dotnet restore Fisga.slnx --locked-mode
dotnet build Fisga.slnx -c Release --no-restore -warnaserror
dotnet test Fisga.slnx -c Release --no-build

cd web
npm ci
npm run check
npm run build
```

---

## Fatos verificados antes do plano

Levantados no worktree em 2026-11-18, para o implementador não redescobrir:

- `markdownlint-cli2@0.24.1` no corpus `AGENTS.md`, `Projetos/fisga/README.md`, `Projetos/fisga/AGENTS.md`, `Projetos/fisga/docs/**` com configuração explícita: **60 erros em 3 arquivos** depois de excluir `docs/superpowers/**` — 44 `MD060` (tabelas), 7 `MD036`, 6 `MD029`, 3 `MD040`. `--fix` resolve `MD060` e `MD029`; sobram 7 `MD036` e 3 `MD040` manuais em `ddd.md` e `ux-audit.md`. `MD013` fica desligado (linha longa é estilo, não erro).
- Os arquivos em `docs/superpowers/**` (plans, specs, runbooks) são artefatos de processo, não documentação canônica, e ficam **fora** do corpus do lint. Sem essa exclusão, o próprio plano desta entrega adiciona ~1700 avisos.
- `lychee 0.24.2` online encontra 47 links nesse corpus, **0 erros**, com `--exclude-loopback --exclude-private`. Offline também passa. Link para diretório existente é aceito.
- `Fisga.Api` já chama `AddOpenApi()` (linha 45) e `MapOpenApi()` (linha 146, só em Development).
- Geração de OpenAPI em build **funciona** com `Microsoft.Extensions.ApiDescription.Server` 10.0.15 mais `ASPNETCORE_ENVIRONMENT=Development`, `Database__ConnectionString=Data Source=<tmp>.db` e `Sentry__Dsn=`. Resultado: `Fisga.Api.json` com 48 paths e 25 schemas, **determinístico** (mesmo SHA-256 em rebuild).
- Em Production a geração falha por `Email:ResendApiKey` e por connection string PostgreSQL. Por isso o script usa Development com SQLite temporário.
- A action `anomalyco/opencode/github@latest` só aceita `issue_comment` e `pull_request_review_comment`, exige menção `/oc` e empurra commit quando o workspace fica sujo. **Não serve para rodar automaticamente em `pull_request`.**
- `opencode` CLI aceita `run --model provider/model#variant --file <arquivo> --archived --standalone --format json`. Com `--format json` a saída é JSONL; `jq -r 'select(.type == "text") | .part.text'` extrai só a resposta do modelo (testado localmente em 2026-11-18). Sem isso, logs e eventos de passo contaminam o comentário.
- O provider `opencode-go` lê `OPENCODE_API_KEY` do ambiente (models.dev: `env: ["OPENCODE_API_KEY"]`, base `https://opencode.ai/inference/go/openai/v1`). Não é preciso config nem 9router no CI.
- `OPENCODE_PERMISSION` aceita JSON de permissões (`edit`, `bash`, …).
- `fisga-pipeline.yml`: jobs `changes`, `api`, `web`, `docker`, `gate`; `gate` falha só com `failure`/`cancelled`, então job `skipped` não bloqueia.
- `fisga-deploy.yml` usa o mesmo filtro `^Projetos/fisga/` e por isso **hoje um merge só de docs deploya API e web**. `docs/ci-cd.md` registra isso como known behavior e a exclusão como item de Fase B.
- `lycheeverse/lychee-action@v2.9.0`: o input `args` substitui os globs padrão (`./**/*.md`, …) e `failIfEmpty` é `true` por default. Por isso o `args` do job precisa listar os arquivos explicitamente.

---

## Decisões derivadas do builder check

O design passou por um builder check independente. As respostas que mudaram o plano:

| Questão | Decisão |
| --- | --- |
| Formato do comentário da IA | Comentário único e fixo, marcado com `<!-- fisga-docs-ai -->`, editado a cada run. Quando não há drift, o comentário existente é **apagado**. Nunca review inline: thread inline vira pendência obrigatória e transformaria a IA em gate. |
| `docs` roda em `push` para `main`? | Não. `docs` roda só em PR e `workflow_dispatch`. Link externo que morre depois do merge não pode bloquear o deploy de código já mergeado. |
| Corpus do lint | Só documentação de desenvolvimento. Conteúdo de produto (`web/src/content`), `design-system/` e docs gerados ficam fora. `MD013` desligado com justificativa. |
| `AGENTS.md` da raiz entra no gate? | Sim. O job `changes` ganha um output `documentacao` que cobre root `AGENTS.md`, `.github/copilot-instructions.md`, `.markdownlint-cli2.jsonc` e `.lychee.toml`. |
| Seções operacionais do README | Ficam no README. O índice aponta para elas por âncora. Não há duplicação porque o índice não repete conteúdo. |
| “Testes de comportamento com fixtures” | Validação manual em pushes no próprio PR, com evidência registrada. Sem harness commitado e sem mock de LLM nesta fase. |
| Merge só de docs deploya? | **Deixa de deployar** nesta entrega: o filtro de CI e de deploy passa a excluir `docs/**`, `README.md` e `AGENTS.md` do Fisga. A exceção é só o arquivo gerado `docs/api/openapi.json`, que continua contando como código no CI. |
| Spike de OpenAPI no worktree | Foi revertido. A Task 4 implementa do zero, com lock e `.gitignore` corretos. |

---

## Task 1 — Fundação documental

**Objetivo:** porta de entrada única, contexto versionado para agentes e template de PR.

### Arquivos

| Arquivo | Ação |
| --- | --- |
| `Projetos/fisga/docs/INDEX.md` | criar |
| `Projetos/fisga/docs/adr/README.md` | criar |
| `Projetos/fisga/docs/api/README.md` | criar (contrato gerado é preenchido na Task 4) |
| `Projetos/fisga/docs/guias/como-adicionar-endpoint.md` | criar |
| `AGENTS.md` | criar (raiz) |
| `Projetos/fisga/AGENTS.md` | criar |
| `.github/copilot-instructions.md` | criar |
| `.github/PULL_REQUEST_TEMPLATE.md` | criar |
| `Projetos/fisga/README.md` | editar só a seção `## Documentação` |

### Passo 1 — Teste que falha primeiro

```bash
cd /Users/absondutragalvao/.local/share/opencode/worktree/72e3a4/fisga-documentacao-continua
for f in Projetos/fisga/docs/INDEX.md Projetos/fisga/docs/adr/README.md \
         Projetos/fisga/docs/api/README.md \
         Projetos/fisga/docs/guias/como-adicionar-endpoint.md AGENTS.md \
         Projetos/fisga/AGENTS.md .github/copilot-instructions.md \
         .github/PULL_REQUEST_TEMPLATE.md; do
  test -f "$f" && echo "ok $f" || echo "FALTA $f"
done
```

Esperado: oito linhas `FALTA`.

### Passo 2 — `docs/INDEX.md`

Conteúdo:

```markdown
# Documentação de desenvolvimento do Fisga

Porta de entrada para quem mantém o Fisga. Leia na ordem sugerida.
Conteúdo de produto (espécies, técnicas, locais) não fica aqui.

## Comece aqui

1. [Visão geral do projeto](../README.md)
2. [Rodando localmente](../README.md#rodando-localmente)
3. [Arquitetura e DDD](ddd.md)
4. [Como adicionar um endpoint](guias/como-adicionar-endpoint.md)

## Operação

- [CI/CD e deploy](ci-cd.md)
- [Review e qualidade](review.md)
- [Runbook de identidade](superpowers/runbooks/2026-11-17-identity-rollout.md)

## API

- [Contrato OpenAPI](api/README.md)

## Decisões

- [Índice de ADRs](adr/README.md)

## Planejamento

- [Roadmap](roadmap.md)
- [Auditoria de UX](ux-audit.md)

## Referências

- [Fishing World Guide](referencias/fishing-world-guide.md)
- [Tábua de Marés](referencias/tabuademares.md)

## Para agentes de IA

- [AGENTS.md](../AGENTS.md) — comandos, invariantes e onde atualizar a documentação
```

`docs/api/README.md` é criado nesta tarefa com o texto final; a Task 4 só ajusta o número de paths se o código tiver mudado.

### Passo 3 — `docs/adr/README.md`

Tabela com as 13 ADRs. Para cada arquivo `0001`–`0013`, ler o H1 e a linha de status e montar:

```markdown
# Decisões arquiteturais (ADRs)

Uma ADR por decisão relevante. Nova decisão entra como `0014-<slug>.md` e
ganha linha nesta tabela.

| ADR | Decisão | Status |
| --- | --- | --- |
| [0001](0001-captura-com-identidade.md) | Captura com identidade | aceita |
| … | … | … |
```

Usar o título real de cada ADR; não inventar.

### Passo 4 — `docs/guias/como-adicionar-endpoint.md`

Guia curto e verdadeiro em relação ao código:

1. Contrato em `src/Fisga.Api/Endpoints/<Area>Endpoints.cs`, como `Map<Area>Endpoints(this IEndpointRouteBuilder app)`, registrado em `Program.cs` (linhas ~156–161).
2. DTOs em `src/Fisga.Api/Contracts/`.
3. Regra de negócio em `src/Fisga.Application`; persistência em `src/Fisga.Infrastructure`.
4. Autenticação: `RequireAuthorization()` quando aplicável; `AllowAnonymous()` só com justificativa; rate limiting com as políticas existentes (`leitura`, etc.).
5. Cache: `CachePolicy.Public(...)` só para resposta pública e sem dado pessoal.
6. Testes em `tests/Fisga.Api.Tests`; integração quando tocar banco.
7. Regenerar o contrato: `./scripts/gerar-openapi.sh` e commitar `docs/api/openapi.json`.
8. Documentação: `docs/ddd.md` se o agregado mudou; seção `## API` do README se a rota for de produto.

### Passo 5 — `AGENTS.md` da raiz

```markdown
# AGENTS.md — monorepo abson

## O que é este repositório

Monorepo do portfólio. O site institucional e a API ficam em `apps/`; os
projetos de produto ficam em `Projetos/`. Deploy via Coolify.

## Onde está o quê

| Caminho | Projeto |
| --- | --- |
| `apps/` | abson.tech (Astro SSR + ASP.NET Core) |
| `Projetos/fisga/` | Fisga (app de pesca), em produção |
| `Projetos/precifica/` | Precifica |
| `Projetos/cardapio-no-lucro/` | Cardápio no Lucro |

## Antes de mexer

- Leia `Projetos/fisga/AGENTS.md` antes de alterar qualquer coisa do Fisga.
- Leia `Projetos/fisga/docs/INDEX.md` para a documentação de desenvolvimento.
- Nunca commite segredos (`.env`, tokens, DSNs, chaves). Configuração vive no Coolify.

## Regras gerais

- UI e copy em português do Brasil.
- Commits pequenos e convencionais.
- `Projetos/fisga/**` tem CI e CD próprios.
- Mudança de comportamento, contrato, configuração ou operação atualiza a documentação no mesmo PR.
```

### Passo 6 — `Projetos/fisga/AGENTS.md`

Deve conter, em pt-BR:

- **Visão em uma frase** e stack (ASP.NET Core 10 + PostgreSQL 17 + Astro SSR).
- **Comandos** (bloco `Comandos base`, incluindo o `export PATH`).
- **Arquitetura em 5 linhas:** `Domain` → `Application` → `Infrastructure` → `Api`; `web/` consome `PUBLIC_API_URL`.
- **Invariantes de CI/CD** copiadas de `docs/ci-cd.md` (sem reescrever o runbook): deploy é `workflow_run`, `gate` é o único check obrigatório, runtime `aspnet:10.0`, `SENTRY_AUTH_TOKEN` só por BuildKit secret, não mergear durante deploy.
- **Documentação:** fonte em `docs/`, entrada em `docs/INDEX.md`, contrato em `docs/api/openapi.json` gerado por `scripts/gerar-openapi.sh`.
- **Nunca:** commitar segredo, usar `pull_request_target`, documentar endpoint à mão, mergear com thread de review aberta.
- **Antes de abrir PR:** os quatro comandos de build/test + `npx --yes markdownlint-cli2@0.24.1`.

### Passo 7 — `.github/copilot-instructions.md`

```markdown
# Instruções para agentes de código

Leia `AGENTS.md` na raiz e `Projetos/fisga/AGENTS.md` antes de propor mudanças.
A documentação de desenvolvimento do Fisga está indexada em
`Projetos/fisga/docs/INDEX.md`.

Regras que não podem ser violadas:

- Nada de segredos em código, Markdown, workflow ou `.env` commitado.
- O check obrigatório do repositório é `gate`; não crie outro check obrigatório.
- Mudou comportamento, contrato, configuração ou operação? Atualize a documentação no mesmo PR.
- `docs/api/openapi.json` é gerado por `Projetos/fisga/scripts/gerar-openapi.sh`; não edite à mão.
```

### Passo 8 — `.github/PULL_REQUEST_TEMPLATE.md`

```markdown
## O que mudou

<!-- Resuma em 1–3 frases. -->

## Como validar

<!-- Comandos, URL, print ou passos manuais. -->

## Checklist

- [ ] Testes e build passam localmente
- [ ] Documentação atualizada quando o comportamento, contrato, configuração ou operação mudou
- [ ] Documento novo indexado em `Projetos/fisga/docs/INDEX.md`
- [ ] Nenhum segredo, token ou DSN no diff
- [ ] Links relativos novos apontam para arquivos existentes
```

### Passo 9 — `README.md`: seção `## Documentação`

Substituir o bloco atual (linhas ~639–650) por:

```markdown
## Documentação

A porta de entrada é [`docs/INDEX.md`](docs/INDEX.md). Atalhos:

- [`docs/ddd.md`](docs/ddd.md) — contextos, agregados, invariantes e limites.
- [`docs/ci-cd.md`](docs/ci-cd.md) — pipeline, deploy, Coolify, rollback e invariantes.
- [`docs/review.md`](docs/review.md) — camadas de review, agentes e critérios.
- [`docs/guias/como-adicionar-endpoint.md`](docs/guias/como-adicionar-endpoint.md) — passo a passo de uma rota nova.
- [`docs/roadmap.md`](docs/roadmap.md) — módulos, fases, dados e métricas.
- [`docs/adr/README.md`](docs/adr/README.md) — decisões arquiteturais (0001–0013).
- [`docs/referencias`](docs/referencias) — estudos de referência.
- [`AGENTS.md`](AGENTS.md) — contexto para agentes de código.
```

As seções `Rodando localmente`, `Configuração`, `Testes` e `API` **continuam no README**; o índice aponta para elas por âncora.

### Passo 10 — Verificar

```bash
for f in Projetos/fisga/docs/INDEX.md Projetos/fisga/docs/adr/README.md \
         Projetos/fisga/docs/api/README.md \
         Projetos/fisga/docs/guias/como-adicionar-endpoint.md AGENTS.md \
         Projetos/fisga/AGENTS.md .github/copilot-instructions.md \
         .github/PULL_REQUEST_TEMPLATE.md; do
  test -f "$f" && echo "ok $f" || echo "FALTA $f"
done

L=/private/var/folders/5z/8kpgpxt506b38qpmjy2fwwf00000gn/T/opencode/lychee/lychee-aarch64-apple-darwin/lychee
$L --offline --include-fragments --no-progress 'Projetos/fisga/docs/**/*.md' 'Projetos/fisga/README.md' 'AGENTS.md'
```

Esperado: oito `ok` e `Errors 0`.

**Commit:** `docs(fisga): criar índice, contexto de agentes e template de PR`

---

## Task 2 — Gate determinístico de documentação

**Objetivo:** link quebrado e Markdown inválido bloqueiam o merge pelo `gate` existente, só em PR.

### Arquivos

| Arquivo | Ação |
| --- | --- |
| `.markdownlint-cli2.jsonc` | criar na raiz |
| `.lychee.toml` | criar na raiz |
| `.github/workflows/fisga-pipeline.yml` | job `docs` + `gate.needs` |
| `.github/workflows/actions.lock` | regenerar |
| `Projetos/fisga/docs/ddd.md`, `review.md`, `ux-audit.md` | limpeza de formatação (60 erros prévios) |

### Passo 1 — Teste que falha primeiro

```bash
cd /Users/absondutragalvao/.local/share/opencode/worktree/72e3a4/fisga-documentacao-continua
cp Projetos/fisga/docs/INDEX.md /tmp/index.bak
echo '[quebrado](nao-existe.md)' >> Projetos/fisga/docs/INDEX.md
lychee --offline --include-fragments --no-progress 'Projetos/fisga/docs/**/*.md' | tail -5
cp /tmp/index.bak Projetos/fisga/docs/INDEX.md
```

Esperado: `Errors ... 1`.

### Passo 2 — `.markdownlint-cli2.jsonc`

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/DavidAnson/markdownlint-cli2/main/schema/markdownlint-cli2-config-schema.json",
  // Só documentação de desenvolvimento. Conteúdo de produto em
  // Projetos/fisga/web/src/content, o export design-system/ e os artefatos de
  // processo em docs/superpowers/ têm outro ciclo de revisão.
  "globs": [
    "AGENTS.md",
    "Projetos/fisga/README.md",
    "Projetos/fisga/AGENTS.md",
    "Projetos/fisga/docs/**/*.md"
  ],
  "ignores": [
    "Projetos/fisga/docs/superpowers/**"
  ],
  "config": {
    // Linha longa é estilo, não erro.
    "MD013": false
  }
}
```

Verificar: `npx --yes markdownlint-cli2@0.24.1` → impossível até o corpus ser limpo.

#### Limpeza única do corpus existente

O corpus tem 60 erros prévios em `docs/ddd.md`, `docs/review.md` e `docs/ux-audit.md`. Limpar antes de ligar o gate:

```bash
npx --yes markdownlint-cli2@0.24.1 --fix
npx --yes markdownlint-cli2@0.24.1   # esperado: 10 erros (7 MD036, 3 MD040)
```

`--fix` resolve tabelas (`MD060`) e numeração de listas (`MD029`). Os 10 restantes exigem edição manual em `docs/ddd.md` e `docs/ux-audit.md`:

- `MD036`: trocar o parágrafo em ênfase por um título `###` ou `####` do nível correto.
- `MD040`: informar a linguagem do bloco (por exemplo `text` ou `bash`).

Revisar o diff do `--fix` para garantir que tabelas e listas continuam com o mesmo conteúdo. Não alterar texto, só formatação.

Verificar depois: `npx --yes markdownlint-cli2@0.24.1` → `0 error`.

### Passo 3 — `.lychee.toml`

```toml
# Links da documentação de desenvolvimento do Fisga.
max_retries = 3
retry_wait_time = 5
include_fragments = "full"
exclude_loopback = true
exclude_private = true

exclude = [
  # Domínios reservados para exemplo em documentação.
  "^https?://[^/]*\\.example",
  # Painel do Coolify no VPS: não responde de fora.
  "^https?://manager\\.abson\\.tech",
]
```

**Atenção:** no formato TOML do lychee 0.24.2, `include_fragments` é string
(`"full"`), não booleano — o booleano só vale para a flag de CLI. Verificado
em 2026-11-18: config com `"full"` dá `84 OK / 0 Errors`.

Verificar: `lychee --config .lychee.toml --no-progress <globs>` → `Errors 0`.

### Passo 4 — Job `docs`

Inserir depois do job `web`:

```yaml
  docs:
    name: docs
    needs: changes
    # Só em PR: link externo que morre depois do merge não pode bloquear o
    # deploy de código já mergeado.
    if: |
      github.event_name != 'push' &&
      (needs.changes.outputs.documentacao == 'true' || needs.changes.outputs.fisga == 'true')
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v7.0.1

      - name: Markdown
        run: npx --yes markdownlint-cli2@0.24.1

      - name: Links
        uses: lycheeverse/lychee-action@v2.9.0
        with:
          # Sobrescrever `args` descarta os globs padrão da action: inclua-os.
          args: >-
            --config .lychee.toml --no-progress --verbose
            'AGENTS.md' 'Projetos/fisga/README.md' 'Projetos/fisga/AGENTS.md'
            'Projetos/fisga/docs/**/*.md'
          fail: true
```

O output `documentacao` é criado na Task 3. Para a Task 2 funcionar sozinha, use provisoriamente só `needs.changes.outputs.fisga == 'true'` e ajuste na Task 3.

E incluir `docs` em `gate.needs`:

```yaml
    needs:
      - changes
      - api
      - web
      - docker
      - docs
```

### Passo 5 — Pin

```bash
gh actions-lock
git diff .github/workflows/actions.lock
```

Esperado: `lycheeverse/lychee-action@v2.9.0` registrado.

### Passo 6 — Verificar

```bash
npx --yes markdownlint-cli2@0.24.1
lychee --config .lychee.toml --no-progress \
  'AGENTS.md' 'Projetos/fisga/README.md' 'Projetos/fisga/AGENTS.md' \
  'Projetos/fisga/docs/**/*.md'
python3 - <<'PY'
import yaml
w = yaml.safe_load(open('.github/workflows/fisga-pipeline.yml'))
assert 'docs' in w['jobs']
assert 'docs' in w['jobs']['gate']['needs']
assert w['jobs']['gate']['name'] == 'gate'
print('ok')
PY
```

**Commit:** `ci(fisga): validar markdown e links no gate`

---

## Task 3 — CI e deploy ignoram mudança só de documentação

**Objetivo:** PR só de docs não builda API/web nem deploya; mas é validado pelo job `docs`.

### Arquivos

| Arquivo | Ação |
| --- | --- |
| `.github/workflows/fisga-pipeline.yml` | outputs `fisga` e `documentacao` no job `changes`; `api`/`web`/`docker` inalterados; `docs` passa a usar `documentacao` |
| `.github/workflows/fisga-deploy.yml` | filtro ignora docs |
| `Projetos/fisga/docs/ci-cd.md` | atualizar known behavior e invariantes |
| `Projetos/fisga/AGENTS.md` | refletir o filtro novo |

### Passo 1 — Teste que falha primeiro

O comportamento atual é o bug: um diff só de docs produz `fisga=true`. Provar com um script local:

```bash
cd /Users/absondutragalvao/.local/share/opencode/worktree/72e3a4/fisga-documentacao-continua
cat > /tmp/classificar.sh <<'SH'
#!/usr/bin/env bash
set -euo pipefail
FILES="$1"
CODIGO="$(printf '%s\n' "$FILES" | grep -E '^Projetos/fisga/' \
  | grep -vE '^Projetos/fisga/(docs/|README\.md$|AGENTS\.md$)' || true)"
GERADO="$(printf '%s\n' "$FILES" | grep -E '^Projetos/fisga/docs/api/openapi\.json$' || true)"
WORKFLOWS="$(printf '%s\n' "$FILES" | grep -E '^\.github/workflows/fisga-pipeline\.yml$' || true)"
DOCUMENTACAO="$(printf '%s\n' "$FILES" \
  | grep -E '^Projetos/fisga/(docs/|README\.md$|AGENTS\.md$)|^AGENTS\.md$|^\.github/(copilot-instructions\.md|PULL_REQUEST_TEMPLATE\.md)$|^\.markdownlint-cli2\.jsonc$|^\.lychee\.toml$' \
  | grep -vE '^Projetos/fisga/docs/api/openapi\.json$' || true)"
printf 'codigo=%s documentacao=%s\n' \
  "$([ -n "$CODIGO" ] || [ -n "$GERADO" ] || [ -n "$WORKFLOWS" ] && echo true || echo false)" \
  "$([ -n "$DOCUMENTACAO" ] && echo true || echo false)"
SH
chmod +x /tmp/classificar.sh
/tmp/classificar.sh 'Projetos/fisga/docs/INDEX.md'
/tmp/classificar.sh 'AGENTS.md'
/tmp/classificar.sh 'Projetos/fisga/docs/api/openapi.json'
/tmp/classificar.sh 'Projetos/fisga/src/Fisga.Api/Program.cs'
```

Esperado depois da tarefa: `codigo=false documentacao=true`, `codigo=false documentacao=true`, `codigo=true documentacao=false`, `codigo=true documentacao=false`.

> Observação: o script acima é a especificação do filtro. Ele será embutido no `run:` do job `changes`, não commitado como arquivo.

### Passo 2 — `fisga-pipeline.yml`, job `changes`

Substituir o bloco final do `run` por:

```bash
          FILES="$(git diff --name-only "$BASE" "$HEAD")"

          # Código = qualquer arquivo do Fisga que não seja documentação.
          CODIGO="$(printf '%s\n' "$FILES" \
            | grep -E '^Projetos/fisga/' \
            | grep -vE '^Projetos/fisga/(docs/|README\.md$|AGENTS\.md$)' || true)"
          # O contrato gerado conta como código: mudá-lo à mão precisa passar
          # pelo build do job api, que regenera e compara.
          GERADO="$(printf '%s\n' "$FILES" \
            | grep -E '^Projetos/fisga/docs/api/openapi\.json$' || true)"
          WORKFLOWS="$(printf '%s\n' "$FILES" \
            | grep -E '^\.github/workflows/fisga-pipeline\.yml$' || true)"
          DOCUMENTACAO="$(printf '%s\n' "$FILES" \
            | grep -E '^Projetos/fisga/(docs/|README\.md$|AGENTS\.md$)|^AGENTS\.md$|^\.github/(copilot-instructions\.md|PULL_REQUEST_TEMPLATE\.md)$|^\.markdownlint-cli2\.jsonc$|^\.lychee\.toml$' \
            | grep -vE '^Projetos/fisga/docs/api/openapi\.json$' || true)"

          if [ -n "$CODIGO" ] || [ -n "$GERADO" ] || [ -n "$WORKFLOWS" ]; then
            echo "fisga=true" >> "$GITHUB_OUTPUT"
          else
            echo "fisga=false" >> "$GITHUB_OUTPUT"
          fi

          if [ -n "$DOCUMENTACAO" ]; then
            echo "documentacao=true" >> "$GITHUB_OUTPUT"
          else
            echo "documentacao=false" >> "$GITHUB_OUTPUT"
          fi
```

E declarar os outputs:

```yaml
    outputs:
      fisga: ${{ steps.diff.outputs.fisga }}
      documentacao: ${{ steps.diff.outputs.documentacao }}
```

**Atenção ao `workflow_dispatch`:** o job `changes` tem um `exit 0` antecipado que hoje grava só `fisga=true`. Ele precisa gravar `documentacao=true` também, senão o job `docs` não roda no dispatch.

```bash
          if [ "${{ github.event_name }}" = "workflow_dispatch" ]; then
            echo "fisga=true" >> "$GITHUB_OUTPUT"
            echo "documentacao=true" >> "$GITHUB_OUTPUT"
            exit 0
          fi
```

### Passo 3 — `fisga-pipeline.yml`, job `docs`

Ajustar a condição para usar o output novo:

```yaml
    if: |
      github.event_name != 'push' &&
      (needs.changes.outputs.documentacao == 'true' || needs.changes.outputs.fisga == 'true')
```

### Passo 4 — `fisga-deploy.yml`, job `changes`

```bash
          FILES="$(git diff --name-only "$BASE" "$HEAD")"
          CODIGO="$(printf '%s\n' "$FILES" \
            | grep -E '^Projetos/fisga/' \
            | grep -vE '^Projetos/fisga/(docs/|README\.md$|AGENTS\.md$)' || true)"
          WORKFLOWS="$(printf '%s\n' "$FILES" \
            | grep -E '^\.github/workflows/fisga-(pipeline|deploy)\.yml$' || true)"
          if [ -n "$CODIGO" ] || [ -n "$WORKFLOWS" ]; then
            echo "fisga=true" >> "$GITHUB_OUTPUT"
          else
            echo "fisga=false" >> "$GITHUB_OUTPUT"
          fi
```

Não mexer nos `if` de `deploy-api`/`deploy-web` nem no `workflow_run`.

> Efeito colateral esperado: o merge **deste PR** contém mudança em `fisga-deploy.yml`, então `WORKFLOWS` é não vazio e o deploy roda normalmente — o caminho de deploy é exercitado.

### Passo 5 — `docs/ci-cd.md`

Substituir o bloco “Known behavior” (linhas ~126–131) por:

```markdown
### Documentação não deploya

O filtro de código exclui `Projetos/fisga/docs/**`, `README.md` e `AGENTS.md`.
Um merge só de documentação roda o job `docs` no PR e **não** dispara deploy.
Mudou código, configuração ou container? Deploy normal. O job de deploy continua
sendo o mesmo `workflow_run`, sem `paths:`.
```

E remover da lista de Fase B (linha ~515) o item “Excluir `docs/**` e markdown do filtro de deploy”, já que foi feito.

### Passo 6 — `Projetos/fisga/AGENTS.md`

Atualizar a invariante de deploy para refletir o filtro novo (docs não deploya).

### Passo 7 — Verificar

```bash
python3 - <<'PY'
import yaml
p = yaml.safe_load(open('.github/workflows/fisga-pipeline.yml'))
d = yaml.safe_load(open('.github/workflows/fisga-deploy.yml'))
assert 'documentacao' in p['jobs']['changes']['outputs']
assert p['jobs']['docs']['if'].find("'push'") != -1
assert p['jobs']['gate']['needs'] == ['changes','api','web','docker','docs']
assert d['jobs']['changes']['outputs']['fisga']
print('ok')
PY
gh actions-lock && git diff --exit-code .github/workflows/actions.lock
```

**Commit:** `ci(fisga): separar mudança de documentação de mudança de código`

---

## Task 4 — OpenAPI gerado do código

**Objetivo:** o contrato sai do código, é commitado e o CI recusa PR com contrato desatualizado.

### Arquivos

| Arquivo | Ação |
| --- | --- |
| `Projetos/fisga/src/Fisga.Api/Fisga.Api.csproj` | pacote + PropertyGroup condicional |
| `Projetos/fisga/src/Fisga.Api/packages.lock.json` | regenerar |
| `Projetos/fisga/scripts/gerar-openapi.sh` | criar |
| `Projetos/fisga/docs/api/openapi.json` | gerar e commitar |
| `Projetos/fisga/docs/api/README.md` | criar (ou completar o criado na Task 1) |
| `.gitignore` | ignorar `artifacts/` |
| `.github/workflows/fisga-pipeline.yml` | steps no job `api` |

### Passo 1 — Teste que falha primeiro

```bash
cd /Users/absondutragalvao/.local/share/opencode/worktree/72e3a4/fisga-documentacao-continua
test -x Projetos/fisga/scripts/gerar-openapi.sh && echo ok || echo "FALTA script"
test -s Projetos/fisga/docs/api/openapi.json && echo ok || echo "FALTA openapi.json"
```

Esperado: dois `FALTA`.

### Passo 2 — `Fisga.Api.csproj`

No `ItemGroup` de pacotes:

```xml
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.15">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
```

E:

```xml
  <!-- Geração opt-in: o build normal não abre a aplicação nem exige configuração. -->
  <PropertyGroup Condition="'$(GenerateOpenApiDocs)' == 'true'">
    <OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/artifacts/openapi</OpenApiDocumentsDirectory>
  </PropertyGroup>
```

Depois `dotnet restore Fisga.slnx` (sem `--locked-mode`) para gravar o lock novo.

### Passo 3 — `scripts/gerar-openapi.sh`

```bash
#!/usr/bin/env bash
# Gera docs/api/openapi.json a partir do código.
#
# A geração sobe a aplicação em memória; por isso usa Development com SQLite
# temporário e Sentry desligado. Nunca use este script com dados reais.
set -euo pipefail

raiz=$(cd "$(dirname "$0")/.." && pwd)
origem="$raiz/src/Fisga.Api/artifacts/openapi/Fisga.Api.json"
destino="$raiz/docs/api/openapi.json"
temporario=$(mktemp -d)
trap 'rm -rf "$temporario"' EXIT

export ASPNETCORE_ENVIRONMENT=Development
export Database__ConnectionString="Data Source=$temporario/fisga-openapi.db"
export Sentry__Dsn=""

dotnet build "$raiz/src/Fisga.Api/Fisga.Api.csproj" \
  -c Release \
  -p:GenerateOpenApiDocs=true

mkdir -p "$(dirname "$destino")"
cp "$origem" "$destino"
printf 'OpenAPI gerado em %s\n' "$destino"
```

`chmod +x`.

### Passo 4 — `.gitignore`

Adicionar `artifacts/` ao bloco de builds.

### Passo 5 — Gerar e conferir determinismo

```bash
cd Projetos/fisga
./scripts/gerar-openapi.sh
sha1=$(shasum -a 256 docs/api/openapi.json | cut -d' ' -f1)
./scripts/gerar-openapi.sh
sha2=$(shasum -a 256 docs/api/openapi.json | cut -d' ' -f1)
test "$sha1" = "$sha2" && echo "deterministico"
python3 -c "
import json
d=json.load(open('docs/api/openapi.json'))
print(d['openapi'], len(d['paths']), 'paths', len(d['components']['schemas']), 'schemas')
"
```

Esperado: `deterministico` e `3.1.1 48 paths 25 schemas`. Se o número de paths diferir, o código mudou desde este plano — aceite o real e registre no PR.

### Passo 6 — `docs/api/README.md`

Explicar em ~10 linhas: arquivo gerado, não editar à mão; como regenerar; `/openapi/v1.json` só existe em Development; autenticação por cookie ainda não aparece como `securityScheme` (limitação conhecida).

### Passo 7 — CI no job `api`

Depois do step `Test`:

```yaml
      - name: Gerar OpenAPI
        run: ./scripts/gerar-openapi.sh

      - name: Contrato documentado está atualizado
        run: |
          set -euo pipefail
          if ! git diff --exit-code -- docs/api/openapi.json; then
            echo "::error::docs/api/openapi.json está desatualizado. Rode ./scripts/gerar-openapi.sh e commite."
            exit 1
          fi

      - name: Publicar OpenAPI
        uses: actions/upload-artifact@v7.0.2
        with:
          name: fisga-openapi
          path: Projetos/fisga/docs/api/openapi.json
          retention-days: 30
```

### Passo 8 — Verificar

```bash
cd Projetos/fisga
./scripts/gerar-openapi.sh
git status --porcelain docs/api/openapi.json   # vazio
dotnet build Fisga.slnx -c Release --no-restore -warnaserror
dotnet test Fisga.slnx -c Release --no-build
```

**Commit:** `feat(fisga): gerar openapi a partir do código e validar no ci`

---

## Task 5 — Revisão de documentação por IA (automática e sob demanda)

**Objetivo:** todo PR do Fisga recebe análise de drift; `/docs` dispara análise profunda; nada bloqueia merge.

### Arquivo

`.github/workflows/fisga-docs-ai.yml` (novo). Não alterar `oc.yml`.

### Por que não a action `opencode/github`

Verificado no código da action (`anomalyco/opencode`, tag `latest`): aceita apenas `issue_comment` e `pull_request_review_comment`, exige `/oc` e empurra commit quando o workspace fica sujo. Para rodar automaticamente em `pull_request` e garantir somente leitura, o workflow usa o **CLI** `opencode run`.

### Workflow

```yaml
name: fisga-docs-ai

on:
  pull_request:
    types: [opened, synchronize, reopened, ready_for_review]
    paths:
      - "Projetos/fisga/**"
      - "AGENTS.md"
      - ".github/copilot-instructions.md"
  issue_comment:
    types: [created]
  workflow_dispatch:
    inputs:
      pr:
        description: "Número do PR"
        required: true
        type: string

permissions:
  contents: read
  issues: write
  pull-requests: write

concurrency:
  group: fisga-docs-ai-${{ github.event_name }}-${{ github.event.pull_request.number || github.event.issue.number || inputs.pr }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

jobs:
  docs:
    name: docs-ai
    if: |
      (github.event_name == 'pull_request' &&
       github.event.pull_request.draft == false &&
       github.event.pull_request.head.repo.full_name == github.repository &&
       github.event.pull_request.user.type != 'Bot') ||
      (github.event_name == 'issue_comment' &&
       github.event.issue.pull_request &&
       contains(fromJSON('["OWNER","MEMBER","COLLABORATOR"]'), github.event.comment.author_association) &&
       startsWith(github.event.comment.body, '/docs')) ||
      github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-latest
    timeout-minutes: 20
    continue-on-error: true
    steps:
      - name: Resolver PR
        id: pr
        shell: bash
        env:
          NUMERO: ${{ github.event.pull_request.number || github.event.issue.number || inputs.pr }}
        run: |
          set -euo pipefail
          if [ -z "${NUMERO:-}" ]; then
            echo "::error::PR não identificado."
            exit 1
          fi
          echo "numero=$NUMERO" >> "$GITHUB_OUTPUT"

      - name: Checkout do PR
        uses: actions/checkout@v7.0.1
        with:
          ref: refs/pull/${{ steps.pr.outputs.numero }}/head
          fetch-depth: 0
          persist-credentials: false

      - name: Preparar diff
        shell: bash
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          NUMERO: ${{ steps.pr.outputs.numero }}
        run: |
          set -euo pipefail
          base=$(gh pr view "$NUMERO" --repo "$GITHUB_REPOSITORY" --json baseRefName --jq .baseRefName)
          git fetch --no-tags origin "$base"
          merge_base=$(git merge-base "origin/$base" HEAD)
          {
            echo "Diff resumido:"
            git diff --stat "$merge_base" HEAD
            echo
            echo "Patch (limitado a 200 KB):"
            git diff -U3 "$merge_base" HEAD | head -c 200000
          } > /tmp/pr-diff.txt
          wc -c /tmp/pr-diff.txt

      - name: Preparar prompt
        run: |
          cat > /tmp/prompt.txt <<'PROMPT'
          Você é o revisor de documentação de desenvolvimento do Fisga.

          Antes de analisar, leia:
          - AGENTS.md
          - Projetos/fisga/AGENTS.md
          - Projetos/fisga/docs/INDEX.md

          O diff deste PR está anexado. Analise apenas o que mudou e diga qual
          documentação ficou desatualizada por causa da mudança.

          Verifique quando o diff tocar nesses pontos:
          - rotas, contratos e DTOs: docs/api/openapi.json e docs/ddd.md
          - regras de negócio, agregados e invariantes: docs/ddd.md e docs/adr/
          - CI/CD, deploy, segredos e invariantes: docs/ci-cd.md e Projetos/fisga/AGENTS.md
          - configuração, variáveis de ambiente e migrações: README (Configuração) e runbooks
          - testes e camadas de review: docs/review.md

          Regras:
          - Use o código como fonte. Não invente endpoints, variáveis nem comportamento.
          - Não proponha mudança para o que o diff não alterou.
          - Não comente estilo que o markdownlint já cobre.
          - Cada achado: caminho do documento, motivo e trecho sugerido de 2 a 6 linhas.
          - Se nada precisar mudar, responda exatamente: SEM_DRIFT
          - Não altere arquivos. Sua resposta vira um comentário no PR, em português do Brasil.
          PROMPT

      - name: Instalar OpenCode
        run: curl -fsSL https://opencode.ai/install | bash

      - name: Analisar documentação
        env:
          OPENCODE_API_KEY: ${{ secrets.OPENCODE_API_KEY }}
          OPENCODE_PERMISSION: '{"edit":"deny","write":"deny","patch":"deny","bash":"deny"}'
        run: |
          set -euo pipefail
          export PATH="$HOME/.opencode/bin:$PATH"
          if ! opencode run \
              --standalone \
              --format json \
              --model 'opencode-go/deepseek-v4.1-flash#max' \
              --archived \
              --file /tmp/pr-diff.txt \
              "$(cat /tmp/prompt.txt)" > /tmp/resposta.jsonl 2> /tmp/erro.log; then
            echo "::warning::A análise de documentação falhou; o PR não é bloqueado."
            tail -30 /tmp/erro.log || true
            exit 0
          fi
          # --format json emite JSONL; só os eventos "text" trazem a resposta.
          # Verificado em 2026-11-18 com a CLI instalada.
          jq -r 'select(.type == "text") | .part.text' /tmp/resposta.jsonl \
            > /tmp/resposta.md

      - name: Publicar comentário
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          REPO: ${{ github.repository }}
          NUMERO: ${{ steps.pr.outputs.numero }}
        run: |
          set -euo pipefail
          if [ ! -s /tmp/resposta.md ]; then
            echo "Sem resposta; nada a publicar."
            exit 0
          fi
          id=$(gh api "repos/$REPO/issues/$NUMERO/comments" --paginate \
            --jq '.[] | select(.body | startswith("<!-- fisga-docs-ai -->")) | .id' | head -1)
          if grep -q '^SEM_DRIFT$' /tmp/resposta.md; then
            if [ -n "$id" ]; then
              gh api -X DELETE "repos/$REPO/issues/comments/$id" > /dev/null
              echo "Sem drift; comentário anterior removido."
            else
              echo "Sem drift."
            fi
            exit 0
          fi
          {
            echo "<!-- fisga-docs-ai -->"
            echo "## Revisão de documentação (IA)"
            echo
            cat /tmp/resposta.md
            echo
            echo "_Análise automática. O gate determinístico é que decide o merge._"
          } > /tmp/comentario.md
          if [ -n "$id" ]; then
            gh api -X PATCH "repos/$REPO/issues/comments/$id" -F body=@/tmp/comentario.md > /dev/null
          else
            gh api -X POST "repos/$REPO/issues/$NUMERO/comments" -F body=@/tmp/comentario.md > /dev/null
          fi
```

### Verificações obrigatórias antes do commit

```bash
python3 -c "import yaml;yaml.safe_load(open('.github/workflows/fisga-docs-ai.yml'));print('yaml ok')"
actionlint .github/workflows/fisga-docs-ai.yml   # baixar binário se ausente
grep -nE 'sk-|ghp_|github_pat_' .github/workflows/fisga-docs-ai.yml && exit 1 || echo "sem segredo"
gh actions-lock
```

### Restrições de comportamento

- Não entra em `gate.needs`.
- `contents: read` + `persist-credentials: false`: não há credencial para push.
- `OPENCODE_PERMISSION` nega `edit`, `write`, `patch`, `bash`.
- Fork, draft e bot são ignorados pelo `if`.
- `/docs` só funciona depois do merge (`issue_comment` usa a branch padrão).

**Commit:** `ci(fisga): revisar documentação por ia em todo pr`

---

## Task 6 — Verificação final, PR e validação comportamental

### Passo 0 — Registrar design e plano

O spec e o plano desta entrega estão em `docs/superpowers/` e ainda não estão
rastreados. Eles são artefatos de processo, fora do corpus do markdownlint mas
dentro dos globs do lychee. Commitá-los antes do PR:

```bash
git add Projetos/fisga/docs/superpowers/specs/2026-11-18-documentacao-continua-design.md \
        Projetos/fisga/docs/superpowers/plans/2026-11-18-documentacao-continua.md
git commit -m "docs(superpowers): registrar design e plano da documentação contínua"
```

O ledger em `.superpowers/sdd/` continua fora do repositório.

### Passo 0b — Corrigir contagem de testes

A suíte real tem **326 testes** (36 domínio + 54 aplicação + 236 API), mas os
docs vivos ainda dizem 250 (36 + 48 + 166). O número é pré-existente, não foi
introduzido por esta entrega, mas deixá-lo errado enfraquece a proposta de
documentação contínua. Atualizar os documentos vivos — `README.md` (linhas ~434
e ~473), `docs/ci-cd.md` (~16, ~92), `docs/review.md` (~36, ~198),
`docs/roadmap.md` (~134) e `docs/ux-audit.md` (~323, ~386). Specs e planos
arquivados em `docs/superpowers/` são registros históricos e ficam intactos.

```bash
git add Projetos/fisga/README.md Projetos/fisga/docs/ci-cd.md \
        Projetos/fisga/docs/review.md Projetos/fisga/docs/roadmap.md \
        Projetos/fisga/docs/ux-audit.md
git commit -m "docs(fisga): atualizar contagem de testes nos documentos vivos"
```

### Passo 1 — Suíte completa

```bash
export PATH="$HOME/.dotnet:$PATH"
cd Projetos/fisga
dotnet restore Fisga.slnx --locked-mode
dotnet format Fisga.slnx --verify-no-changes --no-restore
dotnet build Fisga.slnx -c Release --no-restore -warnaserror
dotnet test Fisga.slnx -c Release --no-build
cd web && npm ci && npm run check && npm run build && cd ../../..
npx --yes markdownlint-cli2@0.24.1
lychee --config .lychee.toml --no-progress \
  'AGENTS.md' 'Projetos/fisga/README.md' 'Projetos/fisga/AGENTS.md' \
  'Projetos/fisga/docs/**/*.md'
cd Projetos/fisga && ./scripts/gerar-openapi.sh
git status --porcelain docs/api/openapi.json   # vazio
```

### Passo 2 — Push e PR

```bash
git push -u origin feat/fisga-documentacao-continua
gh pr create --repo AbsonDev/abson --base main --head feat/fisga-documentacao-continua \
  --title "docs(fisga): documentação contínua com gate determinístico e revisão por IA" \
  --body-file /tmp/pr-body.md
```

Corpo do PR (criar `/tmp/pr-body.md`):

```markdown
## O que mudou

- `docs/INDEX.md` como porta de entrada; índice de ADRs; guia de endpoint.
- `AGENTS.md` (raiz e Fisga), `copilot-instructions.md` e template de PR.
- Job `docs` no `fisga-pipeline` (markdownlint + lychee) dentro do `gate`, só em PR.
- Filtros de CI e deploy separam documentação de código: PR só de docs não builda nem deploya.
- OpenAPI gerado do código e conferido no CI.
- Workflow `fisga-docs-ai`: automático em PR, `/docs` sob demanda e dispatch.

## Como validar

- `npx --yes markdownlint-cli2@0.24.1`
- `lychee --config .lychee.toml --no-progress 'AGENTS.md' 'Projetos/fisga/README.md' 'Projetos/fisga/AGENTS.md' 'Projetos/fisga/docs/**/*.md'`
- `cd Projetos/fisga && ./scripts/gerar-openapi.sh && git status --porcelain docs/api/openapi.json`
- Conferir o comentário do `fisga-docs-ai` neste PR.

## Escopo

O site Starlight (Fase 2 do design) fica para um plano próprio depois deste merge.
```

### Passo 3 — Checks e review

```bash
gh pr checks <N> --repo AbsonDev/abson --watch --interval 30
```

Depois dos checks, reler **todos** os comentários (PR-Agent, CodeQL, reviewdog, `/oc`, `fisga-docs-ai`) e resolver cada thread via GraphQL, confirmando `isResolved: true`.

### Passo 4 — Validação comportamental do job de IA

| Cenário | Como provocar | Esperado |
| --- | --- | --- |
| Drift real | alterar `src/Fisga.Api/Endpoints/CondicoesEndpoints.cs` sem tocar docs | comentário fixo apontando `docs/api/openapi.json` ou `docs/ddd.md` |
| Sem drift | alterar `web/src/styles/*.css` | `SEM_DRIFT`; comentário anterior apagado |
| Dois pushes | dois commits seguidos | run anterior `cancelled`; só o último comenta |
| Comentário único | push após comentário existente | mesmo comentário editado, sem duplicar |
| Bot | PR do Dependabot | job `skipped` |
| Falha de IA | `workflow_dispatch` com número inválido de PR | job neutro; `gate` verde |

Registrar os resultados no PR.

### Passo 5 — Pós-merge

Validar `/docs` em um PR real (o `issue_comment` usa a branch padrão) e registrar a evidência em `docs/review.md`.

---

## Fora de escopo (Fase 2 — plano próprio)

- Site Astro Starlight, Pagefind e GitHub Pages.
- `llms.txt` e cópias `.md` publicadas.
- Domínio `docs.fisga.abson.tech`.
- Security schemes no OpenAPI (cookie `fisga_auth`).
- Docs evals e MCP server de documentação.
