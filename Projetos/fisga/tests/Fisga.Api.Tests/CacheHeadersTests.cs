using System.Net;
using Microsoft.AspNetCore.Http;
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

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    public void Decisao_de_erro_e_sempre_no_store(int status)
    {
        Assert.Equal(
            "private, no-store",
            DecisaoDeCache.Valor(status, "public, max-age=86400"));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(206)]
    [InlineData(304)]
    public void Decisao_de_sucesso_preserva_a_politica(int status)
    {
        Assert.Equal(
            "public, max-age=60",
            DecisaoDeCache.Valor(status, "public, max-age=60"));
    }

    [Theory]
    [InlineData("/api/catalogo", "public, max-age=86400")]
    [InlineData("/api/legal/versoes", "public, max-age=86400")]
    [InlineData("/api/condicoes", "private, max-age=300, stale-while-revalidate=600")]
    [InlineData("/api/locais/reverso", "private, max-age=86400")]
    [InlineData("/api/locais/buscar", "private, max-age=3600")]
    [InlineData("/api/pescarias/{shareToken}", "private, max-age=30")]
    [InlineData("/api/pescarias/{shareToken}/pdf", "private, max-age=300")]
    public void Politicas_dos_sete_endpoints_estao_declaradas(string rota, string esperado)
    {
        var endpoint = Localizar(rota);
        Assert.Equal(esperado, endpoint.Metadata.GetMetadata<CachePolicy>()?.HeaderValue);
    }

    [Fact]
    public void Endpoints_sem_politica_ficam_no_store_por_padrao()
    {
        // O grupo /api/locais é registrado com o padrão vazio e o ASP.NET Core
        // materializa a rota como "/api/locais/" (com barra final). GET e POST
        // compartilham o mesmo texto; a listagem é o GET.
        var locais = Localizar("/api/locais/", "GET");

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

    [Fact]
    public async Task Erros_400_e_404_respondem_no_store_mesmo_com_politica_positiva()
    {
        var cliente = _fixture.Factory.CreateClient();

        // /api/locais/reverso tem private, max-age=86400 na política. Sem
        // coordenadas ele responde 400; esse erro não pode herdar o TTL.
        var reverso = await cliente.GetAsync("/api/locais/reverso");
        Assert.Equal(HttpStatusCode.BadRequest, reverso.StatusCode);
        Assert.Equal("private, no-store",
            reverso.Headers.NonValidated["Cache-Control"].ToString());

        // A pescaria compartilhada tem private, max-age=30. Um token inexistente
        // responde 404 e também não pode ser cacheado.
        var pescaria = await cliente.GetAsync($"/api/pescarias/nao-existe-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, pescaria.StatusCode);
        Assert.Equal("private, no-store",
            pescaria.Headers.NonValidated["Cache-Control"].ToString());
    }

    [Fact]
    public async Task Media_preserva_cache_publico_e_erro_nao_e_cacheado()
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "media");
        Directory.CreateDirectory(pasta);
        var nome = $"teste-cache-{Guid.NewGuid():N}.txt";
        var caminho = Path.Combine(pasta, nome);
        await File.WriteAllTextAsync(caminho, "ok");

        try
        {
            var cliente = _fixture.Factory.CreateClient();

            var arquivo = await cliente.GetAsync($"/media/{nome}");
            Assert.Equal(HttpStatusCode.OK, arquivo.StatusCode);
            Assert.Equal("public,max-age=31536000,immutable",
                arquivo.Headers.NonValidated["Cache-Control"].ToString());

            var ausente = await cliente.GetAsync($"/media/nao-existe-{Guid.NewGuid():N}.png");
            Assert.Equal(HttpStatusCode.NotFound, ausente.StatusCode);
            Assert.Equal("private, no-store",
                ausente.Headers.NonValidated["Cache-Control"].ToString());
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    private RouteEndpoint Localizar(string rota, string? metodo = null)
    {
        var endpoints = _fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        return endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == rota
                         && (metodo is null
                             || e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(metodo) == true));
    }
}
