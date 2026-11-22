---
operacao: criarPonto
proposta: >
  Salva um ponto privado no mapa da conta: ponto, trilha, poita, estrutura ou
  rampa.
acesso: sessao
corpo: |
  {"nome":"Poita do capim","descricao":"Poita na entrada do braço, com capim nas margens.","latitude":-23.7514,"longitude":-46.7209,"tipo":"Poita"}
verificar: >
  A resposta é `201` com o ponto completo e o header `Location` apontando para
  `/api/pontos/{id}`. O `id` devolvido é o que `atualizarPonto` e `excluirPonto`
  usam.
relacionados: [listarPontos, atualizarPonto]
erros:
  - status: 400
    codigos: [limite_de_pontos, ponto_sem_nome, ponto_nome_longo, ponto_descricao_longa, ponto_latitude_invalida, ponto_longitude_invalida, ponto_tipo_invalido]
    quando: >
      A conta já tem duzentos pontos; o nome vem vazio ou passa de 80 caracteres;
      a descrição passa de 1.000; a coordenada sai da faixa; ou o tipo não é um
      dos cinco válidos.
---
O limite é de duzentos pontos por conta. A descrição é obrigatória no contrato,
mas pode vir vazia; é nela que o pescador anota o que só ele precisa saber.
