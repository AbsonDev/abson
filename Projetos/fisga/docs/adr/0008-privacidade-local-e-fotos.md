# ADR 0008 — Privacidade do local e múltiplas fotos por captura

**Status:** parcialmente substituído pelo [ADR 0015](0015-niveis-de-detalhe-do-local.md) · **Data:** 2026-11-17

> A substituição alcança apenas a decisão dos três valores de `PrivacidadeDoLocal`. As decisões
> sobre fotos — quatro por captura, capa e galeria em JSON — permanecem aceitas.

## Contexto

O link público de uma pescaria mostrava o local exato, mesmo quando o pescador queria compartilhar
apenas o troféu. Ao mesmo tempo, uma única foto por captura limitava o card e o PDF.

## Decisão

- `Pescaria.PrivacidadeDoLocal` tinha três valores: `Exato`, `Cidade` e `Oculto` — decisão
  substituída pelo [ADR 0015](0015-niveis-de-detalhe-do-local.md), que define cinco níveis.
- A projeção pública usa `LocalPublico`, `CidadePublica` e `EstadoPublico`; o diário privado
  continua mostrando o local completo.
- O PDF, o troféu e o link público usam a projeção pública.
- `Captura` aceita até quatro fotos e uma capa; a primeira foto é a capa padrão.
- A capa é persistida na coluna antiga `FotoUrl`; a galeria fica em uma coluna `Fotos` serializada
  em JSON. Isso preserva os dados existentes e evita uma tabela nova.
- A exclusão de conta remove todas as fotos da galeria.

## Consequências

- O pescador pode compartilhar sem entregar o ponto exato.
- O troféu usa a capa escolhida; a página pública mostra a galeria.
- O modelo de captura fica mais rico sem quebrar links de troféu existentes.
- A galeria em JSON é adequada para até 4 URLs; se o produto crescer para dezenas de fotos, vale
  migrar para uma tabela própria/object storage.
