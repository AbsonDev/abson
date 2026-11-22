---
operacao: previsualizarNewsletter
proposta: >
  Renderiza o HTML da newsletter que um assinante receberia no mês pedido, sem
  enviar nada.
acesso: admin
consulta:
  email: marina@exemplo.com.br
  mes: "2026-11"
verificar: >
  A resposta é `200` com `Content-Type: text/html` e o corpo é a mensagem
  pronta. Sem `mes`, vale o mês atual; `mes` em formato inválido também cai no mês
  atual.
relacionados: [enviarNewsletter]
---
`404` sem corpo cobre três casos: token inválido, e-mail ausente ou inválido e
e-mail sem conta. O `404` é proposital — não confirma quem está inscrito.
