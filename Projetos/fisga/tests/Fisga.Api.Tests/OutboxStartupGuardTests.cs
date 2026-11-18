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
            ConfiguracaoDeEmail.Validar("Outbox", resendApiKey: null, desenvolvimento: false));

        Assert.Contains("Outbox", excecao.Message);
        Assert.Contains("Development", excecao.Message);
    }

    [Theory]
    [InlineData("outbox")]
    [InlineData("OUTBOX")]
    [InlineData("Outbox")]
    public void Outbox_em_development_e_permitido(string provedor)
    {
        ConfiguracaoDeEmail.Validar(provedor, resendApiKey: null, desenvolvimento: true);
    }

    [Fact]
    public void Resend_com_chave_e_aceito_em_producao()
    {
        ConfiguracaoDeEmail.Validar("Resend", "chave-de-teste", desenvolvimento: false);
    }

    [Theory]
    [InlineData("Console")]
    [InlineData("Outbox")]
    [InlineData(null)]
    [InlineData("Resend")]
    public void Producao_sem_resend_e_chave_falha(string? provedor)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ConfiguracaoDeEmail.Validar(provedor, resendApiKey: null, desenvolvimento: false));
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
