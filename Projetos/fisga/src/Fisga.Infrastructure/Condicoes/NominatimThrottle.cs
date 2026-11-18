using System.Diagnostics;

namespace Fisga.Infrastructure.Condicoes;

public sealed class NominatimThrottle
{
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim _porta = new(1, 1);
    private long _ultimoInicio;

    /// <summary>
    /// Executa a ação segurando a porta do throttle: garante ao menos 1 s entre
    /// inícios e impede duas requisições simultâneas ao Nominatim, mesmo para
    /// chaves de cache distintas. A porta só é liberada no <c>finally</c>, depois
    /// que o HTTP terminou.
    /// </summary>
    public async Task<T> ExecutarAsync<T>(
        Func<CancellationToken, Task<T>> acao,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(acao);

        await _porta.WaitAsync(ct);
        try
        {
            if (_ultimoInicio != 0)
            {
                var espera = IntervaloMinimo - Stopwatch.GetElapsedTime(_ultimoInicio);
                if (espera > TimeSpan.Zero)
                {
                    await Task.Delay(espera, ct);
                }
            }

            _ultimoInicio = Stopwatch.GetTimestamp();
            return await acao(ct);
        }
        finally
        {
            _porta.Release();
        }
    }
}
