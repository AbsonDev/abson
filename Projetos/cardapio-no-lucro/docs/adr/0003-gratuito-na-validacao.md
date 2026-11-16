# ADR 0003 — Produto gratuito durante a validação

**Status:** aceito · **Data:** 2026-11-16

## Contexto

A monetização prevista é R$ 29–49/mês por estabelecimento. Antes de cobrar, precisamos validar se o
nicho usa a ficha, entende o preço por canal e volta para atualizar custos.

## Decisão

- Não implementar checkout/assinatura no MVP.
- Todos os recursos, incluindo PDF sem marca d’água, são gratuitos.
- Não pedir cartão nem e-mail para calcular; manter os mesmos tokens de compartilhamento/edição.
- O contexto de assinatura será separado e plugado quando houver tração.

## Consequências

- Menor atrito e foco absoluto no fluxo de valor.
- O código não tem dependência de gateway, reduzindo superfície de risco.
- A futura cobrança exigirá plano, entitlement, gateway e webhooks — preferencialmente reaproveitando
  as decisões do Precifica.
