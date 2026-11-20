# ADR 0017 — Portal público da API gerado a partir do contrato

**Status:** aceito · **Data:** 2026-11-20 · **Issue:** [#58](https://github.com/AbsonDev/abson/issues/58)

## Contexto

O Fisga tem 54 operações em 48 rotas e um `openapi.json` (OpenAPI 3.1.1) gerado
por build, comparado em CI e usado pelo web. Esse contrato nunca sai do
repositório: em produção, `/openapi/v1.json`, `/swagger` e `/scalar` respondem
`404`. Quem integra precisa ler o código-fonte.

O contrato também não contava a história toda. O
[inventário](../api/inventario.md) lista as lacunas do contrato: `201`/`202`/`204`
ausentes, formato de erro real (`{codigo, detalhe}`) em vez de RFC 9457, uploads
multipart sem schema e rotas administrativas que respondem `404` de propósito.
Segurança, tags, `operationId`, rate limit e cache foram resolvidos no A1
(2026-11-20).

Publicar esse contrato cru produziria documentação que mente. Publicar
documentação escrita à mão sem contrato produziria documentação que envelhece
no primeiro PR. A decisão precisa resolver as duas coisas ao mesmo tempo e
sobreviver ao CI que já existe (`gate` + `workflow_run`).

## Decisão

1. **Portal separado em `docs.fisga.abson.tech`**, estático, servido por um app
   próprio no Coolify. Não é rota do app (`fisga.abson.tech/docs`) nem do
   domínio da API.
2. **O contrato é a fonte da verdade do que é publicado.** A referência é gerada
   em tempo de build a partir de `docs/api/openapi.json`; nenhuma operação
   documentada pode existir só na prosa. Prosa cobre propósito, acesso,
   autenticação, limites, erros e exemplos.
3. **Corrigir o contrato é pré-requisito da publicação.** F1 fecha as lacunas do
   contrato antes de o portal ir ao ar; o portal nasce com status reais e
   schemas de erro.
4. **Leitura pública de tudo, execução restrita.** Todas as operações são
   documentadas, inclusive as administrativas. O console "Experimente" só
   executa `GET` públicos; escritas e rotas de sessão exigem credencial do
   próprio leitor, e as administrativas são explicitamente não executáveis.
5. **Sem sandbox na v1.** Ambiente de testes dedicado fica no backlog (F4), com
   seed e dados sintéticos.
6. **Portal é estático e sem runtime de terceiros.** HTML pré-renderizado por
   operação, busca local (Pagefind), e um único *island* JavaScript para o
   console. Nada de Mintlify, ReadMe, Scalar hospedado ou embed de CDN.
7. **Publicação versionada.** Cada release do portal carrega a versão do
   contrato e o commit de origem; `/changelog` é alimentado pelo histórico de
   mudanças do spec, não por memória de quem escreveu.

## Alternativas descartadas

| Alternativa | Por que não |
| --- | --- |
| **A — rota `/docs` no app Astro do Fisga** | Amarra o ciclo de deploy da documentação ao do produto. Um typo em um guia exigiria deploy do app inteiro, e o portal herdaria SSR, Sentry e cookies do app. |
| **C — plataforma hospedada (Mintlify, ReadMe, Bump.sh)** | Rápido no começo, mas preço por assento, lock-in de conteúdo, dependência de CDN de terceiros e pouca liberdade para a identidade do Fisga. Contraria a decisão de manter a documentação no repositório. |
| **Embed de Scalar/Redoc em runtime** | Referência só existe com JavaScript, sem HTML indexável, sujeita a mudança de comportamento de CDN e a CSP mais frouxa. O visual também não conversa com o design system do Fisga. |
| **Documentação manual em Markdown sem geração** | Divergiria do contrato no primeiro endpoint novo. Já acontece hoje: o resumo do README, o inventário e o spec só coincidem enquanto alguém compara à mão; nenhum job falha quando um endpoint novo entra só no código. |
| **Publicar o `openapi.json` cru em produção** | Não resolve nada do ponto de vista de quem integra: continua sendo leitura de máquina sobre um contrato ainda incompleto. |

## Consequências

- **Positivas.** Uma fonte de verdade; o portal quebra o build quando o contrato
  e a documentação divergem. Referência completa e indexável, em português, com
  a identidade do Fisga. Custo de hospedagem de site estático. Nenhum segredo
  novo no runtime: o portal não fala com o banco.
- **Negativas.** F1 vira bloqueio real de cronograma — não dá para "publicar
  logo e corrigir depois". Exige um terceiro app e um terceiro webhook no
  Coolify, e um job `docs-site` no pipeline que precisa entrar na lista de
  dependências do `gate`.
- **Riscos.** (a) Divergência silenciosa se alguém editar o HTML gerado — o
  build sobrescreve, então a regra é "nunca editar saída". (b) Referência
  pública de rotas administrativas aumenta a superfície de reconhecimento; a
  mitigação é que elas já respondem `404` sem token e o portal diz isso.
  (c) Portal e app podem divergir de identidade visual — mitigado pelo
  `DESIGN.md` versionado em `design-system/`.
- **Invariantes preservados.** O contrato continua gerado por
  `scripts/gerar-openapi.sh` e comparado no job `docs`; o portal **consome** esse
  arquivo, não o reescreve. Nenhuma operação da API muda de comportamento nesta
  fase.

## Desdobramentos

- [Inventário da API](../api/inventario.md) — matriz de cobertura e lacunas.
- [Política editorial](../api/politica-editorial.md) — o que toda página precisa ter.
- [Arquitetura do portal](../api/portal.md) — mapa de URL, navegação e build.
- [Backlog F1–F4](../api/backlog.md) — ordem de execução.
