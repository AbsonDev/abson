---
operacao: esquecerSenha
proposta: >
  Pede o envio de um link de redefinição de senha para um e-mail. O pedido tem a
  mesma resposta exista ou não conta cadastrada, e o `202` é o resultado normal.
acesso: publico
corpo: |
  {"email":"marina@exemplo.com.br"}
verificar: >
  A resposta é `202`, com `mensagem` genérica; o header `Location` aponta para
  `/api/auth/me`. Não há indicador de que a conta existe.
relacionados: [redefinirSenha, entrarComSenha]
erros:
  - status: 400
    codigos: []
    quando: >
      O corpo não pôde ser lido como JSON. E-mail vazio, inválido ou de conta
      inexistente ainda devolve `202`, sempre com a mesma mensagem.
---
A rota devolve `202` mesmo quando o e-mail vem vazio. Nesse caso o link não é
gerado; a resposta é indistinguível do caminho de sucesso.
