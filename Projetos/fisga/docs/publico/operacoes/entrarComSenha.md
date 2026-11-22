---
operacao: entrarComSenha
proposta: >
  Tenta abrir uma sessão com e-mail e senha. Quando dá certo, grava o cookie
  `fisga_auth`; quando não, responde `200` com `autenticado: false` e o motivo
  numa mensagem.
acesso: publico
corpo: |
  {"email":"marina@exemplo.com.br","senha":"senha-de-exemplo"}
verificar: >
  `autenticado: true` com `mensagem: null` indica sucesso. O cookie de sessão vai
  no `Set-Cookie` da resposta; salve-o com `curl -c cookies.txt` para as chamadas
  seguintes.
relacionados: [obterSessao, sair]
erros:
  - status: 400
    codigos: []
    quando: >
      O corpo não pôde ser lido como JSON — por exemplo, com um tipo errado no
      campo `email` ou JSON malformado. Credencial errada não é `400`: é `200`
      com `autenticado: false`.
---
Senha errada, conta inexistente, e-mail não confirmado e bloqueio por tentativas
compartilham o mesmo status `200`. A mensagem separa bloqueio e e-mail não
confirmado; senha errada e conta inexistente recebem o mesmo texto.
