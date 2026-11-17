using Fisga.Application.Abstractions;
using System.Globalization;
using Fisga.Application.Catalogo;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Pescarias;

public sealed class ServicoDoDiario(
    IPescariaRepository repositorio,
    ICatalogoDePesca catalogo)
{
    public async Task<DiarioDto> ObterAsync(Guid usuarioId, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new NaoAutenticadoException();
        }

        var pescarias = await repositorio.ListarPorUsuarioAsync(usuarioId, ct);

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

        var totalSoltos = capturas.Count(captura => captura.Solto);
        var taxaDeSoltura = capturas.Count == 0
            ? 0
            : Math.Round((decimal)totalSoltos / capturas.Count * 100, 1);

        var pares = pescarias
            .SelectMany(pescaria => pescaria.Capturas.Select(captura => new { Captura = captura, Pescaria = pescaria }))
            .ToList();

        var insights = new List<InsightDto>();

        if (pares.Count > 0)
        {
            var melhorMes = pares
                .GroupBy(par => par.Pescaria.Data.Month)
                .Select(grupo => new
                {
                    Mes = grupo.Key,
                    Capturas = grupo.Count(),
                    Peso = grupo.Sum(par => par.Captura.PesoKg)
                })
                .OrderByDescending(item => item.Capturas)
                .ThenByDescending(item => item.Peso)
                .ThenBy(item => item.Mes)
                .First();

            insights.Add(new InsightDto(
                "Melhor mês",
                Capitalizar(CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetMonthName(melhorMes.Mes)),
                $"{melhorMes.Capturas} capturas"));

            var melhorPeriodo = pares
                .GroupBy(par => par.Pescaria.Periodo)
                .Select(grupo => new
                {
                    Periodo = grupo.Key,
                    Capturas = grupo.Count(),
                    Peso = grupo.Sum(par => par.Captura.PesoKg)
                })
                .OrderByDescending(item => item.Capturas)
                .ThenByDescending(item => item.Peso)
                .First();

            insights.Add(new InsightDto(
                "Melhor período",
                catalogo.NomeDe(melhorPeriodo.Periodo),
                $"{melhorPeriodo.Capturas} capturas"));

            var melhorLua = pares
                .Where(par => par.Pescaria.Lua != FaseDaLua.NaoInformada)
                .GroupBy(par => par.Pescaria.Lua)
                .Select(grupo => new
                {
                    Lua = grupo.Key,
                    Capturas = grupo.Count(),
                    Peso = grupo.Sum(par => par.Captura.PesoKg)
                })
                .OrderByDescending(item => item.Capturas)
                .ThenByDescending(item => item.Peso)
                .FirstOrDefault();

            if (melhorLua is not null)
            {
                insights.Add(new InsightDto(
                    "Melhor lua",
                    catalogo.NomeDe(melhorLua.Lua),
                    $"{melhorLua.Capturas} capturas"));
            }

            var melhorMare = pares
                .Where(par => par.Pescaria.Mare != FaseDaMare.NaoSeAplica)
                .GroupBy(par => par.Pescaria.Mare)
                .Select(grupo => new
                {
                    Mare = grupo.Key,
                    Capturas = grupo.Count(),
                    Peso = grupo.Sum(par => par.Captura.PesoKg)
                })
                .OrderByDescending(item => item.Capturas)
                .ThenByDescending(item => item.Peso)
                .FirstOrDefault();

            if (melhorMare is not null)
            {
                insights.Add(new InsightDto(
                    "Melhor maré",
                    catalogo.NomeDe(melhorMare.Mare),
                    $"{melhorMare.Capturas} capturas"));
            }

            var melhorClima = pares
                .Where(par => par.Pescaria.Clima != Clima.NaoInformado)
                .GroupBy(par => par.Pescaria.Clima)
                .Select(grupo => new
                {
                    Clima = grupo.Key,
                    Capturas = grupo.Count(),
                    Peso = grupo.Sum(par => par.Captura.PesoKg)
                })
                .OrderByDescending(item => item.Capturas)
                .ThenByDescending(item => item.Peso)
                .FirstOrDefault();

            if (melhorClima is not null)
            {
                insights.Add(new InsightDto(
                    "Melhor clima",
                    catalogo.NomeDe(melhorClima.Clima),
                    $"{melhorClima.Capturas} capturas"));
            }

            var melhorAgua = pares
                .Where(par => par.Pescaria.ClarezaDaAgua != ClarezaDaAgua.NaoInformada)
                .GroupBy(par => par.Pescaria.ClarezaDaAgua)
                .Select(grupo => new
                {
                    Clareza = grupo.Key,
                    Capturas = grupo.Count(),
                    Peso = grupo.Sum(par => par.Captura.PesoKg)
                })
                .OrderByDescending(item => item.Capturas)
                .ThenByDescending(item => item.Peso)
                .FirstOrDefault();

            if (melhorAgua is not null)
            {
                insights.Add(new InsightDto(
                    "Melhor água",
                    catalogo.NomeDe(melhorAgua.Clareza),
                    $"{melhorAgua.Capturas} capturas"));
            }
        }

        var recordesPorEspecie = capturas
            .Where(captura => !string.IsNullOrWhiteSpace(captura.Especie))
            .GroupBy(captura => captura.Especie, StringComparer.CurrentCultureIgnoreCase)
            .Select(grupo => new RecordePorEspecieDto(
                grupo.Key,
                grupo.Max(captura => captura.PesoKg),
                grupo.Max(captura => captura.ComprimentoCm),
                grupo.Count()))
            .OrderByDescending(recorde => recorde.PesoKg)
            .ThenByDescending(recorde => recorde.ComprimentoCm)
            .ThenBy(recorde => recorde.Especie, StringComparer.CurrentCultureIgnoreCase)
            .Take(5)
            .ToList();

        var iscasPorEspecie = capturas
            .Where(captura => !string.IsNullOrWhiteSpace(captura.Isca))
            .GroupBy(captura => captura.Especie, StringComparer.CurrentCultureIgnoreCase)
            .Select(grupo =>
            {
                var isca = grupo
                    .GroupBy(captura => captura.Isca!, StringComparer.CurrentCultureIgnoreCase)
                    .Select(subgrupo => new
                    {
                        Nome = subgrupo.Key,
                        Capturas = subgrupo.Count(),
                        Peso = subgrupo.Max(captura => captura.PesoKg)
                    })
                    .OrderByDescending(item => item.Capturas)
                    .ThenByDescending(item => item.Peso)
                    .ThenBy(item => item.Nome, StringComparer.CurrentCultureIgnoreCase)
                    .FirstOrDefault();

                return new
                {
                    Especie = grupo.Key,
                    Capturas = grupo.Count(),
                    Isca = isca
                };
            })
            .Where(item => item.Isca is not null)
            .OrderByDescending(item => item.Capturas)
            .ThenBy(item => item.Especie, StringComparer.CurrentCultureIgnoreCase)
            .Take(5)
            .Select(item => new IscaPorEspecieDto(item.Especie, item.Isca!.Nome, item.Isca.Capturas))
            .ToList();

        return new DiarioDto(
            TotalPescarias: pescarias.Count,
            TotalCapturas: capturas.Count,
            EspeciesDistintas: capturas
                .Select(captura => captura.Especie)
                .Where(especie => !string.IsNullOrWhiteSpace(especie))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Count(),
            TotalSoltos: totalSoltos,
            MaiorPesoKg: capturas.Count == 0 ? 0 : capturas.Max(captura => captura.PesoKg),
            MaiorComprimentoCm: capturas.Count == 0 ? 0 : capturas.Max(captura => captura.ComprimentoCm),
            EspecieTop: especieTop,
            IscaTop: iscaTop,
            LocalTop: localTop,
            TaxaDeSoltura: taxaDeSoltura,
            Insights: insights,
            RecordesPorEspecie: recordesPorEspecie,
            IscasPorEspecie: iscasPorEspecie,
            Pescarias: resumos);
    }

    private static string Capitalizar(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
