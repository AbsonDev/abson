---
operacao: registrarConta
proposta: >
  Cria uma conta com e-mail e senha e envia um link de confirmação. A conta
  existe a partir da chamada, mas só entra depois que o e-mail é confirmado.
acesso: publico
corpo: |
  {"nome":"Marina","email":"marina@exemplo.com.br","senha":"senha-de-exemplo"}
verificar: >
  A resposta é `201`, o corpo traz `mensagem` e o header `Location` aponta para
  `/api/auth/me`. Chamar `obterSessao` em seguida ainda devolve
  `autenticado: false`, porque falta confirmar o e-mail.
relacionados: [confirmarEmail, entrarComSenha]
erros:
  - status: 400
    codigos: [email_obrigatorio, email_invalido, nome_longo, senha_curta, senha_longa, email_em_uso, falha_ao_criar_conta]
    quando: >
      O e-mail vem vazio, sem formato válido ou acima de 254 caracteres; o nome
      passa de 80 caracteres; a senha fica fora de 10 a 128 caracteres; ou o
      e-mail já tem conta. `falha_ao_criar_conta` indica recusa do provedor de
      identidade e pede nova tentativa.
---
O cadastro não distingue maiúsculas de minúsculas no e-mail: a conta é gravada em
minúsculas, e `Marina@…` e `marina@…` disputam o mesmo endereço.
