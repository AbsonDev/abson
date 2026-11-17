# ADR 0009 — Newsletter mensal com descadastro assinado

**Status:** aceito · **Data:** 2026-11-17

## Contexto

O onboarding e a conta ofereciam receber dicas por e-mail, mas não existia envio. A wiki passou a
fornecer conteúdo sazonal suficiente para um resumo mensal.

## Decisão

- `ServicoDeNewsletter` gera um digest personalizado com estatísticas do diário e três leituras da
  wiki, além de um lembrete de defeso.
- Só usuários com `AceitaNovidades = true` recebem.
- O descadastro usa um token HMAC (`usuarioId.assinatura`), sem tabela extra; o endpoint público
  desativa a preferência.
- O envio é disparado por um endpoint administrativo protegido por `X-Admin-Token`; há uma prévia
  HTML para revisão.
- O agendamento mensal é um cron do servidor chamando o endpoint; a aplicação não roda scheduler.
- O e-mail inclui `List-Unsubscribe` e link visível de descadastro.

## Consequências

- A promessa de newsletter passa a ter entrega real.
- O token não precisa ser armazenado; trocar o segredo invalida links antigos.
- Não há idempotência por mês: o cron deve rodar uma vez. Se o volume crescer, adicionar uma tabela
  de envios e paginação por destinatário.
- O Resend precisa do domínio verificado e de uma API key de envio.
