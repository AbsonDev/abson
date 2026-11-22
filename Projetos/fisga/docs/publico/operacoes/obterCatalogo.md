---
operacao: obterCatalogo
proposta: >
  Entrega as espécies, iscas, ambientes, períodos, fases de maré e de lua,
  climas e clarezas de água que o registro aceita, com uma pescaria de exemplo.
acesso: publico
verificar: >
  O corpo traz as listas `especies`, `iscas` e `ambientes`, além de `exemplo` com
  uma pescaria completa. Use `exemplo` como modelo para os campos da pescaria em
  `criarPescaria` (omita `capturas`, que são cadastradas em `atualizarPescaria`).
relacionados: [criarPescaria]
---
Os nomes de `especies` e `iscas` são sugestões de preenchimento: o campo do
registro aceita texto livre de até 80 caracteres, não apenas o que está aqui.
