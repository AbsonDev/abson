---
operacao: descadastrarNewsletter
proposta: >
  Descadastra o assinante a partir do token do link que veio no e-mail e devolve
  o navegador para a página de confirmação do site.
acesso: capacidade
consulta:
  token: tokenDeExemplo
verificar: >
  O `302` traz `Location` para `/newsletter/descadastrado?ok=1` quando o token é
  válido e para `?ok=0` quando não é. A rota responde com redirect em todos os
  casos, nunca com `4xx`.
relacionados: [atualizarPerfil]
---
O token é assinado e identifica o assinante; não há confirmação em duas etapas.
Token adulterado, sem conta por trás ou com segredo de assinatura diferente termina
em `ok=0`.
