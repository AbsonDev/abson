---
operacao: obterPescariaPorToken
proposta: >
  Abre uma pescaria pelo token de compartilhamento. Quem tem o link vê a versão
  pública; o dono vê a mesma pescaria com `podeEditar: true`.
acesso: capacidade
caminho:
  shareToken: tokenDeExemplo
verificar: >
  O corpo traz `entrada` com o local como ele pode ser divulgado, a lista
  `capturas`, `criadoEm` e `podeEditar`. `podeEditar` só é `true` quando a sessão
  do dono acompanha a chamada.
relacionados: [exportarPescariaPdf, obterPermissaoPescaria, atualizarPescaria]
erros:
  - status: 404
    codigos: [pescaria_nao_encontrada]
    quando: O `shareToken` não corresponde a nenhuma pescaria.
---
O local público depende de `privacidadeDoLocal`: em `Cidade` aparece cidade e UF;
em `Oculto`, "Local não divulgado". O `shareToken` é a única credencial — quem tem
o link vê a pescaria.
