---
operacao: solicitarLink
proposta: >
  Pede um link de acesso para um e-mail. Se houver conta ou não, a resposta é a
  mesma, e o link só nasce quando o e-mail tem formato válido.
acesso: publico
corpo: |
  {"email":"marina@exemplo.com.br"}
verificar: >
  O corpo traz `mensagem` genérica e o header `Location` aponta para
  `/api/auth/me`. Em desenvolvimento, `linkDesenvolvimento` vem preenchido; em
  produção, vem `null`.
relacionados: [confirmarLink, obterSessao]
erros:
  - status: 400
    codigos: [email_obrigatorio, email_invalido]
    quando: O e-mail vem vazio, passa de 254 caracteres ou não tem formato válido.
---
A mensagem é a mesma para e-mail cadastrado e não cadastrado, por privacidade. Um
novo pedido dentro do intervalo de reenvio devolve a mesma resposta sem gerar
outro link.
