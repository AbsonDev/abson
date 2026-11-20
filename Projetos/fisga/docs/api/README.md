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
- Metadados do contrato em `src/Fisga.Api/OpenApi/`: `operationId` congelado em
  `MapaDeOperacoes.cs`, tags em `Tags.cs` e `security`, `x-cache` e
  `x-rate-limit` derivados por `ContratoOperationTransformer.cs`.
- Respostas declaradas no handler com `Produces<T>` (sucesso) e
  `ComErro`/`SemCorpo` (erro com corpo e sem corpo), e normalizadas por
  `ContratoOperationTransformer.cs` — media types, headers
  `Location`/`Retry-After` e descrição por status.
- DTOs de entrada e saída em `src/Fisga.Api/Contracts/`.
- A contagem de paths e schemas muda a cada mudança de contrato e não é
  mantida à mão aqui: consulte o próprio `openapi.json`, que é a fonte da
  verdade.

## Limites conhecidos

- **Só existe em Development.** O `MapOpenApi()` fica dentro de
  `if (app.Environment.IsDevelopment())`, então `/openapi/v1.json` responde
  apenas no ambiente local. Em produção o documento não é exposto; a fonte é o
  `openapi.json` versionado aqui.
- **O contrato expressa respostas e erros; falta a identidade do documento.**
  Cada operação declara o status real de sucesso com corpo tipado, as respostas
  de erro apontam para o schema único `ErroResponse` em `application/json` e
  `application/problem+json`, os dois uploads descrevem
  `multipart/form-data`, o `429` traz `Retry-After` e `201`/`202` trazem
  `Location`. Acesso (`security`), cache (`x-cache`) e rate limit
  (`x-rate-limit`) também saem do código. Falta só `info`/`servers` (F1.7): o
  documento não tem `contact`, `license`, descrição nem a lista fixa de
  servidores.
