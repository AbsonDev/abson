---
operacao: listarTrilhas
proposta: >
  Lista as trilhas privadas da conta, da mais recente para a mais antiga, com o
  traçado completo e a distância calculada.
acesso: sessao
verificar: >
  O corpo é uma lista de trilhas com `id`, `nome`, `tipo`, `pontos` e
  `distanciaKm`. Conta sem trilha recebe lista vazia.
relacionados: [criarTrilha, listarPontos]
---
Cada item traz todas as coordenadas, não só o começo e o fim. Trilha longa pesa
na resposta; a ordem é por `atualizadoEm`.
