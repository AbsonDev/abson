# Review do Fisga

Este documento é a fonte da verdade do fluxo de revisão de PRs do Fisga. Ele diz o que roda, o que
bloqueia o merge e o que é apenas advisory. As camadas de CI e CD estão em [ci-cd.md](./ci-cd.md);
aqui está só o julgamento do diff.

**Status:** as camadas abaixo rodam em PR. Nenhuma delas, além do `gate`, é required check — a
promoção depende do ciclo de métricas descrito abaixo.

## Princípio

A revisão é uma pirâmide, não uma pilha de ferramentas. Cada camada responde a uma pergunta
diferente, e a regra é uma só: **se uma máquina consegue decidir com certeza, não gaste token.**

```text
1. determinístico   build · format · testes · astro check · docs · contrato (gate)
2. segurança        CodeQL · dependency-review                 (implementado)
3. qualidade inline reviewdog                                  (implementado)
4. julgamento       PR-Agent · revisão de documentação         (implementado)
5. ação             /oc                                        (implementado)
```

- **Determinístico** decide binário: passou ou não passou. Pode bloquear.
- **Segurança** também é binário quando a regra dispara. Pode bloquear com escopo estreito.
- **Qualidade inline** só mostra o que o linter já achou. Não bloqueia.
- **Julgamento** é probabilístico. Nunca bloqueia. A revisão de documentação compara mudança e
  documentos, e comenta o drift; o `gate` determinístico decide o merge.
- **Ação** altera código. Só roda sob demanda e com permissão explícita.

O objetivo não é substituir revisão humana. Nenhum benchmark de revisor automático encontra metade
dos bugs conhecidos; a camada serve para o humano gastar atenção no que só humano vê.

## Estado atual

| Camada | Ferramenta | Status | Bloqueia? | Custo |
| --- | --- | --- | --- | --- |
| CI | `gate` (build, format, 408 testes, `astro check`, Playwright, `docs`, contrato) | implementado | sim, único required check | R$ 0 |
| Dependências | Dependabot | implementado | não (propõe PRs) | R$ 0 |
| Supply chain | `dependency-review-action@v5` | implementado, advisory | sim, quando promovido | R$ 0 |
| Segurança estática | CodeQL (`csharp`, `javascript-typescript`, `actions`) | implementado, advisory | sim, via regra `code_scanning` | R$ 0 (repo público) |
| Qualidade inline | reviewdog | implementado, advisory | não | R$ 0 |
| Revisor LLM | PR-Agent CLI + OpenCode Go | implementado, advisory | não | franquia Go |
| Revisão de docs | `fisga-docs-ai` (OpenCode CLI + OpenCode Go) | implementado, advisory | não | franquia Go |
| Agente | OpenCode `/oc` | implementado, validado | não (sob demanda) | franquia Go |

O `gate` valida que o código compila, formata, passa nos testes e mantém o contrato gerado e os
documentos íntegros; ele não lê a intenção da mudança. O revisor LLM cobre isso. Nenhuma camada nova
é required check até passar pelo ciclo de métricas.

## Fluxo de um PR

```text
PR aberto ou atualizado
├── fisga-pipeline / gate ........ bloqueia (build, testes, docs, contrato)
├── dependency-review ............ bloqueia CVE high+ (implementado, advisory até promover)
├── codeql (3 linguagens) ........ bloqueia alerta high+ via ruleset
├── reviewdog .................... advisory: linter inline
├── pr-review (PR-Agent) ......... advisory: julgamento do diff
├── fisga-docs-ai ................ advisory: drift de documentação (comentário único)
        │
        ├── humano revisa e decide
        └── /oc sob demanda ...... explica, corrige, implementa (implementado)

merge em main
└── fisga-deploy ................. só com CI verde e mudança de código; ver ci-cd.md
```

Caminhos especiais:

- **PR só de docs:** o `gate` roda (sem `paths:` no gatilho); o job `docs` roda markdownlint, a
  guarda de rotas e a checagem de links, e o CodeQL roda. Se o CodeQL tivesse filtro de caminho no
  gatilho, o merge travaria por "análise não executada" quando a regra `code_scanning` estivesse
  ativa. Ver [CodeQL](#codeql). O `openapi.json` é a única exceção: conta como código e dispara o
  job `api`.
- **PR de fork:** `pull_request` não recebe secrets e o `GITHUB_TOKEN` é read-only. As camadas que
  dependem de chave (PR-Agent, `fisga-docs-ai`, `/oc`) são puladas; CodeQL e `gate` rodam.
- **PR de bot (Dependabot, Sentry):** as camadas automáticas ignoram bot pelo tipo do autor
  (`github.event.pull_request.user.type != 'Bot'` no `fisga-docs-ai`) ou do sender
  (`github.event.sender.type != 'Bot'` no PR-Agent), para não gastar franquia e não duplicar
  comentário.

## O que bloqueia e o que é advisory

| Camada | Bloqueia? | Por quê |
| --- | --- | --- |
| `gate` | **sim** | determinístico, reproduzível, é o contrato de merge |
| dependency-review | **sim**, quando promovido | binário: CVE conhecida ou licença inválida não entram |
| CodeQL (`high+`) | **sim**, quando a regra for ativada | binário quando a regra dispara; escopo estreito evita ruído |
| reviewdog | não | só reporta linter no diff; vermelho não significa bug |
| PR-Agent | não | probabilístico, muda com o modelo, custa token |
| `fisga-docs-ai` | não | aponta drift; `continue-on-error` e fora de `gate.needs` |
| `/oc` | não | agente; pode alterar código e errar |

Regra permanente: **só camada determinística pode ser required check.** Promover LLM a obrigatório
transforma um falso positivo em bloqueio de merge sem dono, e a única saída vira `dismiss` — que
ensina o time a ignorar a ferramenta.

## Camadas

O processo completo (draft, issue, branch, PR, merge e deploy) e o checklist obrigatório de merge
estão em [github-workflow.md](./github-workflow.md); esta seção cobre só o julgamento do diff.

### `gate`

- **Quando roda:** todo PR e todo push em `main`.
- **Onde:** `.github/workflows/fisga-pipeline.yml`.
- **O que valida:** restore locked, `dotnet format --verify-no-changes`, build `-warnaserror`, 408
  testes, `npm ci`, `astro check`, build, 165 Playwright mockados, build das imagens, job `docs`
  (markdownlint + guarda de rotas + links, só em PR e `workflow_dispatch`, não no push),
  regeneração/comparação de `docs/api/openapi.json` e lint de OpenAPI no job `api`.
- **Como ler:** o job `gate` agrega; um job `failure`/`cancelled` derruba o gate.
- **Reexecutar:** push novo no PR, ou `gh run rerun <run-id> --repo AbsonDev/abson`.

### dependency-review

- **Quando roda:** `pull_request`.
- **Onde:** `.github/workflows/dependency-review.yml` (implementado).
- **Pré-requisito:** o dependency graph do repositório precisa estar habilitado. Sem ele, a API de
  dependency review responde `403 Forbidden` e o job falha em segundos. O repo estava com o grafo
  desabilitado; habilitar em Settings → Code security, ou
  `gh api -X PUT repos/AbsonDev/abson/vulnerability-alerts`.
- **Configuração canônica:**

```yaml
- uses: actions/dependency-review-action@v5
  with:
    fail-on-severity: high
    fail-on-scopes: runtime
    license-check: false
    comment-summary-in-pr: on-failure
```

- **Por que `runtime`:** devDependency vulnerável não deve travar merge de produto.
- **Por que `license-check: false` no início:** evita bloqueio por licença transitiva marcada como
  `OTHER`. Ligue depois com `allow-licenses`.
- **Como ler:** o job falha listando pacote, versão e advisory. O resumo também vai como comentário
  quando `comment-summary-in-pr` está ativo.
- **Relação com Dependabot:** Dependabot **propõe** upgrade; dependency-review **impede a entrada**
  de uma versão vulnerável que alguém subiu manualmente.
- **Validação (18/11/2026):** PR descartável com `lodash@4.17.20` falhou como esperado, apontando
  `GHSA-35jh-r3h4-6jhm` e `GHSA-r5fr-rjxr-66jc` (high). PR sem mudança de dependência passa.

### CodeQL

- **Quando roda:** `push` em `main`, `pull_request` para `main` e semanalmente (fase final).
- **Onde:** `.github/workflows/codeql.yml` e `.github/codeql/codeql-config.yml` (implementado).
- **Versão:** `github/codeql-action@v4` (bundle CodeQL 2.27.4 em nov/2026).
- **Linguagens:** `csharp`, `javascript-typescript`, `actions`.
- **Build mode:** `none` nas três. C# não precisa buildar; `.slnx` e .NET 10 são suportados.
- **Escopo:** `paths-ignore` no arquivo de config para `Projetos/cardapio-no-lucro`,
  `Projetos/precifica` e `apps`. Isso evita alerta de código que não está em produção.
- **Limite conhecido:** CodeQL não interpreta arquivos `.astro`. A cobertura de frontend é só
  `.ts`/`.js` em `Projetos/fisga/web/src`. Não prometa análise de componente Astro.
- **Bloqueio planejado:** regra `code_scanning` no ruleset `main-protection`, severidade
  `high_or_higher`, depois da triagem da linha de base.
  Nunca `paths-ignore` no gatilho com essa regra ativa.
- **Como ler:** `Security and quality` → Code scanning, ou
  `gh api "repos/AbsonDev/abson/code-scanning/alerts?state=open"`.
- **Pré-requisito de C#:** o job precisa de `actions/setup-dotnet` com
  `global-json-file: Projetos/fisga/global.json` antes do `init`; sem o SDK 10 o restore do
  `build-mode: none` falha.
- **Validação (18/11/2026):** as três linguagens passaram — `actions` 36 s,
  `javascript-typescript` 1m06s, `csharp` 3m34s. O check `CodeQL` também aparece verde quando o PR
  não introduz alerta novo.

### reviewdog

- **Quando roda:** `pull_request`.
- **Onde:** `.github/workflows/reviewdog.yml` (implementado).
- **O que é:** um reporter, não um linter. Ele pega stdout de outra ferramenta e publica comentário
  na linha do diff (`-filter-mode=added`).
- **Usos que valem:** `actionlint` nos workflows e sugestão de formatação (`action-suggester` com o
  diff de `dotnet format`/`prettier`).
- **O que não vale:** tentar reproduzir o `gate`. Formatação e teste já são obrigatórios lá.
- **Como ler:** comentário inline com sugestão aplicável em um clique. Vermelho é advisory.

### PR-Agent

- **Quando roda:** `pull_request` (`opened`, `reopened`, `ready_for_review`) e comandos em
  `issue_comment`.
- **Onde:** `.github/workflows/pr-review.yml` (implementado).
- **Como roda:** PR-Agent **CLI**, não a action Docker. A imagem publicada mais nova do PR-Agent
  (0.50.0, build de 08/11) fixa LiteLLM 1.105.1, que não tem o provider `opencode_go`; o provider e
  a derivação do `x-opencode-session` entraram no LiteLLM 1.109.1, que o `main` do PR-Agent só passou
  a fixar em 14/11. O job instala o commit pinado com `pip install "pr-agent[github] @ git+..."` e
  usa o provider nativo.
- **Configuração canônica (env do job):**

```yaml
env:
  GITHUB__USER_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  OPENCODE_API_KEY: ${{ secrets.OPENCODE_API_KEY }}
  CONFIG__MODEL: opencode_go/deepseek-v4.1-flash
  CONFIG__FALLBACK_MODELS: '[]'
  CONFIG__CUSTOM_MODEL_MAX_TOKENS: '128000'
  CONFIG__RESTRICTED_MODE: 'true'
  CONFIG__RESPONSE_LANGUAGE: pt-BR
  CONFIG__REASONING_EFFORT: none
  LITELLM__ADD_SESSION_ID_TO_REQUESTS: 'true'
```

- **Session id obrigatório:** o Go rejeita request sem `x-opencode-session` (`MissingSessionID`).
  Com o provider nativo e `add_session_id_to_requests`, o LiteLLM deriva o header do
  `litellm_session_id` que o PR-Agent envia por PR. Com o prefixo `openai/` isso não acontece.
- **Por que `fallback_models=[]`:** o default é `gpt-6-luna`, que exigiria uma chave OpenAI e
  falharia o retry.
- **Por que `reasoning_effort = none`:** em PRs grandes o `deepseek-v4.1-flash` pode streamar
  reasoning por mais de 10 minutos e estourar o timeout do LiteLLM. O próprio PR-Agent documenta
  esse comportamento e recomenda desligar o reasoning ou trocar de modelo.
- **Guardrails:** `restricted_mode = true` (não empurra código), `response_language = pt-BR`,
  `num_max_findings = 6`, `require_tests_review = false` (o gate já roda 408 testes).
- **Comandos:** `/review`, `/describe`, `/ask`, `/improve`, com gate de `author_association` em
  `issue_comment`.
- **Como ler:** comentário do bot com achados numerados. `/ask <pergunta>` responde no thread.
- **Validação (18/11/2026):** run de 1m12s publicou o review em pt-BR com 4 achados inline e o log
  confirmou `Adding reasoning_effort with value none to model opencode_go/deepseek-v4.1-flash`.
- **Quando houver release com LiteLLM >= 1.109.1:** voltar para a action Docker pinada por digest,
  que é mais simples e tem attestation. Até lá, o CLI é o caminho.

### `fisga-docs-ai`

- **Quando roda:** automaticamente em PR não draft de mesmo repo, sem autor bot; sob demanda por
  `/docs` (comentário em PR de `OWNER`, `MEMBER` ou `COLLABORATOR`) e por `workflow_dispatch`.
- **Onde:** `.github/workflows/fisga-docs-ai.yml`; CLI `opencode` pinada em 2.3.2, modelo
  `opencode-go/deepseek-v4.1-flash#max`, chave `OPENCODE_API_KEY`.
- **O que faz:** manda o diff e os documentos para o modelo, que aponta onde a mudança deixou a
  documentação desatualizada. Publica **um** comentário marcado `<!-- fisga-docs-ai -->`; quando o
  modelo responde `SEM_DRIFT`, o comentário anterior é apagado. Não faz revisão inline nem altera
  código (`edit`, `shell` e `subagent` negados via `OPENCODE_CONFIG_CONTENT`).
- **Permissões:** `contents: read`, `issues: write`, `pull-requests: write`. `issues: write` sozinho
  devolve 403 ao comentar em PR; as duas permissões de escrita são necessárias.
- **Por que é advisory:** `continue-on-error: true` e o job não entra em `gate.needs`. Falha do
  modelo ou achado ruim nunca bloqueia merge. PR de fork é pulado e o checkout usa `headRefOid`
  (SHA imutável), não `refs/pull/N/head`, para a checagem de fork e o código analisado serem o mesmo.
- **Validação (19/11/2026):** PR #42, run `44446919922` (8m39s), comentário com 8 drifts reais. As
  correções foram para o PR #46, porque o #42 foi mergeado antes delas (`ef5738f`); o #46 validou
  o loop completo: novos drifts apontados e corrigidos no mesmo PR.

### `/oc`

- **Quando roda:** comentário `/oc` ou `/opencode` em PR, ou comentário em linha do diff.
- **Onde:** `.github/workflows/oc.yml` (implementado).
- **Modelo:** `opencode-go/deepseek-v4.1-flash`, `variant: max`.
- **Gate obrigatório:** só roda para `author_association` em `OWNER`, `MEMBER` ou `COLLABORATOR`.
  Sem isso, qualquer pessoa em repo público queima a franquia.
- **O que faz:** explica o diff, corrige, implementa, responde em linha. Pode commitar no branch do
  PR — por isso `contents: write` e uso sob demanda, nunca automático.
- **Validação (18/11/2026):** run `44367586522` (21 s) após o merge do workflow (#25) publicou a
  resposta `ok` no PR #22. O teste só foi possível depois do merge, porque `issue_comment` sempre usa
  o workflow da branch padrão. Dois avisos não bloqueantes: (1) o `actions/cache@v4` transitivo da
  action ainda roda em Node 20 — a correção depende de release do `anomalyco/opencode`; (2)
  `cache write denied` porque o job não tem `actions: write` — permissão mantida fora de propósito,
  já que o cache não é crítico para uma execução de 21 s.

## Triagem

| Sintoma | Ação |
| --- | --- |
| `gate` vermelho | rodar local: `dotnet format`, `dotnet build -warnaserror`, `dotnet test`, `npm run check`, `npm run build`, `npx --yes markdownlint-cli2@0.24.1`, `node scripts/verificar-rotas.mjs`, `node --test scripts/verificar-rotas.test.mjs`, `lychee --config .lychee.toml ...` e `./scripts/gerar-openapi.sh` + `git diff --exit-code -- docs/api/openapi.json` |
| dependency-review vermelho | atualizar o pacote; se não houver versão corrigida, abrir issue e usar `allow-ghsas` com justificativa |
| CodeQL vermelho | abrir o alerta, seguir o data flow; corrigir ou `dismiss` com motivo (`false positive`, `won't fix`, `used in tests`) |
| CodeQL "analysis not found" no merge | confirmar que o workflow rodou no PR; não filtrar caminho no gatilho |
| reviewdog vermelho | aplicar a sugestão ou ignorar; não é required |
| PR-Agent com achado ruim | responder no thread ou ignorar; usar `/ask` para pedir contexto; `/review` para reavaliar |
| PR-Agent não comentou | conferir secret `OPENCODE_API_KEY`, `CONFIG__MODEL=opencode_go/deepseek-v4.1-flash` e o log do job (`MissingSessionID` = header ausente) |
| PR-Agent falhou no `pip install` | confirmar o extra `[github]` e o commit pinado do PR-Agent |
| `fisga-docs-ai` não comentou | conferir se o PR é draft, fork ou de bot; checar `OPENCODE_API_KEY` e o log do job |
| `fisga-docs-ai` falhou com 403 | conferir `issues: write` **e** `pull-requests: write` no workflow |
| `/docs` não rodou | o workflow precisa existir no branch padrão e o comentário vir de `OWNER`, `MEMBER` ou `COLLABORATOR` |
| `/oc` não rodou | conferir `author_association` do comentário e se o workflow existe no branch padrão |
| `/oc` com warning de Node 20 ou `cache write denied` | ignorar; são transitivos/não bloqueantes e não afetam a resposta |
| `/oc` alterou código errado | reverter o commit no branch e responder no thread com a correção |

## Segurança do fluxo

Repo público muda as regras do jogo. O que vale:

- **Fork não recebe secret.** `pull_request` de fork roda com `GITHUB_TOKEN` read-only. As camadas com
  chave são puladas de propósito. Não use `pull_request_target` com checkout do código do PR.
- **Conteúdo de PR é entrada não confiável.** Título, descrição, diff e comentário podem conter
  instrução para o modelo. Por isso: PR-Agent com `restricted_mode`, `fisga-docs-ai` sem acesso a
  `edit`/`shell`/`subagent`, sem checkout em `pull_request_target`, e `/oc` só para quem tem write.
  O `fisga-docs-ai` resolve o PR e faz checkout pelo `headRefOid`: checagem de fork e código
  analisado usam o mesmo SHA, fechando a janela TOCTOU de `refs/pull/N/head`.
- **`/oc` custa dinheiro.** O gate de `author_association` não é opcional em repo público.
- **Permissões mínimas por workflow:**

| Workflow | Permissões |
| --- | --- |
| `fisga-pipeline` | `contents: read` |
| `dependency-review` | `contents: read`, `pull-requests: write` |
| `codeql` | `contents: read`, `security-events: write`, `actions: read` |
| `reviewdog` | `contents: read`, `pull-requests: write` |
| `pr-review` | `contents: read`, `issues: write`, `pull-requests: write` |
| `fisga-docs-ai` | `contents: read`, `issues: write`, `pull-requests: write` |
| `oc` | `contents: write`, `issues: write`, `pull-requests: write` |

- **`oc` sem `actions: write`:** o `GITHUB_TOKEN` não pode gravar cache, então o post-step da action
  anota `cache write denied`. É esperado e não falha o job; só não reaproveita o cache entre runs.
  Não conceda `actions: write` só por causa disso — a permissão também permite cancelar runs e
  apagar artefatos.

- **Segredos:** só nomes. `OPENCODE_API_KEY` é a única chave de modelo; o resto é `GITHUB_TOKEN`
  efêmero do run. Nenhum valor vai para o repositório, para o documento ou para log.
- **Rotação:** revogar a chave no Console do Go e recriar; `gh secret set OPENCODE_API_KEY` grava
  sem eco.

## Operação

### Secrets e variáveis

| Nome | Onde | Usado por |
| --- | --- | --- |
| `OPENCODE_API_KEY` | Actions secret | `pr-review`, `fisga-docs-ai`, `oc` |
| `GITHUB_TOKEN` | automático | todos |
| `COOLIFY_*` | Actions secret | `fisga-deploy` (inalterado) |

A chave do Go é pessoal e conta na franquia do membro. Limites: 5 horas = 20% da franquia mensal;
semana = 50%, resetando segunda 00:00 UTC. Review de PR gasta pouco; `/oc` gasta muito mais porque
é agente com múltiplas rodadas.

### Pinning e `actions.lock`

Toda action nova entra pinada e registrada:

```bash
gh actions-lock
git diff --stat .github/workflows/actions.lock
```

Push que altera `.github/workflows/**` exige token com escopo `workflow`. O `gh` do host usa OAuth
(`gho_`); se faltar escopo, `gh auth refresh -s workflow`. Nunca colar PAT em chat ou arquivo.

### Custo

- Repo público: runners padrão gratuitos e CodeQL sem custo.
- dependency-review e reviewdog: R$ 0.
- PR-Agent: `pip install` do commit pinado (~40 s) + uma chamada por comando, centavos dentro da
  franquia Go.
- `fisga-docs-ai`: uma chamada em todo PR elegível, mais uma por `/docs`; o run de validação levou
  ~9 min e publicou 8 achados no PR #42, centavos dentro da franquia Go.
- `/oc`: 10–50 chamadas por execução; o gasto real aparece no Console do Go.

## Métricas e promoção

Nenhuma camada nova vira required no primeiro dia. O ciclo é: advisory por 2–3 semanas, medir,
decidir.

| Métrica | Como medir | Meta |
| --- | --- | --- |
| Achados emitidos | contagem de comentários do bot por PR | referência |
| Achados aceitos | commits que aplicam a sugestão / total | ≥ 50% para manter |
| Falso positivo | `dismiss` no CodeQL / total de alertas | < 20% |
| Tempo até primeiro review | timestamp do comentário − abertura do PR | < 5 min |
| Custo por PR | Console do Go | dentro da franquia |

Decisão:

| Taxa de aceitação | Ação |
| --- | --- |
| ≥ 50% | manter; avaliar aumentar `num_max_findings` |
| 20–50% | ajustar `extra_instructions`, reduzir escopo |
| < 20% | remover a camada (deletar o workflow) |

**Nunca promova PR-Agent, `fisga-docs-ai` ou `/oc` a required check.** A decisão de merge continua
humana.

## Rollback e pendências

Rollback é deletar o workflow. Nenhuma camada exige mudança no `fisga-pipeline` ou no `gate`.

| Camada | Reversão |
| --- | --- |
| dependency-review | deletar o workflow; remover required check do ruleset |
| CodeQL | deletar o workflow; remover a regra `code_scanning`; alertas ficam no histórico |
| reviewdog | deletar o workflow |
| PR-Agent | deletar `.github/workflows/pr-review.yml`; revogar a chave só se não for usar no `fisga-docs-ai` nem no `/oc` |
| `fisga-docs-ai` | deletar `.github/workflows/fisga-docs-ai.yml`; não mexe em `gate.needs` |
| `/oc` | deletar o workflow |

Pendências conhecidas:

- Acompanhar release do `anomalyco/opencode` que atualize o `actions/cache` transitivo para Node 24;
  até lá o warning de deprecação aparece em todo run do `/oc` e é inofensivo.
- Voltar o PR-Agent para a action Docker quando sair release com LiteLLM >= 1.109.1; aí pinar a
  imagem por digest e remover o `pip install`.
- Validar o parser de `astro check` no reviewdog; se não encaixar, ficar só com `actionlint`.
- Decidir se o CodeQL roda em PR desde o início ou só depois da triagem da linha de base.
- Fase B do CI/CD (GHCR, E2E real, preview deployments) continua em [ci-cd.md](./ci-cd.md).
