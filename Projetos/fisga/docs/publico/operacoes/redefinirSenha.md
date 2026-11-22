---
operacao: redefinirSenha
proposta: >
  Troca a senha de uma conta a partir do link de redefinição. Ao concluir, toda
  sessão aberta da conta é revogada.
acesso: publico
corpo: |
  {"uid":"00000000-0000-4000-8000-000000000003","token":"tokenDeExemplo","senha":"nova-senha-de-exemplo"}
verificar: >
  O corpo traz `mensagem` com a senha redefinida. As chamadas seguintes precisam
  de `entrarComSenha` com a senha nova; os cookies antigos não autenticam mais.
relacionados: [esquecerSenha, entrarComSenha]
erros:
  - status: 400
    codigos: [link_invalido, senha_curta, senha_longa]
    quando: >
      O `uid` não é um UUID, a conta não existe ou o token foi recusado
      (`link_invalido`); ou a senha nova fica fora de 10 a 128 caracteres.
---
Quando o `uid` aponta para uma conta existente, a validação de tamanho da
senha roda antes do token criptográfico ser conferido: uma senha curta devolve
`senha_curta` mesmo que o token tenha expirado ou sido revogado.
