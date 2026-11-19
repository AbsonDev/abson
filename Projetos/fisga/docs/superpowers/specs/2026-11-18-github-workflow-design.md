# Fisga — fluxo de trabalho no GitHub (Projects + Issues + PRs)

**Data:** 18/11/2026  
**Status:** aprovado pelo usuário em 18/11/2026  
**Escopo:** processo de trabalho do Fisga, não novo CI/CD.

## Intenção

Fazer o GitHub ser o único lugar onde um achado de UX, bug ou ideia nasce, é priorizado,
vira código, passa pela esteira de review e chega em produção. Hoje a esteira de entrega já
existe e funciona; o que falta é entrada organizada e regra de passagem explícita.

## Objetivos

- Nenhum achado ficar só no chat.
- Todo PR ligado a uma issue (`Closes #N`).
- O quadro refletir o estado real do trabalho.
- Merge só depois de responder e resolver todas as threads de revisores automáticos.
- Deploy exclusivamente pelo `fisga-deploy`, com `/health` no SHA mergeado.

## Não-objetivos

- Não criar novo CI nem substituir `fisga-pipeline` / `fisga-deploy`.
- Não substituir o OpenDesign; o handoff `DESIGN-UX.md` continua sendo a referência visual.
- Não publicar crítica crua de UX no repositório público.
- Não fazer deploy manual ou paralelo ao merge de outro PR.

## Anti-objetivos

- Quadro decorativo, sem uso.
- Issue sem PR e PR sem issue.
- Merge com comentário de bot pendente.
- Merge durante deploy em andamento.

## Restrições

- Repo público `AbsonDev/abson`; quadro `projects/6` é privado.
- Ruleset `main-protection` (id `27557615`): `gate` strict, 0 aprovações,
  `required_review_thread_resolution: false`, force push e deleção bloqueados.
- Revisores automáticos: `pr-review` (PR-Agent: `/review`, `/ask`, `/describe`, `/improve`),
  `codeql`, `dependency-review`, `reviewdog` e `/oc` (agente sob demanda, pode commitar).
- `fisga-deploy` dispara por `workflow_run`, builda o HEAD da branch e compara `/health` com
  `head_sha`; portanto merge durante deploy invalida o health check.
- Todo workflow novo precisa de actions pinadas e `gh actions-lock`.
- Token de automação do quadro precisa de escopo `project`; fica em Secrets, nunca no repo/chat.
- Copy e documentação em pt-BR.

## Abordagens consideradas

| Opção | Trade-off |
| --- | --- |
| A — issue pública primeiro | Rastreabilidade máxima, mas expõe achados crus no repo público. |
| B — híbrido (escolhida) | Draft privado para triar; promoção para issue pública só quando acordado. Casa com quadro privado + repo público. Custo: um passo de promoção. |
| C — PR primeiro | Mais rápido, mas sem backlog, sem iteração e sem histórico. |

## Decisões

1. **Entrada:** todo achado vira draft issue no quadro com *o que vi*, *evidência*
   (URL, `arquivo:linha`), *impacto no usuário* e *critério de aceite*.
2. **Refinamento:** ao escolher o trabalho, promover o draft para issue em `AbsonDev/abson`
   e preencher `Priority`, `Size`, `Iteration` e datas.
3. **Labels:** reaproveitar `bug`, `enhancement`, `accessibility`, `documentation` e
   `dependencies`; criar `ux` para agrupar achados de interface.
4. **Template de issue:** `.github/ISSUE_TEMPLATE/fisga.yml`, com resumo, área
   (API/Web/UX/Docs), evidência, reprodução, esperado, critério de aceite e link do artefato.
5. **Template de PR:** `.github/PULL_REQUEST_TEMPLATE.md`, com `Closes #123`, checklist de
   `gate`, threads resolvidas, screenshots antes/depois e `/health` conferido.
6. **Branches:** `fix/123-<slug>` ou `feat/123-<slug>`, ligadas à issue.
7. **Status:** `Backlog` → `Ready` → `In progress` → `In review` → `Done`. Nesta fase o `Done` do
   sync marca o merge; o `/health` no SHA mergeado continua sendo verificação obrigatória antes de
   considerar a entrega concluída, e mover o item só depois do deploy é automação da Fase B. Se o
   deploy falhar, reabrir a issue.
8. **Review:** ler PR-Agent, CodeQL, dependency-review, reviewdog e commits do `/oc`;
   responder em cada thread e marcar `isResolved: true` via API. Ligar
   `required_review_thread_resolution: true` no ruleset para parar de depender de memória.
9. **Automação:** auto-adicionar issues/PRs ao quadro e sincronizar Status via Action que
   chama a CLI `gh` com `FISGA_PROJECT_TOKEN`. Script Node com `node:test`, sem dependências.
10. **Métricas:** tempo issue→PR, tempo PR→deploy, itens por iteração e merges com thread aberta.

## Pronto

- Templates e label `ux` no repo.
- Script de sincronização com testes passando.
- Workflow `project-sync` com actions lockadas.
- Ruleset exigindo resolução de threads.
- Documentação publicada e linkada a partir de `docs/ci-cd.md` e `docs/review.md`.
- Drafts dos achados do `/registrar` no quadro, com Priority/Size/Iteration preenchidos.
