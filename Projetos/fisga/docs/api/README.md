# Contrato da API

`openapi.json` nesta pasta é o contrato publicado da API do Fisga. Ele é
**gerado** a partir do código (`Microsoft.AspNetCore.OpenApi`) e versionado no
repositório para que clientes, revisores e agentes leiam o mesmo documento.
Não edite o JSON à mão: a próxima geração sobrescreve.

## Documentação da API

O programa de documentação pública tem quatro documentos de apoio, todos
anteriores ao portal:

- [Inventário](inventario.md) — uma linha por operação: acesso, rate limit,
  cache, status real e erros, mais as lacunas do contrato.
- [Política editorial](politica-editorial.md) — template obrigatório de página,
  voz, exemplos e checklist de revisão.
- [Arquitetura do portal](portal.md) — mapa de URL, gerador, build, hospedagem e
  os dois jobs novos de CI/CD.
- [Backlog](backlog.md) — fases F1 a F4 com escopo, aceite e estimativa.

A decisão de publicar em `docs.fisga.abson.tech` está na
[ADR 0017](../adr/0017-portal-publico-da-api.md). A referência pública é gerada
deste `openapi.json`; por isso a fase F1 (correção das
[lacunas do inventário](inventario.md#lacunas-de-contrato)) vem antes da
publicação.

## Regenerar

Na raiz de `Projetos/fisga`:

```bash
./scripts/gerar-openapi.sh
```

O script builda a API, extrai o documento e escreve `docs/api/openapi.json`.
Commite o arquivo gerado no mesmo PR da mudança de contrato.

## De onde vem o documento

- Rotas registradas em `src/Fisga.Api/Endpoints/` e os dois endpoints de saúde
  em `src/Fisga.Api/Program.cs`.
- DTOs de entrada e saída em `src/Fisga.Api/Contracts/`.
- A contagem de paths e schemas muda a cada mudança de contrato e não é
  mantida à mão aqui: consulte o próprio `openapi.json`, que é a fonte da
  verdade.

## Limites conhecidos

- **Só existe em Development.** O `MapOpenApi()` fica dentro de
  `if (app.Environment.IsDevelopment())`, então `/openapi/v1.json` responde
  apenas no ambiente local. Em produção o documento não é exposto; a fonte é o
  `openapi.json` versionado aqui.
- **O contrato já expressa acesso, cache e rate limit; falta o resto.** Toda
  operação declara `security` — vazio quando a rota é anônima — e `x-cache`; as
  que têm política declaram também `x-rate-limit`. Ainda faltam os status reais,
  o schema de erro, o schema de upload e `info`/`servers`: o
  [inventário](inventario.md#lacunas-de-contrato) lista o que resta.
- **O contrato não traz schemas de resposta.** Os endpoints devolvem
  `Results.Ok(...)` sem metadados `Produces<T>`, então nenhuma operação declara
  o corpo tipado da resposta. Quem gera cliente a partir de `openapi.json` vê os
  corpos de entrada, mas não os de saída. Adicionar
  `Produces<T>` aos endpoints é um follow-up.
