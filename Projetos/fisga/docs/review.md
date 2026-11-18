# Review do Fisga

Este documento é a fonte da verdade do fluxo de revisão de PRs do Fisga. Ele diz o que roda, o que
bloqueia o merge e o que é apenas advisory. As camadas de CI e CD estão em [ci-cd.md](./ci-cd.md);
aqui está só o julgamento do diff.

**Status em 18/11/2026:** o `gate` é a única camada implementada. As demais estão planejadas e
marcadas como tal na tabela de estado. Nada neste documento deve ser lido como "já funciona".

## Princípio

A revisão é uma pirâmide, não uma pilha de ferramentas. Cada camada responde a uma pergunta
diferente, e a regra é uma só: **se uma máquina consegue decidir com certeza, não gaste token.**

```text
1. determinístico   build · format · testes · astro check      (já existe: gate)
2. segurança        CodeQL · dependency-review                 (planejado)
3. qualidade inline reviewdog                                  (planejado)
4. julgamento       PR-Agent                                   (planejado)
5. ação             /oc                                        (planejado)
```

- **Determinístico** decide binário: passou ou não passou. Pode bloquear.
- **Segurança** também é binário quando a regra dispara. Pode bloquear com escopo estreito.
- **Qualidade inline** só mostra o que o linter já achou. Não bloqueia.
- **Julgamento** é probabilístico. Nunca bloqueia.
- **Ação** altera código. Só roda sob demanda e com permissão explícita.

O objetivo não é substituir revisão humana. Nenhum benchmark de revisor automático encontra metade
dos bugs conhecidos; a camada serve para o humano gastar atenção no que só humano vê.

## Estado atual

| Camada | Ferramenta | Status | Bloqueia? | Custo |
|---|---|---|---|---|
| CI | `gate` (build, format, 250 testes, `astro check`, Playwright) | implementado | sim, único required check | R$ 0 |
| Dependências | Dependabot | implementado | não (propõe PRs) | R$ 0 |
| Supply chain | `dependency-review-action@v5` | planejado | sim, quando promovido | R$ 0 |
| Segurança estática | CodeQL (`csharp`, `javascript-typescript`, `actions`) | planejado | sim, via regra `code_scanning` | R$ 0 (repo público) |
| Qualidade inline | reviewdog | planejado | não | R$ 0 |
| Revisor LLM | PR-Agent + OpenCode Go | planejado | não | franquia Go |
| Agente | OpenCode `/oc` | planejado | não (sob demanda) | franquia Go |

Hoje não existe nenhum revisor automático de diff. O `gate` valida que o código compila, formata e
passa nos testes; ele não lê a intenção da mudança.

## Fluxo de um PR

```text
PR aberto ou atualizado
├── fisga-pipeline / gate ........ bloqueia (build, format, testes, astro check, Playwright)
├── dependency-review ............ bloqueia CVE high+ e licença inválida (planejado)
├── codeql (3 linguagens) ........ bloqueia alerta high+ via ruleset (planejado)
├── reviewdog .................... advisory: linter inline (planejado)
└── pr-review (PR-Agent) ......... advisory: julgamento do diff (planejado)
        │
        ├── humano revisa e decide
        └── /oc sob demanda ...... explica, corrige, implementa (planejado)

merge em main
└── fisga-deploy ................. só com CI verde; ver ci-cd.md
```

Caminhos especiais:

- **PR só de docs:** o `gate` roda (sem `paths:` no gatilho) e o CodeQL roda. Se o CodeQL tivesse
  filtro de caminho no gatilho, o merge travaria por "análise não executada" quando a regra
  `code_scanning` estivesse ativa. Ver [CodeQL](#codeql).
- **PR de fork:** `pull_request` não recebe secrets e o `GITHUB_TOKEN` é read-only. As camadas que
  dependem de chave (PR-Agent, `/oc`) são puladas; CodeQL e `gate` rodam.
- **PR de bot (Dependabot, Sentry):** os exemplos usam `github.event.sender.type != 'Bot'` para não
  gastar franquia e não duplicar comentário.

## O que bloqueia e o que é advisory

| Camada | Bloqueia? | Por quê |
|---|---|---|
| `gate` | **sim** | determinístico, reproduzível, é o contrato de merge |
| dependency-review | **sim** | binário: CVE conhecida ou licença inválida não entram |
| CodeQL (`high+`) | **sim**, via ruleset | binário quando a regra dispara; escopo estreito evita ruído |
| reviewdog | não | só reporta linter no diff; vermelho não significa bug |
| PR-Agent | não | probabilístico, muda com o modelo, custa token |
| `/oc` | não | agente; pode alterar código e errar |

Regra permanente: **só camada determinística pode ser required check.** Promover LLM a obrigatório
transforma um falso positivo em bloqueio de merge sem dono, e a única saída vira `dismiss` — que
ensina o time a ignorar a ferramenta.

## Camadas

### `gate`

- **Quando roda:** todo PR e todo push em `main`.
- **Onde:** `.github/workflows/fisga-pipeline.yml`.
- **O que valida:** restore locked, `dotnet format --verify-no-changes`, build `-warnaserror`, 250
  testes, `npm ci`, `astro check`, build, 61 Playwright mockados, build das imagens.
- **Como ler:** o job `gate` agrega; um job `failure`/`cancelled` derruba o gate.
- **Reexecutar:** push novo no PR, ou `gh run rerun <run-id> --repo AbsonDev/abson`.

### dependency-review

- **Quando roda:** `pull_request`.
- **Onde:** `.github/workflows/dependency-review.yml` (a criar).
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

### CodeQL

- **Quando roda:** `push` em `main`, `pull_request` para `main` e semanalmente (fase final).
- **Onde:** `.github/workflows/codeql.yml` e `.github/codeql/codeql-config.yml` (a criar).
- **Versão:** `github/codeql-action@v4` (bundle CodeQL 2.27.4 em nov/2026).
- **Linguagens:** `csharp`, `javascript-typescript`, `actions`.
- **Build mode:** `none` nas três. C# não precisa buildar; `.slnx` e .NET 10 são suportados.
- **Escopo:** `paths-ignore` no arquivo de config para `Projetos/cardapio-no-lucro`,
  `Projetos/precifica` e `apps`. Isso evita alerta de código que não está em produção.
- **Limite conhecido:** CodeQL não interpreta arquivos `.astro`. A cobertura de frontend é só
  `.ts`/`.js` em `Projetos/fisga/web/src`. Não prometa análise de componente Astro.
- **Bloqueio:** regra `code_scanning` no ruleset `main-protection`, severidade `high_or_higher`.
  Nunca `paths-ignore` no gatilho com essa regra ativa.
- **Como ler:** `Security and quality` → Code scanning, ou
  `gh api "repos/AbsonDev/abson/code-scanning/alerts?state=open"`.

### reviewdog

- **Quando roda:** `pull_request`.
- **Onde:** `.github/workflows/reviewdog.yml` (a criar).
- **O que é:** um reporter, não um linter. Ele pega stdout de outra ferramenta e publica comentário
  na linha do diff (`-filter-mode=added`).
- **Usos que valem:** `actionlint` nos workflows e sugestão de formatação (`action-suggester` com o
  diff de `dotnet format`/`prettier`).
- **O que não vale:** tentar reproduzir o `gate`. Formatação e teste já são obrigatórios lá.
- **Como ler:** comentário inline com sugestão aplicável em um clique. Vermelho é advisory.

### PR-Agent

- **Quando roda:** `pull_request` (`opened`, `reopened`, `ready_for_review`) e comandos em
  `issue_comment`.
- **Onde:** `.github/workflows/pr-review.yml` e `.pr_agent.toml` (a criar).
- **Modelo:** OpenCode Go via endpoint OpenAI-compatible.
- **Configuração canônica do provider:**

```yaml
env:
  OPENAI.KEY: ${{ secrets.OPENCODE_API_KEY }}
  OPENAI.API_BASE: https://opencode.ai/inference/go/v1
  config.model: openai/deepseek-v4.1-flash
  config.custom_model_max_tokens: "128000"
  config.fallback_models: '[]'
```

- **Guardrails:** `restricted_mode = true` (não empurra código), `response_language = "pt-BR"`,
  `num_max_findings = 6`, `require_tests_review = false` (o gate já roda 250 testes).
- **Comandos:** `/review`, `/describe`, `/ask`, `/improve`.
- **Risco conhecido:** o Go é otimizado para clientes de coding agent e validou OpenCode, Claude
  Code, Codex, Hermes, Pi, ZCode, jcode e Kilo. PR-Agent via LiteLLM não está na lista; o
  user-agent e o header `x-opencode-session` não são os nativos. Se o gateway recusar, o plano B é
  usar o OpenCode Action como revisor.
- **Como ler:** comentário do bot com achados numerados. `/ask <pergunta>` responde no thread.

### `/oc`

- **Quando roda:** comentário `/oc` ou `/opencode` em PR, ou comentário em linha do diff.
- **Onde:** `.github/workflows/oc.yml` (a criar).
- **Modelo:** `opencode-go/deepseek-v4.1-flash`, `variant: max`.
- **Gate obrigatório:** só roda para `author_association` em `OWNER`, `MEMBER` ou `COLLABORATOR`.
  Sem isso, qualquer pessoa em repo público queima a franquia.
- **O que faz:** explica o diff, corrige, implementa, responde em linha. Pode commitar no branch do
  PR — por isso `contents: write` e uso sob demanda, nunca automático.

## Triagem

| Sintoma | Ação |
|---|---|
| `gate` vermelho | rodar local: `dotnet format`, `dotnet build -warnaserror`, `dotnet test`, `npm run check`, `npm run build` |
| dependency-review vermelho | atualizar o pacote; se não houver versão corrigida, abrir issue e usar `allow-ghsas` com justificativa |
| CodeQL vermelho | abrir o alerta, seguir o data flow; corrigir ou `dismiss` com motivo (`false positive`, `won't fix`, `used in tests`) |
| CodeQL "analysis not found" no merge | confirmar que o workflow rodou no PR; não filtrar caminho no gatilho |
| reviewdog vermelho | aplicar a sugestão ou ignorar; não é required |
| PR-Agent com achado ruim | responder no thread ou ignorar; usar `/ask` para pedir contexto; `/review` para reavaliar |
| PR-Agent não comentou | conferir secret `OPENCODE_API_KEY`, `OPENAI.API_BASE` e o log do container |
| `/oc` não rodou | conferir `author_association` do comentário e se o workflow existe no branch padrão |
| `/oc` alterou código errado | reverter o commit no branch e responder no thread com a correção |

## Segurança do fluxo

Repo público muda as regras do jogo. O que vale:

- **Fork não recebe secret.** `pull_request` de fork roda com `GITHUB_TOKEN` read-only. As camadas com
  chave são puladas de propósito. Não use `pull_request_target` com checkout do código do PR.
- **Conteúdo de PR é entrada não confiável.** Título, descrição, diff e comentário podem conter
  instrução para o modelo. Por isso: PR-Agent com `restricted_mode`, sem checkout em
  `pull_request_target`, e `/oc` só para quem tem write.
- **`/oc` custa dinheiro.** O gate de `author_association` não é opcional em repo público.
- **Permissões mínimas por workflow:**

| Workflow | Permissões |
|---|---|
| `fisga-pipeline` | `contents: read` |
| `dependency-review` | `contents: read`, `pull-requests: write` |
| `codeql` | `contents: read`, `security-events: write`, `actions: read` |
| `reviewdog` | `contents: read`, `pull-requests: write` |
| `pr-review` | `contents: read`, `issues: write`, `pull-requests: write` |
| `oc` | `contents: write`, `issues: write`, `pull-requests: write` |

- **Segredos:** só nomes. `OPENCODE_API_KEY` é a única chave de modelo; o resto é `GITHUB_TOKEN`
  efêmero do run. Nenhum valor vai para o repositório, para o documento ou para log.
- **Rotação:** revogar a chave no Console do Go e recriar; `gh secret set OPENCODE_API_KEY` grava
  sem eco.

## Operação

### Secrets e variáveis

| Nome | Onde | Usado por |
|---|---|---|
| `OPENCODE_API_KEY` | Actions secret | `pr-review`, `oc` |
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
- PR-Agent: uma chamada por comando, centavos dentro da franquia Go.
- `/oc`: 10–50 chamadas por execução; o gasto real aparece no Console do Go.

## Métricas e promoção

Nenhuma camada nova vira required no primeiro dia. O ciclo é: advisory por 2–3 semanas, medir,
decidir.

| Métrica | Como medir | Meta |
|---|---|---|
| Achados emitidos | contagem de comentários do bot por PR | referência |
| Achados aceitos | commits que aplicam a sugestão / total | ≥ 50% para manter |
| Falso positivo | `dismiss` no CodeQL / total de alertas | < 20% |
| Tempo até primeiro review | timestamp do comentário − abertura do PR | < 5 min |
| Custo por PR | Console do Go | dentro da franquia |

Decisão:

| Taxa de aceitação | Ação |
|---|---|
| ≥ 50% | manter; avaliar aumentar `num_max_findings` |
| 20–50% | ajustar `extra_instructions`, reduzir escopo |
| < 20% | remover a camada (deletar o workflow) |

**Nunca promova PR-Agent ou `/oc` a required check.** A decisão de merge continua humana.

## Rollback e pendências

Rollback é deletar o workflow. Nenhuma camada exige mudança no `fisga-pipeline` ou no `gate`.

| Camada | Reversão |
|---|---|
| dependency-review | deletar o workflow; remover required check do ruleset |
| CodeQL | deletar o workflow; remover a regra `code_scanning`; alertas ficam no histórico |
| reviewdog | deletar o workflow |
| PR-Agent | deletar workflow e `.pr_agent.toml`; revogar a chave se não for usar no `/oc` |
| `/oc` | deletar o workflow |

Pendências conhecidas:

- Validar PR-Agent + OpenCode Go (compatibilidade LiteLLM) num PR de smoke antes de promover.
- Validar o parser de `astro check` no reviewdog; se não encaixar, ficar só com `actionlint`.
- Decidir se o CodeQL roda em PR desde o início ou só depois da triagem da linha de base.
- Fase B do CI/CD (GHCR, E2E real, preview deployments) continua em [ci-cd.md](./ci-cd.md).
