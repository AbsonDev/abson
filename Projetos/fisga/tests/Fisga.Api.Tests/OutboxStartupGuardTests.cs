using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class OutboxStartupGuardTests
{
    [Fact]
    public void Outbox_em_producao_e_recusado()
    {
        var excecao = Assert.Throws<InvalidOperationException>(() =>
            ConfiguracaoDeEmail.Validar("Outbox", desenvolvimento: false));

        Assert.Contains("Outbox", excecao.Message);
        Assert.Contains("Development", excecao.Message);
    }

    [Theory]
    [InlineData("outbox")]
    [InlineData("OUTBOX")]
    [InlineData("Outbox")]
    public void Outbox_em_development_e_permitido(string provedor)
    {
        ConfiguracaoDeEmail.Validar(provedor, desenvolvimento: true);
    }

    [Theory]
    [InlineData("Resend")]
    [InlineData("Console")]
    [InlineData(null)]
    public void Outros_provedores_seguem_normais_em_producao(string? provedor)
    {
        ConfiguracaoDeEmail.Validar(provedor, desenvolvimento: false);
    }

    [Fact]
    public void Host_de_producao_com_outbox_falha_ao_subir()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, configuracao) =>
                    configuracao.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Email:Provider"] = "Outbox",
                        ["Email:OutboxPath"] = Path.Combine(
                            Path.GetTempPath(),
                            $"fisga-outbox-startup-{Guid.NewGuid():N}")
                    }));
            });

        var excecao = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Outbox", excecao.ToString());
        Assert.Contains("Development", excecao.ToString());
    }
}
