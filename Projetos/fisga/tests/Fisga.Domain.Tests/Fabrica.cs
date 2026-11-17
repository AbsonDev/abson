using Fisga.Domain.Common;
using Fisga.Domain.Pescarias;

namespace Fisga.Domain.Tests;

internal static class Fabrica
{
    public static readonly DateTimeOffset Agora = new(2026, 11, 16, 6, 30, 0, TimeSpan.FromHours(-3));

    public static DadosDaPescaria Dados(
        string local = "Represa do Lago Azul",
        params DadosDaCaptura[] capturas)
    {
        return new DadosDaPescaria
        {
            Local = local,
            Pescador = "Abson",
            Cidade = "Campinas",
            Estado = "SP",
            Ambiente = AmbienteDePesca.Represa,
            Data = new DateOnly(2026, 11, 16),
            Periodo = PeriodoDoDia.Manha,
            Mare = FaseDaMare.NaoSeAplica,
            Lua = FaseDaLua.Crescente,
            Clima = Clima.Sol,
            ClarezaDaAgua = ClarezaDaAgua.Turva,
            Observacoes = "Dia de vento fraco.",
            Capturas = capturas.Length == 0
                ? [
                    new DadosDaCaptura(null, "Tucunaré", 2.4m, 48m, "Popper", true, null, null),
                    new DadosDaCaptura(null, "Tilápia", 0.8m, 28m, "Minhoca", false, null, null)
                ]
                : capturas
        };
    }

    public static Pescaria Pescaria(DadosDaPescaria? dados = null) =>
        Fisga.Domain.Pescarias.Pescaria.Criar(
            dados ?? Dados(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "share-token",
            Agora);
}
