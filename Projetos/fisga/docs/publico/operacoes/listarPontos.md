---
operacao: listarPontos
proposta: >
  Lista os pontos privados da conta, do mais recente para o mais antigo.
  Nenhum ponto de outra conta aparece.
acesso: sessao
verificar: >
  O corpo é uma lista de pontos com `id`, `nome`, `tipo`, `latitude`,
  `longitude` e as datas. Conta sem ponto recebe lista vazia.
relacionados: [criarPonto, listarTrilhas]
---
A ordem vem de `atualizadoEm`, não de `criadoEm`: editar um ponto antigo o move
para o topo da lista.
