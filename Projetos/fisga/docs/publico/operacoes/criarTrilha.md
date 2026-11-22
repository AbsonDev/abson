---
operacao: criarTrilha
proposta: >
  Salva uma trilha privada com o traçado completo de um corrico ou de um
  deslocamento no mapa.
acesso: sessao
corpo: |
  {"nome":"Costura da represa","descricao":"Costura pela margem do braço, em água turva.","tipo":"Corrico","pontos":[{"latitude":-23.7502,"longitude":-46.7205},{"latitude":-23.7511,"longitude":-46.7213}]}
verificar: >
  A resposta é `201` com a trilha completa, `distanciaKm` calculada e o header
  `Location` apontando para `/api/trilhas/{id}`.
relacionados: [listarTrilhas, atualizarTrilha]
erros:
  - status: 400
    codigos: [limite_de_trilhas, trilha_sem_nome, trilha_nome_longa, trilha_descricao_longa, trilha_tipo_invalido, trilha_curta, trilha_longa, trilha_coordenada_invalida]
    quando: >
      A conta já tem cem trilhas; o nome vem vazio ou passa de 80 caracteres; a
      descrição passa de 1.000; o tipo não é `Corrico` nem `Deslocamento`; a lista
      tem menos de 2 ou mais de 500 pontos; ou uma coordenada sai da faixa.
---
Um ponto só não é trilha: `trilha_curta` aparece com zero ou um ponto. O
`distanciaKm` é calculado pelo servidor, então não precisa ser enviado.
