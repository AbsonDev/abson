---
operacao: reverterCoordenadas
proposta: >
  Converte latitude e longitude no município brasileiro correspondente, para
  nomear um local a partir do ponto no mapa.
acesso: publico
consulta:
  lat: -23.55
  lon: -46.63
verificar: >
  O corpo traz `nome`, `cidade`, `estado`, `pais` e as coordenadas informadas. O
  `404` sai sem corpo quando o ponto não cai em município brasileiro reconhecido.
relacionados: [buscarLocais, criarLocal]
erros:
  - status: 400
    codigos: [coordenadas_invalidas]
    quando: >
      `lat` ou `lon` não vieram, são `NaN`, ou a coordenada está fora da faixa:
      latitude de -90 a 90, longitude de -180 a 180.
---
A checagem de faixa acontece no handler, antes de qualquer chamada externa. Uma
falha do serviço de geocodificação também termina em `404`, não em `500`.
