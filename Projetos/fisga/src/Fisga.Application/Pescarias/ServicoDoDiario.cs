using Fisga.Application.Abstractions;
using Fisga.Application.Catalogo;
using Fisga.Domain.Common;

namespace Fisga.Application.Pescarias;

public sealed class ServicoDoDiario(
    IPescariaRepository repositorio,
    ICatalogoDePesca catalogo)
{
    public async Task<DiarioDto> ObterAsync(string diarioToken, CancellationToken ct = default)
    {
        var hash = ServicoDePescarias.HashDoDiario(diarioToken);
        var pescarias = await repositorio.ListarPorDiarioAsync(hash, ct);

        var capturas = pescarias
            .SelectMany(pescaria => pescaria.Capturas)
            .ToList();

        var resumos = pescarias
            .OrderByDescending(pescaria => pescaria.Data)
            .ThenByDescending(pescaria => pescaria.CriadoEm)
            .Select(pescaria => new PescariaResumoDto(
                pescaria.Id,
                pescaria.ShareToken,
                pescaria.Local,
                pescaria.Data,
                pescaria.Ambiente.ToString(),
                catalogo.NomeDe(pescaria.Ambiente),
                pescaria.Capturas.Count,
                pescaria.Capturas.Count == 0 ? 0 : pescaria.Capturas.Max(captura => captura.PesoKg),
                pescaria.Capturas.FirstOrDefault(captura => captura.FotoUrl is not null)?.FotoUrl))
            .ToList();

        var especieTop = capturas
            .Where(captura => !string.IsNullOrWhiteSpace(captura.Especie))
            .GroupBy(captura => captura.Especie, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(grupo => grupo.Count())
            .ThenByDescending(grupo => grupo.Max(captura => captura.PesoKg))
            .ThenBy(grupo => grupo.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(grupo => grupo.Key)
            .FirstOrDefault();

        var iscaTop = capturas
            .Where(captura => !string.IsNullOrWhiteSpace(captura.Isca))
            .GroupBy(captura => captura.Isca!, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(grupo => grupo.Count())
            .ThenByDescending(grupo => grupo.Max(captura => captura.PesoKg))
            .ThenBy(grupo => grupo.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(grupo => grupo.Key)
            .FirstOrDefault();

        var localTop = pescarias
            .Where(pescaria => pescaria.Capturas.Count > 0)
            .GroupBy(pescaria => pescaria.Local, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(grupo => grupo.Sum(pescaria => pescaria.Capturas.Count))
            .ThenBy(grupo => grupo.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(grupo => grupo.Key)
            .FirstOrDefault();

        return new DiarioDto(
            TotalPescarias: pescarias.Count,
            TotalCapturas: capturas.Count,
            EspeciesDistintas: capturas
                .Select(captura => captura.Especie)
                .Where(especie => !string.IsNullOrWhiteSpace(especie))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Count(),
            TotalSoltos: capturas.Count(captura => captura.Solto),
            MaiorPesoKg: capturas.Count == 0 ? 0 : capturas.Max(captura => captura.PesoKg),
            MaiorComprimentoCm: capturas.Count == 0 ? 0 : capturas.Max(captura => captura.ComprimentoCm),
            EspecieTop: especieTop,
            IscaTop: iscaTop,
            LocalTop: localTop,
            Pescarias: resumos);
    }
}
