using Fisga.Domain.Pescarias;

namespace Fisga.Application.Pescarias;

public sealed record EntradaDePescaria
{
    public string? Local { get; init; }

    public string? Pescador { get; init; }

    public string? Cidade { get; init; }

    public string? Estado { get; init; }

    public AmbienteDePesca Ambiente { get; init; }

    public DateOnly Data { get; init; }

    public PeriodoDoDia Periodo { get; init; }

    public FaseDaMare Mare { get; init; } = FaseDaMare.NaoSeAplica;

    public FaseDaLua Lua { get; init; } = FaseDaLua.NaoInformada;

    public Clima Clima { get; init; } = Clima.NaoInformado;

    public ClarezaDaAgua ClarezaDaAgua { get; init; } = ClarezaDaAgua.NaoInformada;

    public string? Observacoes { get; init; }

    public IReadOnlyList<EntradaDeCaptura>? Capturas { get; init; }
}

public sealed record EntradaDeCaptura(
    Guid? Id,
    string? Especie,
    decimal PesoKg,
    decimal ComprimentoCm,
    string? Isca,
    bool Solto,
    string? FotoUrl,
    string? Observacao);

public sealed record PescariaSalvaDto(Guid Id, string ShareToken);

public sealed record CapturaDto(
    Guid Id,
    string Especie,
    decimal PesoKg,
    decimal ComprimentoCm,
    string? Isca,
    bool Solto,
    string? FotoUrl,
    string? Observacao);

public sealed record PescariaDetalhadaDto(
    Guid Id,
    string ShareToken,
    EntradaDePescaria Entrada,
    IReadOnlyList<CapturaDto> Capturas,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    bool PodeEditar);

public sealed record PescariaResumoDto(
    Guid Id,
    string ShareToken,
    string Local,
    DateOnly Data,
    string Ambiente,
    string AmbienteNome,
    int Capturas,
    decimal MaiorPesoKg,
    string? FotoCapa);

public sealed record DiarioDto(
    int TotalPescarias,
    int TotalCapturas,
    int EspeciesDistintas,
    int TotalSoltos,
    decimal MaiorPesoKg,
    decimal MaiorComprimentoCm,
    string? EspecieTop,
    string? IscaTop,
    string? LocalTop,
    IReadOnlyList<PescariaResumoDto> Pescarias);

public static class PescariaMapper
{
    public static DadosDaPescaria ParaDados(EntradaDePescaria entrada) =>
        new()
        {
            Local = entrada.Local ?? string.Empty,
            Pescador = entrada.Pescador,
            Cidade = entrada.Cidade,
            Estado = entrada.Estado,
            Ambiente = entrada.Ambiente,
            Data = entrada.Data,
            Periodo = entrada.Periodo,
            Mare = entrada.Mare,
            Lua = entrada.Lua,
            Clima = entrada.Clima,
            ClarezaDaAgua = entrada.ClarezaDaAgua,
            Observacoes = entrada.Observacoes,
            Capturas = (entrada.Capturas ?? [])
                .Select(captura => new DadosDaCaptura(
                    captura.Id,
                    captura.Especie ?? string.Empty,
                    captura.PesoKg,
                    captura.ComprimentoCm,
                    captura.Isca,
                    captura.Solto,
                    captura.FotoUrl,
                    captura.Observacao))
                .ToList()
        };

    public static EntradaDePescaria ParaEntrada(Pescaria pescaria) =>
        new()
        {
            Local = pescaria.Local,
            Pescador = pescaria.Pescador,
            Cidade = pescaria.Cidade,
            Estado = pescaria.Estado,
            Ambiente = pescaria.Ambiente,
            Data = pescaria.Data,
            Periodo = pescaria.Periodo,
            Mare = pescaria.Mare,
            Lua = pescaria.Lua,
            Clima = pescaria.Clima,
            ClarezaDaAgua = pescaria.ClarezaDaAgua,
            Observacoes = pescaria.Observacoes,
            Capturas = pescaria.Capturas
                .OrderBy(captura => captura.Especie, StringComparer.CurrentCultureIgnoreCase)
                .Select(captura => new EntradaDeCaptura(
                    captura.Id,
                    captura.Especie,
                    captura.PesoKg,
                    captura.ComprimentoCm,
                    captura.Isca,
                    captura.Solto,
                    captura.FotoUrl,
                    captura.Observacao))
                .ToList()
        };

    public static PescariaDetalhadaDto ParaDetalhada(Pescaria pescaria, bool podeEditar = false) =>
        new(
            pescaria.Id,
            pescaria.ShareToken,
            ParaEntrada(pescaria),
            pescaria.Capturas
                .OrderBy(captura => captura.Especie, StringComparer.CurrentCultureIgnoreCase)
                .Select(ParaCapturaDto)
                .ToList(),
            pescaria.CriadoEm,
            pescaria.AtualizadoEm,
            podeEditar);

    public static CapturaDto ParaCapturaDto(Captura captura) =>
        new(
            captura.Id,
            captura.Especie,
            captura.PesoKg,
            captura.ComprimentoCm,
            captura.Isca,
            captura.Solto,
            captura.FotoUrl,
            captura.Observacao);
}
