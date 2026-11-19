# ADR 0015 — Níveis de detalhe do local compartilhado

**Status:** aceito · **Data:** 2026-11-19

## Contexto

O link público de uma pescaria mostrava o ponto exato por padrão. A decisão anterior
([ADR 0008](0008-privacidade-local-e-fotos.md)) definia três valores — `Exato`, `Cidade` e
`Oculto` — insuficientes para compartilhar a história sem entregar o ponto: o pescador queria
poder citar o rio, a lagoa ou um trecho sem revelar a coordenada, e o padrão deveria proteger
quem não ajusta a configuração.

A especificação
[`2026-11-19-niveis-de-detalhe-do-local-design.md`](../superpowers/specs/2026-11-19-niveis-de-detalhe-do-local-design.md)
e o draft privado "Níveis de detalhe do local no link público" no quadro do projeto registram o
problema e as alternativas consideradas.

## Decisão

- `PrivacidadeDoLocal` passa a ter cinco valores nomeados: `Exato`, `Trecho`, `CorpoAgua`,
  `Cidade` e `Oculto`. Os nomes antigos são preservados para não exigir migração de dados; o
  enum é persistido como string com no máximo 16 caracteres.
- O padrão passa a ser `Cidade` (privacidade por padrão) no domínio, no DTO de entrada e no
  formulário de registro, inclusive na edição.
- Novo campo opcional `CorpoOuTrecho` (até 80 caracteres) para nomear rio, lagoa ou trecho.
  `Trecho` mostra o campo inteiro; `CorpoAgua` mostra o texto até a primeira vírgula.
- O DTO público nunca devolve `CorpoOuTrecho` cru nem coordenada: a projeção pública entrega
  somente `LocalPublico`, `CidadePublica` e `EstadoPublico`.
- Não há raio em metros nem captura de latitude/longitude nova: o texto livre `Local` e o
  `PontoPrivado` continuam como estão. O feed/comunidade e a agregação por bacia pertencem a uma
  especificação separada e não entram nesta decisão.

## Consequências

- Se `CorpoOuTrecho` estiver vazio, os níveis `Trecho` e `CorpoAgua` caem para `Cidade` — nunca
  para `Local`, para não vazar o nome do ponto por acidente.
- PDF, card de troféu e link público usam a mesma projeção `LocalPublico`; o badge do link
  passa a ter um rótulo por nível, e o troféu deixa de repetir cidade/UF já apresentadas.
- Pescarias existentes com `Exato` permanecem exatas até que o pescador altere a escolha;
  nenhuma linha é reescrita pela migração, que apenas acrescenta a coluna opcional.
- Upvote e raio em metros ficam para fase posterior, sem impacto neste contrato.
