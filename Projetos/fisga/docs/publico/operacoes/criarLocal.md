---
operacao: criarLocal
proposta: >
  Salva um local de pesca na conta. O primeiro local criado vira o padrão
  automaticamente.
acesso: sessao
corpo: |
  {"nome":"Represa de Guarapiranga","cidade":"São Paulo","estado":"SP","latitude":-23.75,"longitude":-46.72,"ambiente":"Represa"}
verificar: >
  A resposta é `201` com o local completo, incluindo `id`, `padrao` e `criadoEm`;
  o header `Location` aponta para `/api/locais/{id}`. No primeiro local da conta,
  `padrao` vem `true`.
relacionados: [listarLocais, definirLocalPadrao, reverterCoordenadas]
erros:
  - status: 400
    codigos: [limite_de_locais, local_sem_nome, local_nome_longo, local_cidade_longa, local_uf_invalida, local_latitude_invalida, local_longitude_invalida, local_ambiente_invalido]
    quando: >
      A conta já tem cinco locais (`limite_de_locais`); o nome vem vazio ou passa
      de 80 caracteres; a cidade passa de 80; a UF não tem duas letras; a
      coordenada sai da faixa; ou o ambiente não é um valor válido.
---
O limite de cinco vale por conta e conta todos os locais, inclusive os que não
são padrão. `padrao: true` no corpo troca o padrão na mesma chamada.
