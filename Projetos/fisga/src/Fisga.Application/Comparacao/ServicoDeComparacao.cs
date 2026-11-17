using Fisga.Application.Abstractions;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Comparacao;

public sealed record ComparacaoDto(
    bool TemDados,
    string Titulo,
    string Detalhe,
    Guid? PescariaId,
    string? ShareToken,
    IReadOnlyList<string> Fatores,
    int PescariasConsideradas);

public sealed class ServicoDeComparacao(IPescariaRepository pescarias)
{
    private const int MinimoDePescarias = 3;

    public async Task<ComparacaoDto> CompararAsync(
        Guid usuarioId,
        FaseDaLua lua,
        FaseDaMare mare,
        Clima clima,
        PeriodoDoDia periodo,
        CancellationToken ct = default)
    {
        var lista = await pescarias.ListarPorUsuarioAsync(usuarioId, ct);
        var comCapturas = lista
            .Where(pescaria => pescaria.Capturas.Count > 0)
            .ToList();

        if (comCapturas.Count < MinimoDePescarias)
        {
            var faltam = MinimoDePescarias - comCapturas.Count;
            return new ComparacaoDto(
                false,
                "Ainda não há padrões suficientes",
                faltam == 1
                    ? "Registre mais 1 pescaria com captura para comparar lua, maré, clima e período."
                    : $"Registre mais {faltam} pescarias com captura para comparar lua, maré, clima e período.",
                null,
                null,
                [],
                comCapturas.Count);
        }

        var avaliadas = comCapturas
            .Select(pescaria => new
            {
                Pescaria = pescaria,
                Pontos = Pontuar(pescaria, lua, mare, clima, periodo)
            })
            .Where(item => item.Pontos >= 3)
            .OrderByDescending(item => item.Pontos)
            .ThenByDescending(item => item.Pescaria.Capturas.Count)
            .ThenByDescending(item => item.Pescaria.Data)
            .ToList();

        if (avaliadas.Count == 0)
        {
            return new ComparacaoDto(
                false,
                "Nenhum dia parecido ainda",
                $"Suas {comCapturas.Count} pescarias com captura não têm lua, maré e clima suficientes para comparar. Preencha as condições no próximo registro.",
                null,
                null,
                [],
                comCapturas.Count);
        }

        var melhor = avaliadas[0].Pescaria;
        var especie = melhor.Capturas
            .GroupBy(captura => captura.Especie, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(grupo => grupo.Count())
            .ThenByDescending(grupo => grupo.Max(captura => captura.PesoKg))
            .Select(grupo => grupo.Key)
            .FirstOrDefault();

        var fatores = new List<string>();
        if (lua == melhor.Lua)
        {
            fatores.Add(Nome(lua));
        }

        if (mare == melhor.Mare && mare != FaseDaMare.NaoSeAplica)
        {
            fatores.Add($"Maré {Nome(mare).ToLowerInvariant()}");
        }

        if (clima == melhor.Clima && clima != Clima.NaoInformado)
        {
            fatores.Add(Nome(clima));
        }

        if (periodo == melhor.Periodo)
        {
            fatores.Add(Nome(periodo));
        }

        var local = string.IsNullOrWhiteSpace(melhor.Cidade)
            ? melhor.Local
            : $"{melhor.Local} · {melhor.Cidade}";
        var data = melhor.Data.ToString("dd/MM/yyyy");
        var titulo = string.IsNullOrWhiteSpace(especie)
            ? "Hoje lembra um dia que funcionou"
            : $"Hoje lembra seu dia de {especie}";

        return new ComparacaoDto(
            true,
            titulo,
            $"{local} · {data} · {melhor.Capturas.Count} {(melhor.Capturas.Count == 1 ? "captura" : "capturas")}",
            melhor.Id,
            melhor.ShareToken,
            fatores.Distinct(StringComparer.Ordinal).Take(4).ToList(),
            comCapturas.Count);
    }

    private static int Pontuar(
        Pescaria pescaria,
        FaseDaLua lua,
        FaseDaMare mare,
        Clima clima,
        PeriodoDoDia periodo)
    {
        var pontos = 0;

        if (lua != FaseDaLua.NaoInformada && pescaria.Lua == lua)
        {
            pontos += 3;
        }

        if (mare != FaseDaMare.NaoSeAplica && pescaria.Mare == mare)
        {
            pontos += 2;
        }

        if (clima != Clima.NaoInformado && pescaria.Clima == clima)
        {
            pontos += 1;
        }

        if (pescaria.Periodo == periodo)
        {
            pontos += 1;
        }

        return pontos;
    }

    private static string Nome(FaseDaLua lua) => lua switch
    {
        FaseDaLua.Nova => "Lua nova",
        FaseDaLua.Crescente => "Lua crescente",
        FaseDaLua.Cheia => "Lua cheia",
        FaseDaLua.Minguante => "Lua minguante",
        _ => "Lua não informada"
    };

    private static string Nome(FaseDaMare mare) => mare switch
    {
        FaseDaMare.Vazante => "Vazante",
        FaseDaMare.Enchente => "Enchente",
        FaseDaMare.Cheia => "Cheia",
        FaseDaMare.Seca => "Seca",
        _ => "Sem maré"
    };

    private static string Nome(Clima clima) => clima switch
    {
        Clima.Sol => "Sol",
        Clima.Nublado => "Nublado",
        Clima.Chuva => "Chuva",
        Clima.Vento => "Vento",
        _ => "Clima não informado"
    };

    private static string Nome(PeriodoDoDia periodo) => periodo switch
    {
        PeriodoDoDia.Madrugada => "Madrugada",
        PeriodoDoDia.Manha => "Manhã",
        PeriodoDoDia.Tarde => "Tarde",
        PeriodoDoDia.Noite => "Noite",
        _ => "Período"
    };
}
