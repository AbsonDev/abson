---
operacao: obterPermissaoPescaria
proposta: >
  Diz se quem está chamando pode editar uma pescaria compartilhada. Devolve o
  identificador da pescaria e a permissão, sem o conteúdo.
acesso: capacidade
caminho:
  shareToken: tokenDeExemplo
verificar: >
  O corpo traz `pescariaId` e `podeEditar`. Com o cookie do dono, `podeEditar` é
  `true`; sem sessão ou com sessão de outra conta, é `false`.
relacionados: [obterPescariaPorToken, atualizarPescaria]
erros:
  - status: 404
    codigos: [pescaria_nao_encontrada]
    quando: O `shareToken` não corresponde a nenhuma pescaria.
---
É a chamada leve para a página decidir se mostra o botão de editar, sem carregar
a pescaria inteira. `false` não é erro: o visitante só está vendo.
