# ADR 0001 — Monólito modular com quatro contextos

**Status:** aceito · **Data:** 2026-11-15

## Contexto

O produto é um MVP de validação, com um único fluxo principal (calcular → salvar → PDF → comprar).
Há contextos com ritmos diferentes: precificação (core), catálogo (supporting), documentos
(supporting) e pagamentos (generic). Microsserviços aumentariam o custo operacional sem resolver
nenhum problema atual.

## Decisão

Um único deployable .NET, com projetos separados por camada e pastas por contexto de domínio.
Persistência em um banco Postgres, com schemas `precificacao` e `pagamentos`. A comunicação entre
contextos é por interfaces da aplicação e eventos em processo.

## Consequências

- Deploy e debug simples; transações locais.
- Fronteiras explícitas o suficiente para extrair um contexto se necessário.
- Disciplina manual para não importar `DbContext` de um contexto no outro; revisão de código cobre.
- Outbox e mensageria ficam como evolução, não como requisito do MVP.
