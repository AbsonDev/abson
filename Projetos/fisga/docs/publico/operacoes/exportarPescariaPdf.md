---
operacao: exportarPescariaPdf
proposta: >
  Gera o PDF de uma pescaria compartilhada, pronto para baixar. O arquivo leva o
  mesmo conteúdo da página pública e um código QR do link.
acesso: capacidade
caminho:
  shareToken: tokenDeExemplo
verificar: >
  A resposta é `200` com `Content-Type: application/pdf` e
  `Content-Disposition` de anexo com nome `fisga-XXXXXXXX.pdf`. O `XXXXXXXX` são
  os oito primeiros caracteres de `shareToken`.
relacionados: [obterPescariaPorToken]
erros:
  - status: 404
    codigos: [pescaria_nao_encontrada]
    quando: O `shareToken` não corresponde a nenhuma pescaria.
---
A exportação usa a versão pública do local, não a privada: o PDF tem o mesmo
nível de detalhe que qualquer visitante do link vê.
