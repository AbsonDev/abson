using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class LegacyCookieStartupGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fisga_auth")]
    [InlineData("FISGA_AUTH")]
    [InlineData("Fisga_Auth")]
    public void Nome_legado_vazio_ou_igual_ao_cookie_da_identity_falha(string? nome)
    {
        var excecao = Assert.Throws<InvalidOperationException>(() =>
            ConfiguracaoDeAutenticacao.Validar(nome));

        Assert.Contains("LegacyCookieName", excecao.Message);
    }

    [Theory]
    [InlineData("fisga_sessao")]
    [InlineData("fisga_sessao_antigo")]
    public void Nome_legado_padrao_e_variacoes_seguras_sao_aceitos(string nome)
    {
        ConfiguracaoDeAutenticacao.Validar(nome);
    }

    [Fact]
    public async Task Host_de_development_com_default_sobe()
    {
        using var client = _fixture.Factory.CreateClient();

        var resposta = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Host_de_producao_com_default_sobe()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"fisga-prod-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pasta);

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Production");
                    builder.ConfigureAppConfiguration((_, configuracao) =>
                        configuracao.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Email:Provider"] = "Console",
                            ["Auth:CookieSecure"] = "false",
                            ["DataProtection:KeysPath"] = Path.Combine(pasta, "keys"),
                            ["Media:Path"] = Path.Combine(pasta, "media"),
                            ["Sentry:Dsn"] = ""
                        }));
                });

            using var client = factory.CreateClient();
            var resposta = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }
        finally
        {
            if (Directory.Exists(pasta))
            {
                Directory.Delete(pasta, recursive: true);
            }
        }
    }

    [Fact]
    public void Host_de_producao_com_legacy_cookie_fisga_auth_falha_ao_subir()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, configuracao) =>
                    configuracao.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Auth:LegacyCookieName"] = "fisga_auth"
                    }));
            });

        var excecao = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("LegacyCookieName", excecao.ToString());
        Assert.Contains("fisga_auth", excecao.ToString());
    }

    private readonly ApiTestFixture _fixture;

    public LegacyCookieStartupGuardTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
    }
}
