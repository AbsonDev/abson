---
operacao: buscarLocais
proposta: >
  Procura municípios brasileiros pelo nome e devolve as coordenadas de cada
  resultado, para o usuário escolher um local sem digitar latitude.
acesso: publico
consulta:
  q: Guarapiranga
verificar: >
  O corpo é uma lista com `nome`, `cidade`, `estado`, `latitude`, `longitude` e
  `pais`. Busca sem resultado devolve lista vazia com `200`, não `404`.
relacionados: [reverterCoordenadas, criarLocal]
---
Termo com menos de dois caracteres devolve lista vazia sem consultar o provedor,
e a checagem roda de novo depois de remover acentos: o termo "á" sozinho fica
com um caractere e não vira busca.
