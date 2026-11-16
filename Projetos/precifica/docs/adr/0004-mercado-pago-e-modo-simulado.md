# ADR 0004 — Mercado Pago com modo simulado

**Status:** aceito · **Data:** 2026-11-15

## Contexto

O público é brasileiro e usa Pix/cartão. O MVP precisa validar a compra sem exigir credenciais e
webhooks públicos em cada ambiente de desenvolvimento. O gateway não pode contaminar o domínio.

## Decisão

- Porta `IGatewayDePagamento` na aplicação; adaptador `MercadoPagoGateway` na infraestrutura.
- Compra avulsa via **Checkout Pro** (`/checkout/preferences`), com `external_reference = pedido.Id`.
- Assinatura via **preapproval** (`/preapproval`), com `external_reference = assinatura.Id`.
- Webhook valida `x-signature` (HMAC SHA-256) quando `MercadoPago:WebhookSecret` está configurado.
- Em Development, `Pagamentos:Modo=Simulado` registra `GatewaySimulado`, que devolve uma URL local e
  expõe endpoints de confirmação. Em Production o modo simulado é rejeitado.
- O processamento é idempotente e sempre responde 200 para eventos reconhecidos/ignorados (o gateway
  reenvia notificações).

## Consequências

- Desenvolvimento e testes E2E funcionam offline.
- A produção exige configuração explícita de token, segredo e URL pública de webhook.
- Renovação recorrente é responsabilidade do Mercado Pago; o MVP reage ao `preapproval` e não faz
  reconciliação ativa.
