---
operacao: confirmarEmail
proposta: >
  Marca o e-mail de uma conta como confirmado, a partir do identificador e do
  token que chegaram no link de confirmação.
acesso: publico
corpo: |
  {"uid":"00000000-0000-4000-8000-000000000003","token":"tokenDeExemplo"}
verificar: >
  O corpo traz `mensagem` dizendo que o e-mail foi confirmado. A partir daí
  `entrarComSenha` passa a abrir sessão para essa conta.
relacionados: [registrarConta, entrarComSenha]
erros:
  - status: 400
    codigos: [confirmacao_invalida]
    quando: >
      O `uid` não é um UUID, a conta não existe ou o token não corresponde. O
      mesmo código cobre os três casos, de propósito.
---
O link vale um dia. Confirmar duas vezes com o mesmo link dentro do prazo
devolve `200` nas duas: o token não é consumido. Trocar ou redefinir a senha,
assim como encerrar todas as sessões, invalida os links emitidos antes.
