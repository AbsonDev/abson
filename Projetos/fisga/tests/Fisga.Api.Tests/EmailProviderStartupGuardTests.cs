using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class EmailProviderStartupGuardTests
{
    [Fact]
    public void Producao_com_console_falha_ao_subir()
    {
        using var factory = CriarProducao(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Console"
        });

        var excecao = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Email:Provider", excecao.ToString());
        Assert.Contains("Resend", excecao.ToString());
    }

    [Fact]
    public void Producao_com_resend_sem_chave_falha_ao_subir()
    {
        using var factory = CriarProducao(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Resend",
            ["Email:ResendApiKey"] = ""
        });

        var excecao = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ResendApiKey", excecao.ToString());
    }

    [Fact]
    public async Task Producao_com_resend_e_chave_sobe()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"fisga-resend-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pasta);

        try
        {
            using var factory = CriarProducao(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Resend",
                ["Email:ResendApiKey"] = "chave-de-teste",
                ["Email:From"] = "Fisga <acesso@abson.tech>",
                ["DataProtection:KeysPath"] = Path.Combine(pasta, "keys"),
                ["Media:Path"] = Path.Combine(pasta, "media"),
                ["Sentry:Dsn"] = ""
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

    private static WebApplicationFactory<Program> CriarProducao(Dictionary<string, string?> configuracoes)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, configuracao) =>
                    configuracao.AddInMemoryCollection(configuracoes));
            });
    }
}
