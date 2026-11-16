# ADR 0001 — CMV alvo e preço mínimo no mesmo cálculo

**Status:** aceito · **Data:** 2026-11-16

## Contexto

No food service, o CMV (custo dos ingredientes ÷ preço) é a métrica mais usada para precificar.
Porém, um prato com CMV de 30% ainda pode não pagar embalagem, mão de obra, custo fixo, imposto e
comissão do iFood. Usar apenas CMV esconde prejuízo; usar apenas “custo + margem” ignora a
linguagem do setor.

## Decisão

Calcular três referências por canal:

1. **Preço por CMV alvo** = custo dos ingredientes por porção ÷ CMV alvo.
2. **Preço mínimo** = (custo total + taxa fixa) ÷ (1 − imposto − comissão).
3. **Preço sugerido** = maior entre 1 e 2, arredondado para cima.

O resultado expõe CMV real, margem de contribuição e lucro operacional, e sinaliza quando o preço
foi elevado ao mínimo (`AjustadoPeloCustoTotal`).

## Consequências

- O produto fala a língua do operador (CMV) sem esconder os custos que o CMV tradicional não cobre.
- O preço mínimo por canal evita vender no prejuízo em marketplaces.
- A fórmula é testável e independente de tabelas de taxas; o operador informa o contrato.
