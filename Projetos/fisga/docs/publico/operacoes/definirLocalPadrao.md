---
operacao: definirLocalPadrao
proposta: >
  Marca um local como padrão e desmarca o que era antes. Devolve o local
  promovido.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
verificar: >
  O corpo traz o local com `padrao: true`. Em `listarLocais`, ele passa a ser o
  primeiro item e nenhum outro vem com `padrao: true`.
relacionados: [listarLocais, criarLocal]
erros:
  - status: 404
    codigos: [local_nao_encontrado]
    quando: >
      O `id` não corresponde a nenhum local. Local de outra conta responde `403`,
      não `404`.
---
Chamar no local que já é padrão devolve `200` e só reescreve `atualizadoEm`. O
local que era padrão perde a marca na mesma chamada: nunca fica mais de um
`padrao: true` na conta.
