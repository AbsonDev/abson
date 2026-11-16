# ADR 0002 — Dinheiro em centavos e percentual em pontos-base

**Status:** aceito · **Data:** 2026-11-15

## Contexto

Preço envolve multiplicação por percentuais fracionários (4,99%), divisão por `(1 − margem −
impostos)` e arredondamento. `double` produz erros; `decimal` é melhor, mas ainda permite centavos
fracionários e comparações frágeis ao persistir.

## Decisão

- `Dinheiro` é um value object com `long Centavos`.
- `Percentual` é um value object com `int PontosBase` (1% = 100 bps).
- Multiplicações e divisões arredondam para o centavo com `MidpointRounding.AwayFromZero`.
- O preço final é arredondado para cima (nunca para baixo) e depois a margem efetiva é recalculada.

## Consequências

- Aritmética determinística e testável; persistência simples (`bigint`/`int`).
- Nenhum `float`/`double` em regra de negócio.
- A UI converte reais/percentuais na borda e a API expõe decimais somente para exibição.
