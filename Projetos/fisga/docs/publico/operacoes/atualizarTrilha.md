---
operacao: atualizarTrilha
proposta: >
  Substitui o traçado e os dados de uma trilha privada. A lista de `pontos` é o
  traçado final, não um acréscimo ao que existia.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
corpo: |
  {"nome":"Costura da represa","descricao":"Costura pela margem do braço, em água turva.","tipo":"Corrico","pontos":[{"latitude":-23.7502,"longitude":-46.7205},{"latitude":-23.7511,"longitude":-46.7213}]}
verificar: >
  O corpo traz a trilha com `pontos` iguais aos enviados e `distanciaKm`
  recalculado. `atualizadoEm` avança e a trilha sobe para o topo de
  `listarTrilhas`.
relacionados: [listarTrilhas, excluirTrilha]
erros:
  - status: 400
    codigos: [trilha_sem_nome, trilha_nome_longa, trilha_descricao_longa, trilha_tipo_invalido, trilha_curta, trilha_longa, trilha_coordenada_invalida]
    quando: >
      Nome vazio ou acima de 80 caracteres; descrição acima de 1.000; tipo
      inválido; menos de 2 ou mais de 500 pontos; ou coordenada fora da faixa.
  - status: 404
    codigos: [trilha_nao_encontrada]
    quando: >
      O `id` não corresponde a nenhuma trilha. Trilha de outra conta responde
      `403`, não `404`.
---
Reenviar o mesmo traçado devolve `200` e não muda a distância. Não há
`limite_de_trilhas` aqui: o teto de cem só vale na criação.
