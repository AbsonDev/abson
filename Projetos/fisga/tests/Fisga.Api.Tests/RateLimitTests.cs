using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Fisga.Api.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace Fisga.Api.Tests;

// O host compartilhado da collection neutraliza as políticas (GetNoLimiter), então
// a rejeição isolada é verificada em leases construídas à mão; o pipeline real,
// com a configuração do Program, é verificado em um host sem essa neutralização.
[Collection("api")]
public sealed class RateLimitTests
{
    private readonly ApiTestFixture _fixture;

    public RateLimitTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RejeicaoEscreveRetryAfterEmSegundosInteiros()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
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
        contexto.Response.Body = new MemoryStream();

        await RejeicaoDoRateLimiter.EscreverAsync(
            new OnRejectedContext { HttpContext = contexto, Lease = new LeaseFalso(null) },
            CancellationToken.None);

        Assert.Equal(
            PoliticasDeTaxa.JanelaEmSegundos.ToString(CultureInfo.InvariantCulture),
            contexto.Response.Headers.RetryAfter.ToString());
        Assert.Equal(0, contexto.Response.Body.Length);
    }

    [Fact]
    public async Task RejeicaoNuncaPublicaZeroSegundos()
    {
        var contexto = new DefaultHttpContext();

        await RejeicaoDoRateLimiter.EscreverAsync(
            new OnRejectedContext
            {
                HttpContext = contexto,
                Lease = new LeaseFalso(TimeSpan.FromSeconds(0.2))
            },
            CancellationToken.None);

        Assert.Equal("1", contexto.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void LimiteDeUsaConfiguracaoQuandoPresente()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"RateLimiting:{PoliticasDeTaxa.Auth}"] = "3"
            })
            .Build();

        Assert.Equal(3, PoliticasDeTaxa.LimiteDe(configuracao, PoliticasDeTaxa.Auth));
    }

    [Fact]
    public void LimiteDeUsaOPadraoEPoliticaDesconhecidaLanca()
    {
        var configuracao = new ConfigurationBuilder().Build();

        Assert.Equal(
            PoliticasDeTaxa.PadroesPorMinuto[PoliticasDeTaxa.Auth],
            PoliticasDeTaxa.LimiteDe(configuracao, PoliticasDeTaxa.Auth));

        var semContexto = Assert.Throws<InvalidOperationException>(
            () => PoliticasDeTaxa.LimiteDe(configuracao, "inexistente"));
        Assert.Equal("Política sem limite conhecido: inexistente", semContexto.Message);

        var comContexto = Assert.Throws<InvalidOperationException>(
            () => PoliticasDeTaxa.LimiteDe(configuracao, "inexistente", "POST /api/x"));
        Assert.Equal("Política sem limite conhecido: inexistente (POST /api/x)", comContexto.Message);
    }

    // Host com a configuração real do Program: o rate limiter não é neutralizado,
    // então este teste falha se Program deixar de registrar o callback de rejeição.
    [Fact]
    public async Task HostRealRejeitaCom429ERetryAfterSemCorpo()
    {
        using var factory = new TestWebApplicationFactory(_fixture.DbPath, neutralizarRateLimit: false);
        using var cliente = factory.CreateClient();

        // A política "auth" real é de 8 requisições por minuto: as oito primeiras
        // passam pelo limiter e falham na validação do corpo vazio, sem chegar ao
        // handler; a nona é rejeitada antes de qualquer leitura do banco.
        for (var tentativa = 1; tentativa <= 8; tentativa++)
        {
            using var resposta = await cliente.PostAsync(
                "/api/auth/entrar",
                new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, resposta.StatusCode);
        }

        using var rejeitada = await cliente.PostAsync(
            "/api/auth/entrar",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejeitada.StatusCode);
        Assert.True(
            rejeitada.Headers.TryGetValues("Retry-After", out var valores),
            "429 sem o header Retry-After");
        var retryAfter = Assert.Single(valores);
        Assert.True(
            int.TryParse(retryAfter, NumberStyles.None, CultureInfo.InvariantCulture, out var segundos),
            $"Retry-After não é um inteiro: {retryAfter}");
        Assert.InRange(segundos, 1, PoliticasDeTaxa.JanelaEmSegundos);
        Assert.Equal(string.Empty, await rejeitada.Content.ReadAsStringAsync());
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
