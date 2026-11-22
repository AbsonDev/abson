---
operacao: exportarConta
proposta: >
  Gera um arquivo JSON com os dados da conta e todas as pescarias registradas,
  incluindo as capturas, para download.
acesso: sessao
verificar: >
  A resposta é `200` com `Content-Disposition` de anexo e o nome
  `fisga-conta-AAAAMMDD.json`. O conteúdo traz `exportadoEm`, `usuario` e a lista
  `pescarias`.
relacionados: [excluirConta, listarSessoes]
---
A exportação mostra os dados como o dono os vê, não a versão pública: local
exato, observações e capturas vêm completos.
