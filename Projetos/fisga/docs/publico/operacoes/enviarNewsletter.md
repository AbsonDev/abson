---
operacao: enviarNewsletter
proposta: >
  Dispara a newsletter do mês para os assinantes que aceitam novidades, ou
  percorre o envio em modo de simulação sem mandar e-mail.
acesso: admin
verificar: >
  O corpo traz `mes`, `elegiveis`, `enviados`, `falhas`, `simulacao` e `erros`.
  Com `simulacao: true`, `enviados` fica `0` e `elegiveis` mostra quem receberia.
relacionados: [previsualizarNewsletter, atualizarPerfil]
---
O mês e o modo de simulação vêm na URL, nos parâmetros `mes` e `dryRun`, mas o
contrato ainda não declara esses parâmetros: monte a URL à mão. Token inválido
ou ausente devolve `404` sem corpo, e a resposta não revela se o token existe.
