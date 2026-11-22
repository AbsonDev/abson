---
operacao: atualizarPonto
proposta: >
  Substitui nome, descrição, coordenada e tipo de um ponto privado. O ponto
  continua sendo o mesmo, com `id` e `criadoEm` preservados.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
corpo: |
  {"nome":"Poita do capim","descricao":"Poita na entrada do braço, com capim nas margens.","latitude":-23.7514,"longitude":-46.7209,"tipo":"Poita"}
verificar: >
  O corpo traz o ponto com `id` inalterado e `atualizadoEm` mais recente. Em
  `listarPontos`, ele sobe para o topo da lista.
relacionados: [listarPontos, excluirPonto]
erros:
  - status: 400
    codigos: [ponto_sem_nome, ponto_nome_longo, ponto_descricao_longa, ponto_latitude_invalida, ponto_longitude_invalida, ponto_tipo_invalido]
    quando: >
      Nome vazio ou acima de 80 caracteres; descrição acima de 1.000; coordenada
      fora da faixa; ou tipo fora dos cinco válidos.
  - status: 404
    codigos: [ponto_nao_encontrado]
    quando: >
      O `id` não corresponde a nenhum ponto. Ponto de outra conta responde `403`,
      não `404`.
---
Não há `limite_de_pontos` nesta rota: o teto só é checado em `criarPonto`. Trocar
o tipo de um ponto não cria outro ponto.
