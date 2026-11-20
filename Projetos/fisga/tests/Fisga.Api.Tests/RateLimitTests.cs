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
