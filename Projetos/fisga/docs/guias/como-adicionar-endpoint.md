# Como adicionar um endpoint

Passo a passo para uma rota nova na API do Fisga. O caminho é o mesmo que o
código já usa em `src/Fisga.Api/Endpoints/`.

## 1. Contrato na API

Crie (ou estenda) `src/Fisga.Api/Endpoints/<Area>Endpoints.cs` com um método de
extensão:

```csharp
public static IEndpointRouteBuilder Map<Area>Endpoints(this IEndpointRouteBuilder app)
{
    app.MapGet("/api/<rota>", (...) => ...);
    return app;
}
```

Registre em `src/Fisga.Api/Program.cs`, junto das chamadas existentes de
`app.Map<Area>Endpoints();`: nada mais é necessário para a rota aparecer no
contrato.

## 2. DTOs

Entrada e saída ficam em `src/Fisga.Api/Contracts/`. Reaproveite os tipos
existentes; não devolva entidade de domínio direto na resposta.

## 3. Regra de negócio e persistência

Regra em `src/Fisga.Application`; acesso a banco em `src/Fisga.Infrastructure`.
A API orquestra e traduz HTTP — não coloque regra no endpoint.

## 4. Autenticação e limites

- Rota com sessão: chame `http.ExigirUsuarioId()` no handler. Sem usuário, ele
  lança `NaoAutenticadoException` e o middleware responde `401 nao_autenticado`.
  O Fisga não usa `RequireAuthorization()` — o `SessaoIdentityMiddleware`
  valida a sessão de dispositivo a cada requisição `/api`.
- A rota só é privada porque o handler chama `http.ExigirUsuarioId()`. O app
  não tem `UseAuthorization`/`RequireAuthorization()`: `AllowAnonymous()` não
  muda quem pode chamar a rota. Esse metadata é lido pelo
  `ConsentimentoMiddleware`, para dispensar a rota do aceite de termos vigentes
  (sem o metadata, usuário com consentimento pendente recebe
  `403 consentimento_pendente`), e pelo `ContratoOperationTransformer`, que
  publica `security`: rota sem `AllowAnonymous()` sai como `cookieAuth` e rota
  com ele sai como anônima (`security: []`). Rota que nunca chama
  `ExigirUsuarioId()` fica pública mesmo sem esse metadata.
- Rota administrativa com token manual não é sessão nem rota pública: aplique
  `.WithMetadata(new RequerTokenAdmin())` para o contrato publicar
  `adminToken` em vez de `security: []`.
- Rate limiting com as políticas existentes em `Program.cs` (`AddRateLimiter`,
  linhas 102–112), via `.RequireRateLimiting("<politica>")`: `leitura`,
  `escrita`, `auth`, `pdf`, `midia`. `web` não é política de rate limit — é a
  política de CORS (`AddCors`/`UseCors("web")`). Usar `web` em
  `RequireRateLimiting` não retorna 429: a política não existe no rate limiter e
  a requisição falha com 500.

## 5. Cache

Cache é default-deny: sem metadata, a resposta sai com `private, no-store`.

- `CachePolicy.Public(segundos)` só para resposta pública e sem dado pessoal.
- `CachePolicy.Private(segundos)` para resposta por usuário.
- Não marque como público nada que dependa da sessão.

## 6. Testes

Testes de API em `tests/Fisga.Api.Tests`. Se o endpoint tocar banco, siga o
padrão de integração já usado na pasta (`TestWebApplicationFactory` e afins).

## 7. Contrato OpenAPI

Toda rota precisa de uma entrada no mapa congelado antes de o contrato ser
gerado. Sem ela, `gerar-openapi.sh` falha com `Operação sem operationId`.

1. Adicione a operação em `src/Fisga.Api/OpenApi/MapaDeOperacoes.cs` com um
   `operationId` estável, no formato `verboSubstantivo`.
2. Aplique `.WithTags(Tags.X)`, `.WithSummary` e `.WithDescription` no handler.
   Sem tag de domínio ou sem resumo de negócio, `ContratoTests` falha.
3. Declare as respostas: `.Produces<T>(status)` para o sucesso, `.ComErro(status)`
   para erro com corpo e `.SemCorpo(status)` para `204`/`302`/`429`/`503`.
   Upload usa `.Accepts<FormularioDeFoto>("multipart/form-data")`. O status
   declarado precisa ser o que o handler devolve — `ContratoTests` compara a
   tabela de `RespostasEsperadas` com o documento gerado.
4. Registre a operação em `tests/Fisga.Api.Tests/RespostasEsperadas.cs` com os
   status reais e, se a rota for pública, em `RotasAnonimasPorDecisao`
   (`ContratoTests.cs`). Um `404` com corpo vazio (em vez de `ErroResponse`)
   entra em `RespostasEsperadas.NotFoundSemCorpo`.
5. Regenere e commite o contrato publicado:

```bash
./scripts/gerar-openapi.sh
```

O resultado vai para `docs/api/openapi.json`. Detalhes em
[`docs/api/README.md`](../api/README.md).

## 8. Documentação

- Agregado, invariante ou contexto novo: atualize [`docs/ddd.md`](../ddd.md).
- Rota de produto (algo que o usuário usa): acrescente na seção `## API` do
  [`README.md`](../../README.md).
- Nada disso mudou? Só o contrato gerado já basta.
