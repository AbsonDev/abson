# ADR 0014 — Registro anônimo: capturar primeiro, conta depois

**Status:** aceito · **Data:** 2026-11-19

## Contexto

O registro exigia login antes da primeira digitação: `/registrar` abria bloqueado para quem não
tinha sessão. Isso punha a conta no começo da jornada, exatamente quando o pescador ainda não tinha
recebido valor nenhum, e fazia o link de condições perder força — a pessoa chegava com local, data e
maré prontos e caía no login. O [ADR 0010](0010-condicoes-e-locais.md) previa preenchimento
automático condicionado à conta; esta decisão revisa esse ponto.

## Decisão

- **O formulário em `/registrar` fica ativo para anônimos.** Não há `inert`, overlay nem bloqueio:
  viagem, condições e capturas podem ser preenchidas sem sessão. A conta entra como explicação no
  envio e no campo de foto, não como porta de entrada.
- **O rascunho é local** (`fisga:rascunho:v1`) e funciona para qualquer visitante. A hidratação é
  síncrona, antes de qualquer requisição, para não apagar a digitação inicial; o indicador diz
  “neste aparelho” e nunca promete nuvem, backup ou sincronização. Logout explícito e sessão
  expirada (401) descartam o rascunho, como já acontecia.
- **Condições da URL vencem o rascunho salvo**, mas nunca apagam digitação ao vivo: campos tocados
  antes da hidratação permanecem com o valor da pessoa, e um aviso inline explica o que veio do
  link. Sem `lat`/`lon`, o rascunho é restaurado integralmente.
- **`?editar=<id>` continua autenticado.** Com sessão, a pescaria salva é carregada antes de qualquer
  rascunho ou condição da URL; não mesclamos nada sobre um registro já salvo sem intenção.
- **Foto sem sessão gera nota inline, sem redirect.** O seletor não abre e o formulário continua no
  mesmo lugar; o texto diz que a foto entra depois do login e que o resto do registro fica onde
  está.
- **Submit anônimo abre confirmação explícita, sem auto-submit.** O painel oferece “Entrar e salvar
  no diário” e “Continuar aqui”; o rascunho é preservado em ambos os caminhos.
- **A conta só é pedida ao salvar.** Preencher, revisar e manter o rascunho no aparelho são públicos;
  diário e locais salvos continuam privados e autenticados.

## Consequências

- O valor das condições chega antes do cadastro: o link de `/condicoes` passa a ser entrada de
  captura, não de login.
- O rascunho deixa de ser exclusivo de quem entra; sem nuvem, ele vive só no aparelho e some em
  logout/401 — o indicador e a nota de erro precisam dizer isso sem ambiguidade.
- A ordem de carregamento passa a ser parte do contrato: editar > digitação ao vivo > condições da
  URL > rascunho salvo. Regras diferentes para anônimo e logado exigiriam dois fluxos e gerariam
  perda de dados.
- A confirmação no submit adiciona um passo, mas evita redirect surpresa e mantém o preenchimento.
- Os testes que descreviam o formulário bloqueado foram reescritos, não apenas desativados.

## Referências

- Design: [`docs/superpowers/specs/2026-11-19-registrar-anonimo-design.md`](../superpowers/specs/2026-11-19-registrar-anonimo-design.md)
- ADR revisado: [`0010-condicoes-e-locais.md`](0010-condicoes-e-locais.md)
