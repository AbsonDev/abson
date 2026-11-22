---
operacao: obterDiario
proposta: >
  Devolve o diário da conta: totais, recordes, espécies e iscas que mais
  aparecem, os destaques calculados (`insights`) e a lista de pescarias com o
  número de capturas de cada uma.
acesso: sessao
verificar: >
  O corpo traz os totais (`totalPescarias`, `totalCapturas`, `taxaDeSoltura`) e a
  lista `pescarias`, da data mais recente para a mais antiga. Conta nova recebe
  totais zerados e lista vazia, não `404`.
relacionados: [criarPescaria, obterCondicoes]
---
O diário é calculado a partir das pescarias gravadas e se atualiza sozinho a
cada uma. Não há como editá-lo diretamente; qualquer correção passa por
`atualizarPescaria`.
