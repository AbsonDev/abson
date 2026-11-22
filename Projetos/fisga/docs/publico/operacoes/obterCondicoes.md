---
operacao: obterCondicoes
proposta: >
  Reúne tempo, maré aproximada, lua, solunar e a melhor janela do dia para uma
  coordenada, com previsão para os próximos dias.
acesso: publico
consulta:
  lat: -23.55
  lon: -46.63
  nome: Represa de Guarapiranga
verificar: >
  O corpo traz `agora`, `tempo`, `solunar`, `linhaDoTempo` e `dias`. Quando o
  local não tem maré, `temMare` é `false`, `mare` vem `null` e `aviso` explica que
  a resposta mostra vento, chuva, lua e solunar.
relacionados: [compararCondicoes, reverterCoordenadas]
erros:
  - status: 400
    codigos: [local_sem_coordenadas, latitude_invalida, longitude_invalida]
    quando: >
      `lat` ou `lon` não vieram, ou a coordenada está fora da faixa: latitude de
      -90 a 90, longitude de -180 a 180. `NaN` também cai na faixa inválida.
---
`nome`, `cidade`, `estado` e `ambiente` são só rótulo: não mudam o cálculo, que
usa exclusivamente `lat` e `lon`. A resposta fica em cache privado por cinco
minutos.
