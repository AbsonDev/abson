using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Caching;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class CacheHeadersTests
{
    private readonly ApiTestFixture _fixture;

    public CacheHeadersTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Endpoints_publicos_e_privados_tem_politica_correta()
    {
        var endpoints = _fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var condicoes = endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/condicoes");
        var catalogo = endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/catalogo");
        // O grupo /api/locais é registrado com o padrão vazio e o ASP.NET Core
        // materializa a rota como "/api/locais/" (com barra final). GET e POST
        // compartilham o mesmo texto; a listagem é o GET.
        var locais = endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/locais/"
                         && e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);

        Assert.Equal("private, max-age=300, stale-while-revalidate=600",
            condicoes.Metadata.GetMetadata<CachePolicy>()?.HeaderValue);
        Assert.Equal("public, max-age=86400",
            catalogo.Metadata.GetMetadata<CachePolicy>()?.HeaderValue);
        Assert.Null(locais.Metadata.GetMetadata<CachePolicy>());
    }

    [Fact]
    public async Task Catalogo_responde_publico_e_auth_me_responde_no_store()
    {
        var cliente = _fixture.Factory.CreateClient();
        var catalogo = await cliente.GetAsync("/api/catalogo");
        var auth = await cliente.GetAsync("/api/auth/me");

        // Lemos o header bruto (NonValidated): HttpClient reordena as diretivas ao
        // formatar CacheControlHeaderValue ("private, no-store" -> "no-store, private"),
        // mas o que navegador e CDN veem é o header na ordem gravada pela API.
        Assert.Equal("public, max-age=86400",
            catalogo.Headers.NonValidated["Cache-Control"].ToString());
        Assert.Equal("private, no-store",
            auth.Headers.NonValidated["Cache-Control"].ToString());
    }
}
