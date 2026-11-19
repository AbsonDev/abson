# ADR 0016 — Confirmação explícita dos dados da foto

**Status:** aceito · **Data:** 2026-11-19

## Contexto

O `/registrar` lia EXIF/GPS e gravava data, período, cidade/UF e até o campo `local` diretamente
nos inputs, antes de qualquer confirmação. Isso sobrescrevia campos já digitados e podia transformar
a cidade inferida do GPS no nome do ponto exato que alimenta o link público — informação que a
[ADR 0015](0015-niveis-de-detalhe-do-local.md) protege. A
[ADR 0012](0012-ferramentas-exif-e-visao.md) registra a leitura automática, mas não a decisão de
gravar sem perguntar.

A especificação
[`2026-11-19-exif-confirmacao-design.md`](../superpowers/specs/2026-11-19-exif-confirmacao-design.md)
e a issue #50 registram o problema e a alternativa escolhida: aviso inline em vez de modal, para não
bloquear a digitação.

## Decisão

- `aplicarExif()` apenas calcula sugestões em memória. Nenhum input é escrito antes da decisão.
- O aviso é inline, com `Aplicar nos campos vazios` e `Ignorar`, e não impede o preenchimento manual.
- `Aplicar` grava somente `data`, `periodo`, `cidade` e `estado` **quando estão vazios**. `local`
  nunca recebe valor do GPS.
- `Ignorar` não toca nenhum campo. A decisão vale pelo resto da sessão e somente a primeira foto com
  dados úteis é considerada.
- Edição (`?editar=`) e restauração de rascunho não disparam o aviso.
- Falha no geocódico inverso não derruba a sugestão de data da foto.

## Consequências

- A ADR 0012 passa a ser parcialmente substituída: a leitura de EXIF continua, mas o preenchimento
  deixa de ser automático.
- Como `data` e `periodo` já nascem com valores padrão, na prática a foto preenche cidade e UF. A
  regra aprovada continua sendo "somente campos vazios", registrada por teste.
- Nenhuma mudança de API, contrato OpenAPI ou projeção `LocalPublico`; rollback é um revert.
