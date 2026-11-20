# F1/A2 — respostas reais, erros únicos e uploads: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** fazer o `docs/api/openapi.json` declarar, nas 54 operações, os status que o código realmente devolve, um único schema de erro (`ErroResponse`) com o media type certo em cada origem, os uploads como `multipart/form-data` com o campo `arquivo`, e `Retry-After` no `429`.

**Architecture:** o status de sucesso entra como metadata `Produces<T>` na própria declaração do endpoint. Erros usam dois helpers (`ComErro`, `SemCorpo`) que publicam `ErroResponse`; um `IOpenApiOperationTransformer` normaliza as respostas geradas — espelha o schema de erro em `application/json` e `application/problem+json`, escreve a descrição de cada status e adiciona os headers `Location` (`201`/`202`) e `Retry-After` (`429`). O rate limit ganha uma fonte única de políticas/defaults em `PoliticasDeTaxa`, usada por `Program.cs`, pelo transformer e pelos testes.

**Tech Stack:** ASP.NET Core 10 Minimal APIs, `Microsoft.AspNetCore.OpenApi` 10.0.15 (Microsoft.OpenApi 2.13), xUnit, `WebApplicationFactory`.

**Spec:** `Projetos/fisga/docs/superpowers/specs/2026-11-20-f1-contrato-verdadeiro-design.md`

**Issues:** #64 (F1.2), #65 (F1.3), #66 (F1.5), #69 (F1.8) — as quatro fecham nesta PR.

**Antecessor:** `docs/superpowers/plans/2026-11-20-f1-a1-contrato-identidade.md` (A1, já mergeado em `136e65c`).

## Global Constraints

- **Nenhuma mudança de comportamento**, exceto `Retry-After` no `429` (aditivo). Status, corpo e media type de cada resposta continuam exatamente como hoje; `Location` já é emitido por `Results.Created`/`Results.Accepted` e aqui só passa a ser **documentado**.
- `openapi.json` é artefato: gerado por `scripts/gerar-openapi.sh`, nunca editado à mão.
- Tudo em português do Brasil: descrições, códigos e comentários novos.
- Contagem fixa: **54 operações**, 13 tags, 5 políticas (`leitura`, `escrita`, `auth`, `pdf`, `midia`).
- Build/teste na raiz de `Projetos/fisga`, com `export PATH="$HOME/.dotnet:$PATH"`.
- O portão de cada task é o teste da própria área: as theories rodam por tag e acumulam violações; a lista restante pertence às outras tasks.
- Não tocar em `docs/ux-audit.md` (snapshot datado) nem em arquivos fora de `Projetos/fisga/**`.

## Decisões que o plano fixa

1. **`ProducesProblem` não é usado.** Ele publica o schema `ProblemDetails` do framework, mas o corpo real de negócio é `ErroResponse` (`codigo`, `detalhe`). Usar `ProducesProblem` faria o contrato mentir sobre o corpo. A distinção "qual é qual" fica na descrição do `400`: regra de negócio devolve `ErroResponse`; corpo JSON malformado devolve `ProblemDetails` do framework.
2. **Erro de negócio é publicado em dois media types.** Handlers devolvem `Results.BadRequest(new ErroResponse(...))` em `application/json`; o `ExceptionHandlingMiddleware`, o `ConsentimentoMiddleware` e o `ProtecaoCsrfMiddleware` escrevem `application/problem+json`. O transformer espelha o mesmo schema nos dois — o formato é o mesmo, só o media type diverge.
3. **Status sem corpo são explícitos:** `204`, `302`, `429`, o `404` de `/api/locais/reverso`, `/api/newsletter/enviar` e `/api/newsletter/preview` e o `503` de `/ready`.
4. **`Location` em `201` e `202`.** `Results.Created` e `Results.Accepted` já escrevem o header; o transformer adiciona o header a qualquer resposta `201`/`202` do documento.
5. **Defaults de rate limit em um único lugar:** `PoliticasDeTaxa` (antes duplicado em `Program.cs`, no transformer e em `ContratoTests`).
6. **A invariante de segurança passa a ser intenção, não consistência.** `ContratoTests` deixa de derivar o esperado de `IAllowAnonymous` (a mesma metadata que o transformer lê) e passa a comparar com o conjunto curado das 23 rotas anônimas.

## Review Focus

1. **Status documentado ≠ status devolvido.** O contrato é gerado do código, mas os status são metadata escrita à mão: um `Produces` a mais publica um erro que nunca acontece; um a menos esconde um erro real. Conferir cada linha contra o handler (`Results.*`) e contra `inventario.md`.
2. **`Produces` com o DTO errado.** Cliente gerado recebe o tipo errado. Teste cobre os quatro `POST` de criação e a presença de schema em todo 2xx JSON.
3. **Erro publicado sem o schema único.** Qualquer 4xx/5xx com corpo que não seja `ErroResponse` nos dois media types quebra a promessa do #65. Teste: schema por status + catálogo.
4. **`Retry-After` só no teste.** Se o `OnRejected` for ligado apenas na factory de teste, produção continua sem header. Teste lê `IOptions<RateLimiterOptions>.Value.OnRejected` do host e invoca o callback.
5. **Multipart sem o nome do campo.** Sem `arquivo` no schema, o exemplo do portal fica errado. Teste: `requestBody` → `properties.arquivo` binário.
6. **Default de rate limit voltar a ser duplicado.** Se `Program.cs` voltar a passar inteiro literal, contrato e comportamento divergem silenciosamente. Teste lê `PoliticasDeTaxa.PadroesPorMinuto` e o loop de `Program.cs` só conhece os nomes.

---

### Task 1: Fonte única do rate limit e `Retry-After` no `429` (#69)

**Files:**
- Create: `src/Fisga.Api/RateLimiting/PoliticasDeTaxa.cs`
- Create: `src/Fisga.Api/RateLimiting/RejeicaoDoRateLimiter.cs`
- Modify: `src/Fisga.Api/Program.cs` (`AddRateLimiter`, `AdicionarPolitica`)
- Modify: `src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs` (usar `PoliticasDeTaxa`)
- Modify: `tests/Fisga.Api.Tests/TestWebApplicationFactory.cs` (usar `RejeicaoDoRateLimiter.Configurar`)
- Modify: `tests/Fisga.Api.Tests/ContratoTests.cs` (remover a cópia dos defaults)
- Create: `tests/Fisga.Api.Tests/RateLimitTests.cs`

**Interfaces:**
- Produces:
  - `Fisga.Api.RateLimiting.PoliticasDeTaxa` com `Leitura`, `Escrita`, `Auth`, `Pdf`, `Midia` (`const string`), `JanelaEmSegundos` (`const int = 60`), `PadroesPorMinuto` (`IReadOnlyDictionary<string,int>`) e `LimiteDe(IConfiguration, string)`.
  - `Fisga.Api.RateLimiting.RejeicaoDoRateLimiter` com `Configurar(RateLimiterOptions)` e `EscreverAsync(OnRejectedContext, CancellationToken)`.
- Consumes: `Microsoft.AspNetCore.RateLimiting`, `System.Threading.RateLimiting`.

- [x] **Step 1: Criar `PoliticasDeTaxa`**

```csharp
using Microsoft.Extensions.Configuration;

namespace Fisga.Api.RateLimiting;

/// <summary>
/// Fonte única das políticas de rate limit: nomes e limites padrão por minuto.
/// A configuração (<c>RateLimiting:&lt;política&gt;</c>) vence o padrão; o contrato
/// publicado e os testes leem daqui para não divergirem do comportamento.
/// </summary>
public static class PoliticasDeTaxa
{
    public const string Leitura = "leitura";
    public const string Escrita = "escrita";
    public const string Auth = "auth";
    public const string Pdf = "pdf";
    public const string Midia = "midia";

    // Janela fixa de um minuto (FixedWindowRateLimiterOptions.Window).
    public const int JanelaEmSegundos = 60;

    public static IReadOnlyDictionary<string, int> PadroesPorMinuto { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [Leitura] = 180,
            [Escrita] = 40,
            [Auth] = 8,
            [Pdf] = 30,
            [Midia] = 30
        };

    public static int LimiteDe(IConfiguration configuracao, string politica) =>
        configuracao.GetValue<int?>($"RateLimiting:{politica}")
        ?? (PadroesPorMinuto.TryGetValue(politica, out var padrao)
            ? padrao
            : throw new InvalidOperationException(
                $"Política sem limite conhecido: {politica}"));
}
```

- [x] **Step 2: Criar `RejeicaoDoRateLimiter` sem comportamento (vermelho)**

```csharp
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Fisga.Api.RateLimiting;

/// <summary>
/// Rejeição do rate limiter: mantém o corpo vazio e publica <c>Retry-After</c>
/// em segundos, como o contrato promete no <c>429</c>.
/// </summary>
public static class RejeicaoDoRateLimiter
{
    public static string DescricaoDoHeader =>
        "Segundos até a próxima janela de requisições.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opcoes.OnRejected = EscreverAsync;
    }

    public static ValueTask EscreverAsync(OnRejectedContext contexto, CancellationToken ct) =>
        ValueTask.CompletedTask; // TODO: header
}
```

- [x] **Step 3: Ligar no host e nos testes**

Em `src/Fisga.Api/Program.cs`, trocar o bloco de políticas por:

```csharp
builder.Services.AddRateLimiter(opcoes =>
{
    RejeicaoDoRateLimiter.Configurar(opcoes);
    // Limites por minuto/IP. Os valores padrão são os de produção; ambientes de
    // teste podem elevá-los por configuração (RateLimiting__Auth=1000) sem
    // alterar o comportamento padrão.
    foreach (var politica in PoliticasDeTaxa.PadroesPorMinuto.Keys)
    {
        AdicionarPolitica(opcoes, builder.Configuration, politica);
    }
});
```

E `AdicionarPolitica` passa a receber só o nome:

```csharp
static void AdicionarPolitica(
    Microsoft.AspNetCore.RateLimiting.RateLimiterOptions opcoes,
    IConfiguration configuration,
    string nome)
{
    var limitePorMinuto = PoliticasDeTaxa.LimiteDe(configuration, nome);
    opcoes.AddPolicy(nome, contexto =>
    {
        var chave = contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo";
        return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limitePorMinuto,
            Window = TimeSpan.FromSeconds(PoliticasDeTaxa.JanelaEmSegundos),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
}
```

Adicionar `using Fisga.Api.RateLimiting;` no topo de `Program.cs`.

Em `tests/Fisga.Api.Tests/TestWebApplicationFactory.cs`, no `services.Configure<RateLimiterOptions>`, substituir a linha `options.RejectionStatusCode = ...` por `RejeicaoDoRateLimiter.Configurar(options);` (as políticas continuam `GetNoLimiter`). Adicionar `using Fisga.Api.RateLimiting;`.

Em `tests/Fisga.Api.Tests/ContratoTests.cs`, apagar o dicionário local `LimitesPadraoPorMinuto` e passar a usar `PoliticasDeTaxa.PadroesPorMinuto`; o `windowSeconds` esperado passa a ser `PoliticasDeTaxa.JanelaEmSegundos`. Adicionar `using Fisga.Api.RateLimiting;`.

Em `src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs`, apagar o dicionário privado `LimitesPadraoPorMinuto` e a constante `JanelaEmSegundos`; `EscreverExtensoes` passa a chamar `PoliticasDeTaxa.LimiteDe(configuration, nome)` e `PoliticasDeTaxa.JanelaEmSegundos`. Adicionar `using Fisga.Api.RateLimiting;`.

- [x] **Step 4: Escrever os testes que falham**

`tests/Fisga.Api.Tests/RateLimitTests.cs`:

```csharp
using System.Threading.RateLimiting;
using Fisga.Api.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fisga.Api.Tests;

// O rate limiter real é neutralizado na factory de teste (todas as políticas são
// GetNoLimiter), então o header é verificado no callback que o host realmente
// registra, com leases construídas à mão.
[Collection("api")]
public sealed class RateLimitTests
{
    private readonly ApiTestFixture _fixture;

    public RateLimitTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RejeicaoEscreveRetryAfterEmSegundosInteiros()
    {
        var contexto = new DefaultHttpContext();
        var lease = new LeaseFalso(TimeSpan.FromSeconds(41.2));

        await RejeicaoDoRateLimiter.EscreverAsync(
            new OnRejectedContext { HttpContext = contexto, Lease = lease },
            CancellationToken.None);

        Assert.Equal("42", contexto.Response.Headers.RetryAfter.ToString());
        Assert.Equal(0, contexto.Response.Body.Length);
    }

    [Fact]
    public async Task RejeicaoSemMetadataUsaAJanelaDaPolitica()
    {
        var contexto = new DefaultHttpContext();

        await RejeicaoDoRateLimiter.EscreverAsync(
            new OnRejectedContext { HttpContext = contexto, Lease = new LeaseFalso(null) },
            CancellationToken.None);

        Assert.Equal(
            PoliticasDeTaxa.JanelaEmSegundos.ToString(System.Globalization.CultureInfo.InvariantCulture),
            contexto.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void HostRegistraORetornoDoCallback()
    {
        var opcoes = _fixture.Factory.Services
            .GetRequiredService<IOptions<RateLimiterOptions>>()
            .Value;

        Assert.NotNull(opcoes.OnRejected);
        Assert.Equal(StatusCodes.Status429TooManyRequests, opcoes.RejectionStatusCode);
    }

    private sealed class LeaseFalso(TimeSpan? espera) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames =>
            espera is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (espera is not null
                && string.Equals(metadataName, MetadataName.RetryAfter.Name, StringComparison.Ordinal))
            {
                metadata = espera.Value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
```

- [x] **Step 5: Rodar e ver falhar**

Run: `export PATH="$HOME/.dotnet:$PATH" && dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~RateLimitTests"`
Expected: `HostRegistraORetornoDoCallback` PASS; os dois primeiros falham com `RetryAfter` vazio.

- [x] **Step 6: Implementar o header**

```csharp
public static ValueTask EscreverAsync(OnRejectedContext contexto, CancellationToken ct)
{
    var segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
        ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
        : PoliticasDeTaxa.JanelaEmSegundos;

    contexto.HttpContext.Response.Headers.RetryAfter =
        segundos.ToString(CultureInfo.InvariantCulture);

    return ValueTask.CompletedTask;
}
```

- [x] **Step 7: Rodar tudo o que toca o contrato**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~RateLimitTests|FullyQualifiedName~ContratoTests"`
Expected: os 5 testes de `ContratoTests` continuam verdes; os 3 de `RateLimitTests` verdes.

- [x] **Step 8: Commit**

```bash
git add src/Fisga.Api/RateLimiting src/Fisga.Api/Program.cs src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs tests/Fisga.Api.Tests
git commit -m "feat(fisga): Retry-After no 429 e fonte única das políticas de rate limit"
```

---

### Task 2: Infraestrutura de respostas, catálogo de erros e testes invariantes

**Files:**
- Create: `src/Fisga.Api/OpenApi/CodigosDeErro.cs`
- Create: `src/Fisga.Api/OpenApi/RespostasPadrao.cs`
- Create: `src/Fisga.Api/OpenApi/Respostas.cs`
- Modify: `src/Fisga.Api/OpenApi/ContratoOperationTransformer.cs` (`NormalizarRespostas`)
- Modify: `src/Fisga.Api/Middleware/ExceptionHandlingMiddleware.cs`
- Modify: `src/Fisga.Api/Middleware/ConsentimentoMiddleware.cs`
- Modify: `src/Fisga.Api/Middleware/ProtecaoCsrfMiddleware.cs`
- Create: `tests/Fisga.Api.Tests/RespostasEsperadas.cs`
- Create: `tests/Fisga.Api.Tests/NormalizacaoDeRespostasTests.cs`
- Modify: `tests/Fisga.Api.Tests/ContratoTests.cs`
- Modify: `src/Fisga.Api/Fisga.Api.csproj` (`<InternalsVisibleTo Include="Fisga.Api.Tests" />`)

**Interfaces:**
- Produces:
  - `CodigosDeErro` com 17 `const string` e `Todos` (`IReadOnlySet<string>`).
  - `RespostasPadrao` com `TipoProblema`, `TipoJson`, `TipoPdf`, `DescricaoComErro(int)` e `DescricaoSemSchema(int)` (`string?`).
  - `Respostas.ComErro(this RouteHandlerBuilder, int)` e `Respostas.SemCorpo(this RouteHandlerBuilder, int)`.
  - `RespostasEsperadas.PorOperacao`, `NotFoundSemCorpo`, `TemCorpo(operacao, status)`.
- Consumes: `IOpenApiOperationTransformer`, `OpenApiResponses` (`OpenApiDictionary<IOpenApiResponse>`), `OpenApiResponse`, `OpenApiHeader`.

- [x] **Step 1: Criar o catálogo de códigos**

`src/Fisga.Api/OpenApi/CodigosDeErro.cs` — constantes para todo `codigo` que a camada de API produz. Códigos de regra de negócio continuam nascendo no domínio (`DomainException`), um lugar por regra; este catálogo cobre o vocabulário HTTP da API (middlewares + validações inline).

```csharp
using System.Reflection;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Vocabulário dos <c>codigo</c> de erro que a camada HTTP produz. Middlewares e
/// handlers usam as constantes; o contrato e o portal documentam a partir delas.
/// Regras de negócio continuam criando o seu código no domínio, onde a regra vive.
/// </summary>
public static class CodigosDeErro
{
    public const string NaoAutenticado = "nao_autenticado";
    public const string AcessoNegado = "acesso_negado";
    public const string ErroInesperado = "erro_inesperado";
    public const string ConsentimentoPendente = "consentimento_pendente";
    public const string OrigemInvalida = "origem_invalida";
    public const string PescariaNaoEncontrada = "pescaria_nao_encontrada";
    public const string LocalSemCoordenadas = "local_sem_coordenadas";
    public const string CoordenadasInvalidas = "coordenadas_invalidas";
    public const string GoogleIndisponivel = "google_indisponivel";
    public const string MidiaSemFormulario = "midia_sem_formulario";
    public const string MidiaVazia = "midia_vazia";
    public const string MidiaGrande = "midia_grande";
    public const string MidiaTipoInvalido = "midia_tipo_invalido";
    public const string SugestaoSemFormulario = "sugestao_sem_formulario";
    public const string SugestaoSemFoto = "sugestao_sem_foto";
    public const string SugestaoFotoGrande = "sugestao_foto_grande";
    public const string SugestaoTipoInvalido = "sugestao_tipo_invalido";

    /// <summary>Todos os códigos declarados acima, sem chance de esquecer um.</summary>
    public static IReadOnlySet<string> Todos { get; } = typeof(CodigosDeErro)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(campo => campo.IsLiteral && campo.FieldType == typeof(string))
        .Select(campo => (string)campo.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);
}
```

- [x] **Step 2: Criar `RespostasPadrao`**

`src/Fisga.Api/OpenApi/RespostasPadrao.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Media types e descrições padrão das respostas do contrato. A descrição mora
/// aqui, não em cada endpoint, para o mesmo status significar a mesma coisa nas
/// 54 operações.
/// </summary>
public static class RespostasPadrao
{
    public const string TipoProblema = "application/problem+json";
    public const string TipoJson = "application/json";
    public const string TipoPdf = "application/pdf";

    /// <summary>Descrição de um status cujo corpo é <see cref="Fisga.Api.Contracts.ErroResponse"/>.</summary>
    /// <exception cref="InvalidOperationException">Status sem descrição catalogada — falha cedo em vez de publicar texto genérico.</exception>
    public static string DescricaoComErro(int status) => status switch
    {
        StatusCodes.Status400BadRequest =>
            $"Requisição inválida. Regra de negócio devolve `codigo` e `detalhe`; corpo JSON malformado devolve ProblemDetails do framework.",
        StatusCodes.Status401Unauthorized =>
            $"Sessão ausente, expirada ou revogada (`codigo`: `{CodigosDeErro.NaoAutenticado}`).",
        StatusCodes.Status403Forbidden =>
            $"Sem permissão (`{CodigosDeErro.AcessoNegado}`), consentimento pendente (`{CodigosDeErro.ConsentimentoPendente}`) ou origem não permitida (`{CodigosDeErro.OrigemInvalida}`).",
        StatusCodes.Status404NotFound => "Recurso não encontrado.",
        StatusCodes.Status500InternalServerError =>
            $"Erro inesperado (`codigo`: `{CodigosDeErro.ErroInesperado}`). Tente novamente em instantes.",
        StatusCodes.Status503ServiceUnavailable => "Serviço indisponível no momento.",
        _ => throw new InvalidOperationException($"Status sem descrição de erro com corpo: {status}")
    };

    /// <summary>Descrição de um status que não publica schema de corpo (ou o publica em outro tipo).</summary>
    public static string? DescricaoSemSchema(int status) => status switch
    {
        StatusCodes.Status201Created =>
            "Recurso criado. O header `Location` aponta para ele.",
        StatusCodes.Status202Accepted =>
            "Requisição aceita para processamento.",
        StatusCodes.Status204NoContent => "Resposta sem corpo.",
        StatusCodes.Status302Found => "Redireciona o navegador para outra URL.",
        StatusCodes.Status404NotFound => "Recurso não encontrado. Corpo vazio.",
        StatusCodes.Status413PayloadTooLarge =>
            "Upload recusado pelo servidor antes do handler. Corpo vazio; reduza a imagem e tente de novo.",
        StatusCodes.Status429TooManyRequests =>
            "Muitas requisições. Corpo vazio; o header `Retry-After` informa os segundos até a próxima tentativa.",
        StatusCodes.Status503ServiceUnavailable => "Serviço indisponível. Corpo vazio.",
        _ => null
    };
}
```

- [x] **Step 3: Criar os helpers de endpoint**

`src/Fisga.Api/OpenApi/Respostas.cs`:

```csharp
using Fisga.Api.Contracts;
using Microsoft.AspNetCore.Builder;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Metadata de resposta para o contrato. <see cref="ComErro"/> publica o schema
/// único de erro para qualquer status cujo corpo seja <c>ErroResponse</c>;
/// <see cref="SemCorpo"/> declara um status sem corpo (204, 302, 404 de rota
/// administrativa ou sem resultado, 429, 503). O transformer normaliza
/// media types, headers e descrições.
/// </summary>
public static class Respostas
{
    public static RouteHandlerBuilder ComErro(this RouteHandlerBuilder construtor, int status) =>
        construtor.Produces<ErroResponse>(status, RespostasPadrao.TipoProblema);

    public static RouteHandlerBuilder SemCorpo(this RouteHandlerBuilder construtor, int status) =>
        construtor.Produces(status);
}
```

- [x] **Step 4: Normalizar no transformer**

Em `ContratoOperationTransformer.cs`:

1. Trocar `using Microsoft.AspNetCore.Http;` (adicionar), `Microsoft.OpenApi`, `System.Text.Json.Nodes` já existentes.
2. Substituir a constante `JanelaEmSegundos` por `PoliticasDeTaxa.JanelaEmSegundos` (Task 1 pode já ter feito isso; se não, fazer agora).
3. Chamar `NormalizarRespostas(operation);` depois de `EscreverExtensoes(...)`.
4. Adicionar:

```csharp
// Respostas são geradas pela metadata Produces<T>; aqui elas ganham o que a
// metadata não expressa: o espelho do schema de erro em application/json,
// descrição por status e os headers Location/Retry-After.
internal static void NormalizarRespostas(OpenApiOperation operation)
{
    if (operation.Responses is null)
    {
        return;
    }

    foreach (var (codigo, resposta) in operation.Responses)
    {
        if (!int.TryParse(codigo, out var status) || resposta is not OpenApiResponse concreta)
        {
            continue;
        }

        if (status is StatusCodes.Status201Created or StatusCodes.Status202Accepted)
        {
            concreta.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concreta.Headers["Location"] = new OpenApiHeader
            {
                Description = "URL do recurso criado ou do próximo passo.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" }
            };
        }

        if (status == StatusCodes.Status429TooManyRequests)
        {
            concreta.Headers ??= new Dictionary<string, IOpenApiHeader>();
            concreta.Headers["Retry-After"] = new OpenApiHeader
            {
                Description = RejeicaoDoRateLimiter.DescricaoDoHeader,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" }
            };
        }

        // Corpo binário: `byte[]` vira `format: byte` (base64), mas o handler escreve
        // bytes crus — `binary` é o formato honesto para o cliente gerado.
        if (concreta.Content is { } conteudo
            && conteudo.TryGetValue(RespostasPadrao.TipoPdf, out var pdf))
        {
            pdf.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
        }

        if (concreta.Content?.ContainsKey(RespostasPadrao.TipoProblema) == true)
        {
            // Cópia, não o mesmo objeto: editar um media type não pode editar o outro.
            var problema = concreta.Content[RespostasPadrao.TipoProblema];
            concreta.Content.TryAdd(
                RespostasPadrao.TipoJson,
                new OpenApiMediaType { Schema = problema.Schema, Example = problema.Example });
            concreta.Description = RespostasPadrao.DescricaoComErro(status);
        }
        else if (RespostasPadrao.DescricaoSemSchema(status) is { } descricao)
        {
            concreta.Description = descricao;
        }
    }
}
```

- [x] **Step 5: Usar o catálogo nos middlewares**

Substituir os literais por `CodigosDeErro.*` nos três arquivos:

- `ExceptionHandlingMiddleware`: `nao_autenticado` → `CodigosDeErro.NaoAutenticado`, `acesso_negado` → `AcessoNegado`, `erro_inesperado` → `ErroInesperado`.
- `ConsentimentoMiddleware`: `consentimento_pendente` → `CodigosDeErro.ConsentimentoPendente`.
- `ProtecaoCsrfMiddleware`: `origem_invalida` → `CodigosDeErro.OrigemInvalida`.

Cada arquivo ganha `using Fisga.Api.OpenApi;`.

- [x] **Step 6: Congelar a tabela de respostas por operação**

`tests/Fisga.Api.Tests/RespostasEsperadas.cs` — status que cada operação pode devolver, medidos no handler e em `inventario.md`. A chave é a mesma de `MapaDeOperacoes` (método + rota normalizada).

```csharp
namespace Fisga.Api.Tests;

/// <summary>
/// Status que cada operação devolve de fato, medidos no handler e no middleware.
/// Não é a intenção do contrato: é a lista que o contrato precisa declarar.
/// </summary>
/// <remarks>
/// A tabela enumera o que a operação produz: status do handler, guardas de
/// sessão/consentimento, um 403 próprio da operação e o 429 sempre que a
/// operação publica <c>x-rate-limit</c>. Duas respostas universais de
/// middleware não são enumeradas por operação, porque valem para todas e estão
/// documentadas uma vez em <c>inventario.md</c> § Regras transversais:
/// <c>403 origem_invalida</c> (CSRF em qualquer mutação) e
/// <c>500 erro_inesperado</c> (qualquer exceção não tratada, declarado só onde
/// a operação tem caminho de falha próprio: provedores externos, saúde).
/// </remarks>
internal static class RespostasEsperadas
{
    public static IReadOnlyDictionary<(string Metodo, string Rota), int[]> PorOperacao { get; } =
        new Dictionary<(string Metodo, string Rota), int[]>
        {
            // Sistema & Legal
            [("GET", "/health")] = [200, 500],
            [("GET", "/ready")] = [200, 503, 500],
            [("GET", "/api/legal/versoes")] = [200, 429],

            // Catálogo
            [("GET", "/api/catalogo")] = [200, 429],

            // Autenticação & Sessão
            [("POST", "/api/auth/solicitar")] = [202, 400, 429],
            [("POST", "/api/auth/registrar")] = [201, 400, 429],
            [("POST", "/api/auth/entrar")] = [200, 400, 429],
            [("POST", "/api/auth/confirmar-email")] = [200, 400, 429],
            [("POST", "/api/auth/esqueci-senha")] = [202, 400, 429],
            [("POST", "/api/auth/redefinir-senha")] = [200, 400, 429],
            [("GET", "/api/auth/confirmar")] = [302, 429, 500],
            [("GET", "/api/auth/google/disponivel")] = [200, 429],
            [("GET", "/api/auth/google/iniciar")] = [302, 503, 429],
            [("GET", "/api/auth/google/concluir")] = [302, 429],
            [("GET", "/api/auth/google/conectar")] = [302, 401, 403, 503, 429],
            [("GET", "/api/auth/google/conectar/concluir")] = [302, 403, 429],
            [("GET", "/api/auth/me")] = [200, 429],
            [("PATCH", "/api/auth/perfil")] = [200, 400, 401, 403, 429],
            [("POST", "/api/auth/sair")] = [204, 429],
            [("POST", "/api/auth/sair-de-todos")] = [204, 401, 403, 429],

            // Conta & Consentimento
            [("POST", "/api/conta/consentimento")] = [204, 400, 401, 429],
            [("POST", "/api/conta/senha")] = [204, 400, 401, 403, 429],
            [("GET", "/api/conta/sessoes")] = [200, 401, 403, 429],
            [("GET", "/api/conta/exportar")] = [200, 401, 403, 429],
            [("DELETE", "/api/conta")] = [204, 400, 401, 403, 429],

            // Pescarias & Compartilhamento
            [("POST", "/api/pescarias")] = [201, 400, 401, 403, 429],
            [("GET", "/api/pescarias/{shareToken}")] = [200, 404, 429],
            [("GET", "/api/pescarias/{shareToken}/permissao")] = [200, 404, 429],
            [("GET", "/api/pescarias/{id}/editar")] = [200, 401, 403, 404, 429],
            [("PUT", "/api/pescarias/{id}")] = [200, 400, 401, 403, 404, 429],
            [("GET", "/api/pescarias/{shareToken}/pdf")] = [200, 404, 429],

            // Diário
            [("GET", "/api/diario")] = [200, 401, 403, 429],

            // Condições
            [("GET", "/api/condicoes")] = [200, 400, 429, 500],
            [("GET", "/api/condicoes/comparar")] = [200, 400, 401, 403, 429, 500],

            // Locais
            [("GET", "/api/locais/reverso")] = [200, 400, 404, 429],
            [("GET", "/api/locais/buscar")] = [200, 429],
            [("GET", "/api/locais")] = [200, 401, 403, 429],
            [("POST", "/api/locais")] = [201, 400, 401, 403, 429],
            [("PUT", "/api/locais/{id}")] = [200, 400, 401, 403, 404, 429],
            [("POST", "/api/locais/{id}/padrao")] = [200, 401, 403, 404, 429],
            [("DELETE", "/api/locais/{id}")] = [204, 401, 403, 404, 429],

            // Pontos
            [("GET", "/api/pontos")] = [200, 401, 403, 429],
            [("POST", "/api/pontos")] = [201, 400, 401, 403, 429],
            [("PUT", "/api/pontos/{id}")] = [200, 400, 401, 403, 404, 429],
            [("DELETE", "/api/pontos/{id}")] = [204, 401, 403, 404, 429],

            // Trilhas
            [("GET", "/api/trilhas")] = [200, 401, 403, 429],
            [("POST", "/api/trilhas")] = [201, 400, 401, 403, 429],
            [("PUT", "/api/trilhas/{id}")] = [200, 400, 401, 403, 404, 429],
            [("DELETE", "/api/trilhas/{id}")] = [204, 401, 403, 404, 429],

            // Mídia
            [("POST", "/api/midia")] = [200, 400, 401, 403, 429],

            // Visão/Espécies
            [("POST", "/api/especies/sugerir")] = [200, 400, 401, 403, 429, 500],

            // Newsletter/Admin
            [("POST", "/api/newsletter/enviar")] = [200, 404, 500],
            [("GET", "/api/newsletter/preview")] = [200, 404, 500],
            [("GET", "/api/newsletter/descadastrar")] = [302, 429]
        };

    // 404 sem corpo: token ausente nas rotas administrativas e coordenada sem
    // local no geocodificador. Todo outro 404 carrega ErroResponse.
    private static readonly HashSet<(string Metodo, string Rota)> NotFoundSemCorpo =
    [
        ("GET", "/api/locais/reverso"),
        ("POST", "/api/newsletter/enviar"),
        ("GET", "/api/newsletter/preview")
    ];

    public static bool TemCorpo((string Metodo, string Rota) operacao, int status) => status switch
    {
        204 or 302 or 429 => false,
        404 => !NotFoundSemCorpo.Contains(operacao),
        503 => operacao != ("GET", "/ready"),
        _ => true
    };
}
```

> **Auditoria obrigatória nesta task:** conferir cada linha contra o handler antes de
> commitar. Duas divergências já conhecidas do inventário de 20/11:
> `GET /api/auth/confirmar` não devolve `400` (o `DomainException` é convertido em
> `302` com `estado=<codigo>`), e `GET /api/auth/google/conectar/concluir` responde
> `302` mesmo sem sessão. Se outra linha divergir, o handler vence e o
> `inventario.md` é corrigido na Task 8.

- [x] **Step 7: Escrever os testes invariantes**

Acrescentar em `tests/Fisga.Api.Tests/ContratoTests.cs` (usar `using Microsoft.AspNetCore.Http;` se necessário):

```csharp
public static TheoryData<string> Dominios => new(Tags.Todas);

[Theory]
[MemberData(nameof(Dominios))]
public async Task RespostasDeclaradasSaoAsReais(string dominio)
{
    var documento = await LoadAsync();
    var operacoes = Operacoes(documento)
        .Where(operacao => Tag(operacao.Operacao) == dominio)
        .ToList();

    Assert.NotEmpty(operacoes);

    var violacoes = new List<string>();
    foreach (var (metodo, rota, operacao) in operacoes)
    {
        var chave = (metodo, rota);
        if (!RespostasEsperadas.PorOperacao.TryGetValue(chave, out var esperados))
        {
            violacoes.Add($"Operação sem status esperado: {metodo} {rota}");
            continue;
        }

        var declarados = operacao.GetProperty("responses").EnumerateObject()
            .Select(resposta => resposta.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var listaEsperada = esperados
            .Select(status => status.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!listaEsperada.SequenceEqual(declarados))
        {
            violacoes.Add(
                $"{metodo} {rota}: esperado [{string.Join(", ", listaEsperada)}], " +
                $"declarado [{string.Join(", ", declarados)}]");
        }
    }

    Assert.Empty(violacoes);
}

[Fact]
public void TabelaDeRespostasCobreAs54Operacoes()
{
    Assert.Equal(54, MapaDeOperacoes.Todos.Count);
    Assert.Equal(
        MapaDeOperacoes.Todos.Keys.Order(StringComparer.Ordinal),
        RespostasEsperadas.PorOperacao.Keys
            .Select(chave => $"{chave.Metodo} {chave.Rota.TrimStart('/')}")
            .Order(StringComparer.Ordinal));
}

[Fact]
public async Task TodaOperacaoComRateLimitDeclara429()
{
    var documento = await LoadAsync();
    var operacoes = Operacoes(documento);

    var comPolitica = operacoes
        .Where(operacao => operacao.Operacao.TryGetProperty("x-rate-limit", out _))
        .Select(operacao => (operacao.Metodo, operacao.Rota))
        .ToHashSet();
    var com429 = operacoes
        .Where(operacao => operacao.Operacao.GetProperty("responses").TryGetProperty("429", out _))
        .Select(operacao => (operacao.Metodo, operacao.Rota))
        .ToHashSet();

    Assert.Equal(50, comPolitica.Count);
    Assert.True(
        comPolitica.SetEquals(com429),
        $"Sem 429: {string.Join(", ", comPolitica.Except(com429))}; " +
        $"429 sem política: {string.Join(", ", com429.Except(comPolitica))}");
    Assert.Equal(
        50,
        RespostasEsperadas.PorOperacao.Count(entrada => entrada.Value.Contains(429)));
}

[Theory]
[MemberData(nameof(Dominios))]
public async Task ErrosUsamUmUnicoSchema(string dominio)
{
    var documento = await LoadAsync();
    var operacoes = Operacoes(documento)
        .Where(operacao => Tag(operacao.Operacao) == dominio)
        .ToList();

    var violacoes = new List<string>();
    foreach (var (metodo, rota, operacao) in operacoes)
    {
        var chave = (metodo, rota);
        foreach (var resposta in operacao.GetProperty("responses").EnumerateObject())
        {
            var status = int.Parse(resposta.Name, System.Globalization.CultureInfo.InvariantCulture);
            var objeto = resposta.Value;
            var content = objeto.TryGetProperty("content", out var conteudo) ? conteudo : default;

            if (!RespostasEsperadas.TemCorpo(chave, status))
            {
                if (content.ValueKind != JsonValueKind.Undefined && content.EnumerateObject().Any())
                {
                    violacoes.Add($"{metodo} {rota} {status}: resposta sem corpo declarou content");
                }

                if (status == 429)
                {
                    var headers = objeto.TryGetProperty("headers", out var h) ? h : default;
                    if (headers.ValueKind != JsonValueKind.Object
                        || !headers.TryGetProperty("Retry-After", out _))
                    {
                        violacoes.Add($"{metodo} {rota}: 429 sem header Retry-After");
                    }
                }

                continue;
            }

            if (status == 201 || status == 202)
            {
                var headers = objeto.TryGetProperty("headers", out var h) ? h : default;
                if (headers.ValueKind != JsonValueKind.Object
                    || !headers.TryGetProperty("Location", out _))
                {
                    violacoes.Add($"{metodo} {rota}: {status} sem header Location");
                }
            }

            if (status < 400)
            {
                continue;
            }

            foreach (var tipo in new[] { RespostasPadrao.TipoProblema, RespostasPadrao.TipoJson })
            {
                if (!content.TryGetProperty(tipo, out var midia)
                    || !midia.TryGetProperty("schema", out var schema)
                    || schema.GetProperty("$ref").GetString() != "#/components/schemas/ErroResponse")
                {
                    violacoes.Add($"{metodo} {rota} {status}: sem schema ErroResponse em {tipo}");
                }
            }

            if (objeto.GetProperty("description").GetString() != RespostasPadrao.DescricaoComErro(status))
            {
                violacoes.Add($"{metodo} {rota} {status}: descrição fora do catálogo");
            }
        }
    }

    Assert.Empty(violacoes);
}

[Fact]
public async Task UploadsDescrevemMultipart()
{
    var documento = await LoadAsync();

    foreach (var (metodo, rota) in new[]
             {
                 ("POST", "/api/midia"),
                 ("POST", "/api/especies/sugerir")
             })
    {
        var operacao = Operacoes(documento).Single(o => o.Metodo == metodo && o.Rota == rota).Operacao;
        var multipart = operacao.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("multipart/form-data");
        var referencia = multipart.GetProperty("schema").GetProperty("$ref").GetString()!;
        var nome = referencia["#/components/schemas/".Length..];
        var schema = documento.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(nome);

        Assert.True(schema.GetProperty("properties").TryGetProperty("arquivo", out var arquivo));
        Assert.Contains("arquivo", schema.GetProperty("required").EnumerateArray().Select(v => v.GetString()));

        var arquivoRef = arquivo.GetProperty("$ref").GetString()!;
        var arquivoSchema = documento.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(arquivoRef["#/components/schemas/".Length..]);
        Assert.Equal("string", arquivoSchema.GetProperty("type").GetString());
        Assert.Equal("binary", arquivoSchema.GetProperty("format").GetString());
    }
}

[Fact]
public void CatalogoDeCodigosDeErroEhCoerente()
{
    Assert.Equal(17, CodigosDeErro.Todos.Count);
    foreach (var codigo in CodigosDeErro.Todos)
    {
        Assert.Matches("^[a-z]+(_[a-z]+)+$", codigo);
    }
}
```

Adicionar o helper privado:

```csharp
private static string? Tag(JsonElement operacao) =>
    operacao.TryGetProperty("tags", out var tags) && tags.GetArrayLength() == 1
        ? tags[0].GetString()
        : null;
```

- [x] **Step 8: Trocar a invariante de segurança para o conjunto curado**

Em `ContratoTests.cs`, substituir a linha que lê `IAllowAnonymous` por um conjunto congelado de 23 rotas:

```csharp
// Rotas anônimas por decisão de produto: leitura pública, login, recuperação,
// descadastro e saúde. A invariante é a intenção, não a metadata do endpoint —
// derivar de IAllowAnonymous faria o teste concordar com qualquer mudança.
private static readonly HashSet<(string Metodo, string Rota)> RotasAnonimasPorDecisao =
[
    ("GET", "/api/auth/confirmar"),
    ("POST", "/api/auth/confirmar-email"),
    ("POST", "/api/auth/entrar"),
    ("POST", "/api/auth/esqueci-senha"),
    ("GET", "/api/auth/google/concluir"),
    ("GET", "/api/auth/google/disponivel"),
    ("GET", "/api/auth/google/iniciar"),
    ("GET", "/api/auth/me"),
    ("POST", "/api/auth/redefinir-senha"),
    ("POST", "/api/auth/registrar"),
    ("POST", "/api/auth/sair"),
    ("POST", "/api/auth/solicitar"),
    ("GET", "/api/catalogo"),
    ("GET", "/api/condicoes"),
    ("GET", "/api/legal/versoes"),
    ("GET", "/api/locais/buscar"),
    ("GET", "/api/locais/reverso"),
    ("GET", "/api/newsletter/descadastrar"),
    ("GET", "/api/pescarias/{shareToken}"),
    ("GET", "/api/pescarias/{shareToken}/pdf"),
    ("GET", "/api/pescarias/{shareToken}/permissao"),
    ("GET", "/health"),
    ("GET", "/ready")
];
```

No laço de `SegurancaExpressaPorOperacao`, trocar

```csharp
var anonima = Localizar(metodo, rota).Metadata.GetMetadata<IAllowAnonymous>() is not null;
```

por

```csharp
var anonima = RotasAnonimasPorDecisao.Contains((metodo, rota));
```

e, ao final do teste, acrescentar:

```csharp
Assert.Equal(23, RotasAnonimasPorDecisao.Count);
Assert.Equal(2, ComTokenAdmin.Count);
```

- [x] **Step 9: Rodar e ver o vermelho esperado**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: `TodaOperacaoTemOperationIdDoMapa`, `ResumosSaoDeNegocio`, `TagsSaoDeDominio`, `SegurancaExpressaPorOperacao`, `RateLimitECacheRefletemMetadata`, `TabelaDeRespostasCobreAs54Operacoes`, `TodaOperacaoComRateLimitDeclara429` e `CatalogoDeCodigosDeErroEhCoerente` PASS. As theories `RespostasDeclaradasSaoAsReais` falham em todos os domínios (o spec ainda declara só `200`) e `UploadsDescrevemMultipart` falha (sem `Accepts`) — esse é o vermelho das Tasks 3–7. `ErrosUsamUmUnicoSchema` passa porque ainda não existe resposta de erro declarada; a cobertura da normalização (espelho de media type, descrição e headers) vem de `NormalizacaoDeRespostasTests`, que exercita `NormalizarRespostas` num `OpenApiOperation` construído à mão. A theory ganha corpo real quando a Task 3 declarar o primeiro 4xx.

- [x] **Step 10: Commit**

```bash
git add src/Fisga.Api/OpenApi src/Fisga.Api/Middleware tests/Fisga.Api.Tests
git commit -m "test(fisga): respostas reais, erro único e multipart como invariantes do contrato"
```

---

### Task 3: Respostas de Catálogo, Pescarias, Diário e Mídia

**Files:**
- Modify: `src/Fisga.Api/Endpoints/PescariaEndpoints.cs`
- Modify: `src/Fisga.Api/Contracts/Contracts.cs` (novo `FormularioDeFoto`)
- Test: `tests/Fisga.Api.Tests/ContratoTests.cs` (theories `Catálogo`, `Pescarias`, `Diário`, `Mídia`; `UploadsDescrevemMultipart` continua vermelho só por Espécies)

**Interfaces:**
- Consumes: `Respostas.ComErro`, `Respostas.SemCorpo`, `CodigosDeErro`, `Tags`.
- Produces: `Fisga.Api.Contracts.FormularioDeFoto(Microsoft.AspNetCore.Http.IFormFile Arquivo)`.

- [x] **Step 1: Criar o formulário de upload**

Em `src/Fisga.Api/Contracts/Contracts.cs`:

```csharp
/// <summary>
/// Forma do corpo multipart documentada no contrato. O handler continua lendo o
/// arquivo por <c>Request.Files.GetFile("arquivo")</c>; o tipo existe para o
/// schema publicar o nome do campo e o formato binário.
/// </summary>
public sealed record FormularioDeFoto(Microsoft.AspNetCore.Http.IFormFile Arquivo);
```

- [x] **Step 2: Anotar cada operação**

Na declaração de cada endpoint, logo antes de `.RequireRateLimiting(...)`:

| Operação | Metadata de resposta |
| --- | --- |
| `GET /api/catalogo` | `.Produces<CatalogoDto>(200)` `.SemCorpo(429)` |
| `POST /api/pescarias` | `.Produces<PescariaSalvaResponse>(201)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `GET /api/pescarias/{shareToken}` | `.Produces<PescariaDetalhadaDto>(200)` `.ComErro(404)` `.SemCorpo(429)` |
| `GET /api/pescarias/{shareToken}/permissao` | `.Produces<PermissaoResponse>(200)` `.ComErro(404)` `.SemCorpo(429)` |
| `GET /api/pescarias/{id:guid}/editar` | `.Produces<PescariaDetalhadaDto>(200)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |
| `PUT /api/pescarias/{id:guid}` | `.Produces<PescariaDetalhadaDto>(200)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |
| `GET /api/pescarias/{shareToken}/pdf` | `.Produces<byte[]>(200, "application/pdf")` `.ComErro(404)` `.SemCorpo(429)` |
| `GET /api/diario` | `.Produces<DiarioDto>(200)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `POST /api/midia` | `.Produces<MidiaResponse>(200)` `.Accepts<FormularioDeFoto>("multipart/form-data")` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |

> **Atenção ao `404`:** só são corpo vazio `/api/locais/reverso`,
> `/api/newsletter/enviar` e `/api/newsletter/preview`; todos os outros vêm do
> middleware com `ErroResponse`. Confirme no handler antes de escolher entre
> `.ComErro(404)` e `.SemCorpo(404)`.

Adicionar os `using` que faltarem: `Fisga.Application.Catalogo`, `Fisga.Application.Pescarias` (já presentes), `Fisga.Api.RateLimiting` se necessário.

- [x] **Step 3: Usar o catálogo nos erros de mídia e pescaria**

Trocar os literais de `PescariaEndpoints.cs` pelas constantes: `PescariaNaoEncontrada`, `MidiaSemFormulario`, `MidiaVazia`, `MidiaGrande`, `MidiaTipoInvalido`.

- [x] **Step 4: Rodar as theories da área**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: `RespostasDeclaradasSaoAsReais` e `ErrosUsamUmUnicoSchema` PASS para `Catálogo`, `Pescarias`, `Diário` e `Mídia`; continuam falhando para os demais domínios. `UploadsDescrevemMultipart` ainda falha só em `/api/especies/sugerir`.

- [x] **Step 5: Conferir no JSON gerado**

Run: `./scripts/gerar-openapi.sh && python3 -c "import json; d=json.load(open('docs/api/openapi.json')); print(json.dumps(d['paths']['/api/pescarias']['post']['responses'], ensure_ascii=False, indent=1)); print(json.dumps(d['paths']['/api/midia']['post']['requestBody'], ensure_ascii=False))"`
Expected: `201` com `Location`, `400`/`401`/`403` com `ErroResponse` nos dois media types, `429` sem conteúdo e com `Retry-After`; `requestBody` com `arquivo`.

- [x] **Step 6: Commit**

```bash
git add src/Fisga.Api/Endpoints/PescariaEndpoints.cs src/Fisga.Api/Contracts/Contracts.cs
git commit -m "docs(fisga): respostas reais de catálogo, pescarias, diário e mídia"
```

> **Artefato:** o `docs/api/openapi.json` gerado no Step 5 serve só para
> conferência. Depois de inspecionar a saída, reverta com
> `git checkout -- docs/api/openapi.json` — o worktree fica limpo entre as
> tasks e a Task 8 gera e commita o artefato final de uma vez.

---

### Task 4: Respostas de Autenticação (17 operações)

**Files:**
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs`
- Test: theories `Autenticação`

- [x] **Step 1: Anotar cada operação**

| Operação | Metadata de resposta |
| --- | --- |
| `GET /api/legal/versoes` | `.Produces<VersoesLegaisResponse>(200)` `.SemCorpo(429)` |
| `POST /api/auth/solicitar` | `.Produces<SolicitarLinkResponse>(202)` `.ComErro(400)` `.SemCorpo(429)` |
| `POST /api/auth/registrar` | `.Produces<RegistrarResponse>(201)` `.ComErro(400)` `.SemCorpo(429)` |
| `POST /api/auth/entrar` | `.Produces<EntrarResponse>(200)` `.ComErro(400)` `.SemCorpo(429)` |
| `POST /api/auth/confirmar-email` | `.Produces<ConfirmarEmailResponse>(200)` `.ComErro(400)` `.SemCorpo(429)` |
| `POST /api/auth/esqueci-senha` | `.Produces<EsqueciSenhaResponse>(202)` `.ComErro(400)` `.SemCorpo(429)` |
| `POST /api/auth/redefinir-senha` | `.Produces<RedefinirSenhaResponse>(200)` `.ComErro(400)` `.SemCorpo(429)` |
| `GET /api/auth/confirmar` | `.SemCorpo(302)` `.SemCorpo(429)` `.ComErro(500)` |
| `GET /api/auth/google/disponivel` | `.Produces<GoogleDisponivelResponse>(200)` `.SemCorpo(429)` |
| `GET /api/auth/google/iniciar` | `.SemCorpo(302)` `.ComErro(503)` `.SemCorpo(429)` |
| `GET /api/auth/google/concluir` | `.SemCorpo(302)` `.SemCorpo(429)` |
| `GET /api/auth/google/conectar` | `.SemCorpo(302)` `.ComErro(401)` `.ComErro(403)` `.ComErro(503)` `.SemCorpo(429)` |
| `GET /api/auth/google/conectar/concluir` | `.SemCorpo(302)` `.ComErro(403)` `.SemCorpo(429)` |
| `GET /api/auth/me` | `.Produces<MeResponse>(200)` `.SemCorpo(429)` |
| `PATCH /api/auth/perfil` | `.Produces<UsuarioResponse>(200)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `POST /api/auth/sair` | `.SemCorpo(204)` `.SemCorpo(429)` |
| `POST /api/auth/sair-de-todos` | `.SemCorpo(204)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |

- [x] **Step 2: Usar o catálogo no `google_indisponivel`**

As duas ocorrências de `new ErroResponse("google_indisponivel", ...)` passam a usar `CodigosDeErro.GoogleIndisponivel`.

- [x] **Step 3: Rodar**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: `Autenticação` verde nas duas theories; demais domínios continuam vermelhos.

- [x] **Step 4: Commit**

```bash
git add src/Fisga.Api/Endpoints/AuthEndpoints.cs
git commit -m "docs(fisga): respostas reais de autenticação e sessão"
```

---

### Task 5: Respostas de Conta (5 operações)

**Files:**
- Modify: `src/Fisga.Api/Endpoints/AuthEndpoints.cs` (bloco `MapContaEndpoints`)
- Test: theory `Conta`

- [x] **Step 1: Anotar cada operação**

| Operação | Metadata de resposta |
| --- | --- |
| `POST /api/conta/consentimento` | `.SemCorpo(204)` `.ComErro(400)` `.ComErro(401)` `.SemCorpo(429)` |
| `POST /api/conta/senha` | `.SemCorpo(204)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `GET /api/conta/sessoes` | `.Produces<List<SessaoResponse>>(200)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `GET /api/conta/exportar` | `.Produces<ExportacaoDaConta>(200, "application/json")` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `DELETE /api/conta` | `.SemCorpo(204)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |

- [x] **Step 2: Rodar**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: `Conta` verde; falham apenas os domínios das Tasks 6 e 7.

- [x] **Step 3: Commit**

```bash
git add src/Fisga.Api/Endpoints/AuthEndpoints.cs
git commit -m "docs(fisga): respostas reais de conta e consentimento"
```

---

### Task 6: Respostas de Condições, Locais, Pontos e Trilhas

**Files:**
- Modify: `src/Fisga.Api/Endpoints/CondicoesEndpoints.cs`
- Modify: `src/Fisga.Api/Endpoints/PontosEndpoints.cs`
- Modify: `src/Fisga.Api/Endpoints/TrilhasEndpoints.cs`
- Test: theories `Condições`, `Locais`, `Pontos`, `Trilhas`

- [x] **Step 1: Anotar Condições**

| Operação | Metadata de resposta |
| --- | --- |
| `GET /api/condicoes` | `.Produces<CondicoesDto>(200)` `.ComErro(400)` `.SemCorpo(429)` `.ComErro(500)` |
| `GET /api/condicoes/comparar` | `.Produces<ComparacaoDto>(200)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` `.ComErro(500)` |

- [x] **Step 2: Anotar Locais**

| Operação | Metadata de resposta |
| --- | --- |
| `GET /api/locais/reverso` | `.Produces<ResultadoDeBusca>(200)` `.ComErro(400)` `.SemCorpo(404)` `.SemCorpo(429)` |
| `GET /api/locais/buscar` | `.Produces<IReadOnlyList<ResultadoDeBusca>>(200)` `.SemCorpo(429)` |
| `GET /api/locais` | `.Produces<IReadOnlyList<LocalDto>>(200)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `POST /api/locais` | `.Produces<LocalDto>(201)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `PUT /api/locais/{id:guid}` | `.Produces<LocalDto>(200)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |
| `POST /api/locais/{id:guid}/padrao` | `.Produces<LocalDto>(200)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |
| `DELETE /api/locais/{id:guid}` | `.SemCorpo(204)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |

- [x] **Step 3: Anotar Pontos e Trilhas**

| Operação | Metadata de resposta |
| --- | --- |
| `GET /api/pontos` | `.Produces<IReadOnlyList<PontoDto>>(200)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `POST /api/pontos` | `.Produces<PontoDto>(201)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `PUT /api/pontos/{id:guid}` | `.Produces<PontoDto>(200)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |
| `DELETE /api/pontos/{id:guid}` | `.SemCorpo(204)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |
| `GET /api/trilhas` | `.Produces<IReadOnlyList<TrilhaDto>>(200)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `POST /api/trilhas` | `.Produces<TrilhaDto>(201)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.SemCorpo(429)` |
| `PUT /api/trilhas/{id:guid}` | `.Produces<TrilhaDto>(200)` `.ComErro(400)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |
| `DELETE /api/trilhas/{id:guid}` | `.SemCorpo(204)` `.ComErro(401)` `.ComErro(403)` `.ComErro(404)` `.SemCorpo(429)` |

- [x] **Step 4: Usar o catálogo em Condições**

`local_sem_coordenadas` → `CodigosDeErro.LocalSemCoordenadas`; `coordenadas_invalidas` → `CodigosDeErro.CoordenadasInvalidas`.

- [x] **Step 5: Rodar**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests"`
Expected: `Condições`, `Locais`, `Pontos` e `Trilhas` verdes. Falta só `Sistema` e `Espécies`/`Newsletter`.

- [x] **Step 6: Commit**

```bash
git add src/Fisga.Api/Endpoints/CondicoesEndpoints.cs src/Fisga.Api/Endpoints/PontosEndpoints.cs src/Fisga.Api/Endpoints/TrilhasEndpoints.cs
git commit -m "docs(fisga): respostas reais de condições, locais, pontos e trilhas"
```

---

### Task 7: Respostas de Espécies, Newsletter e Sistema

**Files:**
- Modify: `src/Fisga.Api/Endpoints/VisionEndpoints.cs`
- Modify: `src/Fisga.Api/Endpoints/NewsletterEndpoints.cs`
- Modify: `src/Fisga.Api/Program.cs` (`/health`, `/ready`)
- Modify: `src/Fisga.Api/Contracts/Contracts.cs` (`SaudeResponse`, `ProntidaoResponse`)
- Test: theories `Espécies`, `Newsletter`, `Sistema`; fatos `UploadsDescrevemMultipart` e `SaudeDeclaraCorpoJson`

- [x] **Step 1: Anotar Espécies (multipart)**

`POST /api/especies/sugerir`:

```csharp
.Produces<SugestaoDeEspecieResultado>(200)
.Accepts<FormularioDeFoto>("multipart/form-data")
.ComErro(400)
.ComErro(401)
.ComErro(403)
.SemCorpo(429)
.ComErro(500)
```

Trocar os literais pelos códigos: `SugestaoSemFormulario`, `SugestaoSemFoto`, `SugestaoFotoGrande`, `SugestaoTipoInvalido`.

- [x] **Step 2: Anotar Newsletter**

| Operação | Metadata de resposta |
| --- | --- |
| `POST /api/newsletter/enviar` | `.Produces<ResultadoNewsletter>(200)` `.SemCorpo(404)` `.ComErro(500)` |
| `GET /api/newsletter/preview` | `.Produces<string>(200, "text/html")` `.SemCorpo(404)` `.ComErro(500)` |
| `GET /api/newsletter/descadastrar` | `.SemCorpo(302)` `.SemCorpo(429)` |

- [x] **Step 3: Anotar saúde e prontidão**

Em `Program.cs`:

Ambas as rotas devolvem corpo JSON de verdade. `.Produces(200, null, "application/json")`
faz o gerador descartar o `content` (verificado na Task 3), então o contrato diria
"200 sem corpo" — mentira. Use os contratos tipados:

Em `src/Fisga.Api/Contracts/Contracts.cs`:

```csharp
/// <summary>Corpo de <c>GET /health</c>.</summary>
public sealed record SaudeResponse(string Status, string Version);

/// <summary>Corpo de <c>GET /ready</c>.</summary>
public sealed record ProntidaoResponse(string Status);
```

Em `Program.cs`:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "ok", version = versao }))
    .AllowAnonymous()
    .Produces<SaudeResponse>(200)
    .ComErro(500)
    .WithTags(Tags.Sistema)
    ...

app.MapGet("/ready", async (FisgaDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous()
    .Produces<ProntidaoResponse>(200)
    .SemCorpo(503)
    .ComErro(500)
    .WithTags(Tags.Sistema)
    ...
```

O handler continua devolvendo o objeto anônimo (nenhuma mudança de comportamento);
o record é só a forma publicada. `Version` serializa como `version`, igual ao corpo real.

Adicionar `using Fisga.Api.Contracts;` e `using Fisga.Api.OpenApi;` em `Program.cs`
(o segundo já existe).

Acrescentar em `ContratoTests` o fato `SaudeDeclaraCorpoJson`: `GET /health` e
`GET /ready` têm `200.content["application/json"].schema.$ref` apontando para
`SaudeResponse`/`ProntidaoResponse`. Sem ele, voltar para `.Produces(200, null, …)`
passa despercebido.

- [x] **Step 4: Rodar a suíte de contrato inteira**

Run: `dotnet test Fisga.slnx -c Release --filter "FullyQualifiedName~ContratoTests|FullyQualifiedName~RateLimitTests"`
Expected: todas as theories verdes, incluindo `UploadsDescrevemMultipart` e as 13 tags de `RespostasDeclaradasSaoAsReais`.

- [x] **Step 5: Commit**

```bash
git add src/Fisga.Api/Endpoints/VisionEndpoints.cs src/Fisga.Api/Endpoints/NewsletterEndpoints.cs src/Fisga.Api/Program.cs src/Fisga.Api/Contracts/Contracts.cs tests/Fisga.Api.Tests/ContratoTests.cs
git commit -m "docs(fisga): respostas reais de espécies, newsletter e sistema"
```

---

### Task 8: Contrato regerado, catálogo zerado e documentação sincronizada

**Files:**
- Modify: `docs/api/openapi.json` (gerado)
- Modify: `docs/api/inventario.md`
- Modify: `docs/api/backlog.md`
- Modify: `docs/api/politica-editorial.md`
- Modify: `docs/api/README.md`
- Modify: `docs/guias/como-adicionar-endpoint.md`
- Modify: `docs/api/portal.md` (somente se a linha sobre `429` com corpo ainda existir)
- Test: suíte completa + markdownlint

- [x] **Step 1: Regenerar e auditar o contrato**

```bash
export PATH="$HOME/.dotnet:$PATH"
./scripts/gerar-openapi.sh
python3 - <<'EOF'
import json, collections
d = json.load(open('docs/api/openapi.json'))
ops = [(m.upper(), p, o) for p, item in d['paths'].items() for m, o in item.items()
       if m in ('get', 'post', 'put', 'patch', 'delete')]
print('operações:', len(ops))
print('com 429 e Retry-After:', sum(1 for _, _, o in ops if '429' in o['responses'] and 'Retry-After' in o['responses']['429'].get('headers', {})))
print('com 201/202 e Location:', sum(1 for _, _, o in ops for s in ('201', '202') if s in o['responses'] and 'Location' in o['responses'][s].get('headers', {})))
print('erros fora do schema:', [f'{m} {p}' for m, p, o in ops for s, r in o['responses'].items()
      if int(s) >= 400 and r.get('content') and any(c.get('schema', {}).get('$ref') != '#/components/schemas/ErroResponse' for c in r['content'].values())])
print('multipart:', [f'{m} {p}' for m, p, o in ops if 'multipart/form-data' in o.get('requestBody', {}).get('content', {})])
EOF
```

Expected: 54 operações; 50 com `429` e `Retry-After`; 7 com `Location` (5 `201` + 2 `202`); lista de erros fora do schema vazia; multipart nos dois uploads.

- [x] **Step 2: Zerar o inventário**

Em `docs/api/inventario.md`:

- Remover as linhas **1, 2, 3, 4, 5, 6, 8 e 9** da tabela "Lacunas de contrato"; sobra só a lacuna de `info`/`servers` (F1.7, A3), renumerada para **1**.
- Trocar o aviso do topo por:

```markdown
> **Resolvido no A2 (2026-11-20):** status reais (`201`/`202`/`204`/`302`/`503`),
> schema único de erro (`ErroResponse`) nos dois media types, as descrições de
> erro do catálogo, com `codigo` onde cabe, uploads `multipart/form-data` e
> `Retry-After` no `429` estão no [`openapi.json`](openapi.json). Resta a
> identidade do documento (`info`/`servers`).
```

- Trocar "Confirmadas no `openapi.json` atual. Viram tickets da Fase 1." por uma linha no singular, coerente com a única lacuna restante.
- Renomear `PescariaDto` para `PescariaDetalhadaDto` nas três linhas de pescaria: o nome não existe nem no código nem no artifact.
- Mover o `302` com `ok=0` de `/api/newsletter/descadastrar` da coluna de erros para Nota, como já se fez em `auth/confirmar` e `google/*`.

- Corrigir as linhas de erro que divergiam do handler, conferindo cada uma contra a tabela de `RespostasEsperadas` (as divergências conhecidas, já verificadas):
  - `GET /api/legal/versoes`: `429` (não `500`).
  - `GET /api/catalogo`: `429` (não `500`).
  - `GET /api/auth/confirmar`: `429`, `500`; o "302 com `estado=<codigo>`" sai de "Erros a documentar" e vira Nota (é redirect de sucesso).
  - `GET /api/auth/google/conectar`: acrescentar `403`.
  - `GET /api/auth/google/conectar/concluir`: acrescentar `403`; o "302 com `estado=<codigo>`" vira Nota.
  - `GET /api/pescarias/{shareToken}`, `.../permissao` e `/{id}/editar`: acrescentar `429`.
- No "Resumo de regras transversais", corrigir o parágrafo do rate limit: a rejeição é `429` **sem corpo, com `Retry-After`** em segundos.
- No "Formato de erro", corrigir o parágrafo: handlers escrevem `application/json`, middlewares escrevem `application/problem+json`, e os dois carregam o mesmo `{codigo, detalhe}`. A tabela de origem × status × `codigo` continua válida.
- Atualizar a tabela "Verificação em execução local": a linha do `429` passa a registrar `Retry-After` com o número de segundos (`sem corpo, com Retry-After: <n>`), não mais "sem `Retry-After`".
- Conferir as demais linhas dessa tabela contra o contrato regerado e corrigir qualquer status que tenha mudado (ex.: `google/conectar/concluir` com `403`).

- [x] **Step 3: Marcar o backlog**

Em `docs/api/backlog.md`, acrescentar `**Estado:** concluída no A2 (2026-11-20) — esta PR fecha #64.` em F1.2, `#65` em F1.3, `#66` em F1.5 e `#69` em F1.8 (uma linha por item, no mesmo formato de F1.4/F1.6).

Ajustar o texto que o A2 tornou falso ou que descreve o caminho descartado:

- F1.3, escopo: trocar "`ProducesProblem` por status em cada operação" por
  "`Respostas.ComErro` por status em cada operação (`ProducesProblem` foi
  descartado: publica o schema `ProblemDetails` do framework, não o corpo real)".
- F1.3, aceite: trocar "`429` documentado como corpo vazio" por "`429`
  documentado como corpo vazio com `Retry-After`".
- F1.2, aceite: trocar "teste de contrato compara spec × handler para os quatro
  `POST` de criação" por "teste de contrato compara a tabela congelada com o
  documento gerado nas 54 operações".
- F1.8: riscar o "portal documenta o header" só se o portal ainda não o fizer;
  caso contrário manter.

- [x] **Step 4: Atualizar o README do contrato**

Em `docs/api/README.md`, reescrever a seção "Limites conhecidos":

- Manter o bullet "Só existe em Development".
- Substituir os dois bullets finais por um que descreva o que o contrato já expressa: status reais e corpo tipado de sucesso quando há corpo, `ErroResponse` em `application/json` e `application/problem+json`, `multipart/form-data` nos dois uploads, `Retry-After` no `429`, `Location` em `201`/`202`; e o que ainda falta: `info`/`servers` (F1.7).
- Acrescentar na seção "De onde vem o documento" que as respostas vêm de `Produces<T>` + `Respostas.ComErro`/`SemCorpo` e são normalizadas por `ContratoOperationTransformer`.

- [x] **Step 5: Atualizar o guia de endpoint**

Em `docs/guias/como-adicionar-endpoint.md`, seção 7, inserir antes do passo 3:

```markdown
3. Declare as respostas: `.Produces<T>(status)` para o sucesso, `.ComErro(status)`
   para erro com corpo e `.SemCorpo(status)` para `204`/`302`/`429`/`503`.
   Upload usa `.Accepts<FormularioDeFoto>("multipart/form-data")`. O status
   declarado precisa ser o que o handler devolve — `ContratoTests` compara a
   tabela de `RespostasEsperadas` com o documento gerado.
4. Registre a operação em `tests/Fisga.Api.Tests/RespostasEsperadas.cs` com os
   status reais e, se a rota for pública, em `RotasAnonimasPorDecisao`
   (`ContratoTests.cs`). Um `404` com corpo vazio (em vez de `ErroResponse`)
   entra em `RespostasEsperadas.NotFoundSemCorpo`.
```

Renumerar o passo de regeneração para 5.

- [x] **Step 6: Conferir o portal e a política editorial**

Em `docs/api/portal.md`, a linha 158 cita "`429` com corpo" apenas como erro do protótipo; nenhuma mudança é necessária. Se houver qualquer outra frase dizendo que o spec não declara status/erros, atualizar.

Em `docs/api/politica-editorial.md`, o bullet do `429` (linha ~119) diz "sem `Retry-After`", o que deixa de ser verdade: reescrever para "**`429` sem corpo, com `Retry-After`.** O rate limit responde vazio; o header informa os segundos até a próxima janela; o texto diz o limite e a janela." Na mesma seção, corrigir "O corpo de erro real é `application/problem+json`" para registrar os dois media types: `application/json` nos handlers e `application/problem+json` nos middlewares, com o mesmo corpo `{codigo, detalhe}`. Registrar a exceção: os dois `503 google_indisponivel` (`/api/auth/google/iniciar` e `/api/auth/google/conectar`) já saem `application/problem+json` de dentro do handler.

- [x] **Step 7: Rodar tudo**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build Fisga.slnx -c Release -warnaserror
dotnet test Fisga.slnx -c Release --no-build
cd ../.. && npx --yes markdownlint-cli2@0.24.1
```

Expected: build sem warnings; suíte inteira verde, incluindo as 13 theories de `RespostasDeclaradasSaoAsReais`, `ErrosUsamUmUnicoSchema`, `UploadsDescrevemMultipart`, `SaudeDeclaraCorpoJson`, `NormalizacaoDeRespostasTests` e `RateLimitTests`; markdownlint 0 issues na raiz do repo.

- [x] **Step 8: Commit**

```bash
git add docs/api docs/guias docs/superpowers
git commit -m "docs(fisga): contrato A2 regerado e inventário sem as lacunas de resposta"
```

---

### Task 9: Correção pós-revisão (2026-11-20)

Correções feitas depois da revisão do pacote A2, no commit `3323fbf`.

**O plano errou no `413`.** A decisão 3 tratava `413` como status de corpo vazio
do Kestrel nos dois uploads. Medição no Kestrel real: corpo de 32 MB em
`POST /api/midia` e `POST /api/especies/sugerir` devolve
`500 {"codigo":"erro_inesperado"}`. O `BadHttpRequestException` (413) nasce na
leitura do formulário e é capturado pelo `catch (Exception)` do
`ExceptionHandlingMiddleware`, que responde o `500` genérico. A API nunca
devolve `413` — documentá-lo publicava um status que não acontece. Removido de
`POST /api/midia` (a tabela de `RespostasEsperadas` congela
`[200, 400, 401, 403, 429]`) e do `openapi.json` regerado. O catálogo
`RespostasPadrao` mantém a descrição do `413` para uma operação futura que
realmente o devolva.

**Saúde com tipo.** `/health` e `/ready` passam a devolver `SaudeResponse` e
`ProntidaoResponse` e a declará-los com `Produces<T>` no handler; o JSON é
byte a byte o mesmo (conferido por hash), então a mudança é só de contrato.

**Dois fatos novos, cobertos por teste.** Todo 2xx com corpo declara schema
(os `204`/`302` continuam sem corpo), e as quatro criações — `/api/pescarias`,
`/api/locais`, `/api/pontos` e `/api/trilhas` — declaram o DTO que devolvem
(`PescariaSalvaResponse`, `LocalDto`, `PontoDto`, `TrilhaDto`), em vez de um
`201` sem tipo.

**Documentação.** `docs/api/inventario.md` (sem `413`, nota de corpo acima de
30 MB, aviso do A2 com `codigo` onde cabe), `docs/api/README.md` (corpo tipado
quando há corpo; schemas de sucesso vindos de `Fisga.Application`), o guia de
endpoint (sem `413`; registro em `RespostasEsperadas`/`RotasAnonimasPorDecisao`
e `NotFoundSemCorpo`) e o `docs/INDEX.md`, que passa a indexar este plano.

#### Segunda rodada da revisão (PR-Agent, 2026-11-20)

**A regra do `500` mentia sobre o banco.** O `<remarks>` de
`RespostasEsperadas` dizia que o `500` entra "onde a operação tem caminho de
falha próprio (provedor externo, banco, saúde)". "Banco" lê como qualquer
persistência, o que poria `500` em quase toda escrita — mas a tabela só o tem
em `/api/condicoes*`, `/api/especies/sugerir`, `/api/auth/confirmar` e nas duas
rotas de newsletter. A regra agora nomeia o caminho real (chamada a provedor
externo e a invariante pós-confirmação) e diz explicitamente que exceção de
persistência é o `500` genérico do `ExceptionHandlingMiddleware`, não um status
da operação.

**`/health` não tinha caminho de `500`.** O handler só serializa
`VersaoDaAplicacao.Resolver`; o `500` declarado ali era o genérico, que a regra
diz para não enumerar. Saiu do handler e da tabela (`[200]`). Mesma conclusão
para `/ready`: `CanConnectAsync` trata a falha de banco e o handler responde
`503` sem corpo, então o `500` da linha também era só o genérico — removido
(a linha fica `[200, 503]`), e o `503` continua declarado. `inventario.md`
acompanhou: as duas linhas de Sistema & Legal perderam o `500` e o texto diz que
o único `500` possível ali é o transversal.

**Os dois `503` se contradiziam.** `DescricaoComErro(503)` dizia "Serviço
indisponível no momento." e `DescricaoSemSchema(503)` dizia "Corpo vazio." O
leitor não sabia se havia corpo. A descrição com corpo agora diz o que publica:
"Serviço indisponível no momento. Corpo `ErroResponse` com o motivo." A sem
corpo continua sendo a de `/ready`.

---

## Verificação final do pacote

- [x] `git diff origin/main...HEAD --stat` limitado a `Projetos/fisga/**`.
- [x] Nenhuma mudança de comportamento: revisar o diff dos handlers e confirmar que só entraram linhas `.Produces`/`.ComErro`/`.SemCorpo`/`.Accepts`, constantes de código e o `Retry-After`. Nenhum `Results.*` mudou de status.
- [x] `grep -rn 'new ErroResponse("' src/Fisga.Api` não retorna nada (todo código vem de `CodigosDeErro`).
- [x] `grep -rn "LimitesPadraoPorMinuto" src tests` não retorna nada (fonte única).
- [x] `openapi.json` com 54 operações, 54 `operationId`, 13 tags, 50 `x-rate-limit`, 54 `x-cache`, 50 `429` com `Retry-After`, dois `multipart/form-data`.
- [x] `/health` e `/ready` fora da contagem de `429` (isentos) e `/ready` com `503` sem corpo.
- [ ] PR com `Closes #64`, `Closes #65`, `Closes #66`, `Closes #69`; corpo lista o que mudou, como verificar e o desvio consciente de `ProducesProblem` → `ErroResponse`.
