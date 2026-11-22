---
operacao: atualizarLocal
proposta: >
  Substitui os dados de um local salvo. Todos os campos do corpo são gravados, e
  o local continua no mesmo padrão a menos que `padrao: true` peça a troca.
acesso: dono
caminho:
  id: 00000000-0000-4000-8000-000000000001
corpo: |
  {"nome":"Represa de Guarapiranga","cidade":"São Paulo","estado":"SP","latitude":-23.75,"longitude":-46.72,"ambiente":"Represa"}
verificar: >
  O corpo traz o local atualizado com o mesmo `id` e `criadoEm`; `atualizadoEm`
  avança. O header não traz `Location` nesta rota.
relacionados: [listarLocais, definirLocalPadrao]
erros:
  - status: 400
    codigos: [local_sem_nome, local_nome_longo, local_cidade_longa, local_uf_invalida, local_latitude_invalida, local_longitude_invalida, local_ambiente_invalido]
    quando: >
      Nome vazio ou acima de 80 caracteres; cidade acima de 80; UF sem duas
      letras; coordenada fora da faixa; ou ambiente inválido.
  - status: 404
    codigos: [local_nao_encontrado]
    quando: >
      O `id` não corresponde a nenhum local. Local de outra conta responde `403`,
      não `404`.
---
Não há `limite_de_locais` aqui: o limite só é checado na criação. Atualizar nunca
aumenta o número de locais da conta.
