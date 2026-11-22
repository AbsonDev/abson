---
operacao: compararCondicoes
proposta: >
  Compara as condições atuais de uma coordenada com as pescarias da conta e
  aponta o registro mais parecido, ou avisa quantos registros faltam.
acesso: sessao
consulta:
  lat: -23.55
  lon: -46.63
  nome: Represa de Guarapiranga
verificar: >
  O corpo traz `temDados`, `titulo`, `detalhe` e `pescariasConsideradas`. Com
  menos de três pescarias com captura, `temDados` é `false` e `detalhe` diz
  quantas faltam. Com dados, vem `pescariaId`, `shareToken` e `fatores`.
relacionados: [obterCondicoes, obterDiario]
erros:
  - status: 400
    codigos: [local_sem_coordenadas, latitude_invalida, longitude_invalida]
    quando: >
      `lat` ou `lon` não vieram, ou a coordenada está fora da faixa válida.
---
A comparação pontua lua, maré, clima e período e só considera a pescaria que
somar pelo menos três pontos, o que a lua sozinha já alcança. `temDados: false`
é resposta normal de conta nova, não erro.
