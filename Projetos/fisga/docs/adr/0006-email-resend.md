# ADR 0006 — E-mail transacional com Resend e console em desenvolvimento

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O link mágico precisa entregar e-mail em produção, mas o ambiente local não deve depender de rede,
credenciais ou domínio verificado. Também queremos evitar acoplar o domínio a um SDK proprietário.

## Decisão

- A aplicação define a porta `IEmailSender`.
- Em desenvolvimento, `ConsoleEmailSender` loga apenas metadados seguros (domínio, assunto,
  expiração) — nunca o link nem o token — e a API devolve o link em `linkDesenvolvimento`
  para o botão de teste na tela `/entrar`.
- Em produção, `ResendEmailSender` chama `POST https://api.resend.com/emails` com HTML e texto.
- O template HTML é da aplicação (`ModeloDeEmailDeAcesso`), com a identidade do Fisga.
- A API key fica apenas em variável de ambiente do servidor; nunca no repositório.

## Consequências

- O fluxo local é testável sem provedor.
- Trocar de provedor é implementar outra classe e ajustar a composição.
- Produção exige domínio verificado (MX/TXT/DKIM) e uma API key com permissão de envio.
- Sem domínio verificado, o Resend só entrega ao e-mail dono da conta; o login público fica
  bloqueado até a verificação.
