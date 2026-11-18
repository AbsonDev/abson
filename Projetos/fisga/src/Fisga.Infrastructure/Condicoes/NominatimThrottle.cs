using System.Diagnostics;

namespace Fisga.Infrastructure.Condicoes;

public sealed class NominatimThrottle
{
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim _porta = new(1, 1);
    private long _ultimoInicio;

    public async ValueTask AguardarAsync(CancellationToken ct = default)
    {
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
        }
        finally
        {
            _porta.Release();
        }
    }
}
