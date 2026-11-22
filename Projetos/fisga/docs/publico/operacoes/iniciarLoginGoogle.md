---
operacao: iniciarLoginGoogle
proposta: >
  Inicia o login com Google e manda o navegador para a tela de consentimento do
  provedor.
acesso: publico
verificar: >
  O `302` traz `Location` para o Google. Sem credenciais configuradas, a resposta
  é `503` com `codigo` `google_indisponivel` e `detalhe` explicando que o login
  não está configurado.
relacionados: [concluirLoginGoogle, googleDisponivel]
erros:
  - status: 503
    codigos: [google_indisponivel]
    quando: As credenciais do Google não estão configuradas neste ambiente.
---
A rota não autentica ninguém sozinha. A sessão só nasce em `concluirLoginGoogle`,
quando o Google devolve o navegador.
