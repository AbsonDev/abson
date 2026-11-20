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
    public const string DescricaoDoHeader =
        "Segundos até a próxima janela de requisições.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opcoes.OnRejected = EscreverAsync;
    }

    public static ValueTask EscreverAsync(OnRejectedContext contexto, CancellationToken ct)
    {
        var segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
            ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
            : PoliticasDeTaxa.JanelaEmSegundos;

        contexto.HttpContext.Response.Headers.RetryAfter =
            segundos.ToString(CultureInfo.InvariantCulture);

        return ValueTask.CompletedTask;
    }
}
