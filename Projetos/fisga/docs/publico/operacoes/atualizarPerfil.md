---
operacao: atualizarPerfil
proposta: >
  Altera o nome exibido na conta e a preferência por receber novidades. Devolve
  o usuário já com os valores novos.
acesso: sessao
corpo: |
  {"nome":"Marina","aceitaNovidades":false}
verificar: >
  O corpo é o usuário com `nome`, `nomeExibicao` e `aceitaNovidades` já
  atualizados. Nome `null` ou vazio é aceito e faz o `nomeExibicao` cair para a
  parte antes do `@` do e-mail.
relacionados: [obterSessao]
erros:
  - status: 400
    codigos: [nome_longo]
    quando: O nome passa de 80 caracteres.
---
`aceitaNovidades` sempre substitui a escolha anterior: `false` desliga o
recebimento mesmo que a conta já estivesse inscrita.
