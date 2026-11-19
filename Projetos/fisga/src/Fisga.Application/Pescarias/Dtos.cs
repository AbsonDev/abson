using Fisga.Domain.Pescarias;

namespace Fisga.Application.Pescarias;

public sealed record EntradaDePescaria
{
    public string? Local { get; init; }

    public string? Pescador { get; init; }

    public string? Cidade { get; init; }

    public string? Estado { get; init; }

    public string? CorpoOuTrecho { get; init; }

    public AmbienteDePesca Ambiente { get; init; }

    public DateOnly Data { get; init; }

    public PeriodoDoDia Periodo { get; init; }

    public FaseDaMare Mare { get; init; } = FaseDaMare.NaoSeAplica;

    public FaseDaLua Lua { get; init; } = FaseDaLua.NaoInformada;

    public Clima Clima { get; init; } = Clima.NaoInformado;

    public ClarezaDaAgua ClarezaDaAgua { get; init; } = ClarezaDaAgua.NaoInformada;

    public string? Observacoes { get; init; }

    public PrivacidadeDoLocal PrivacidadeDoLocal { get; init; } = PrivacidadeDoLocal.Cidade;

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
    string? Observacao,
    IReadOnlyList<string>? Fotos = null);

public sealed record PescariaSalvaDto(Guid Id, string ShareToken);

public sealed record CapturaDto(
    Guid Id,
    string Especie,
    decimal PesoKg,
    decimal ComprimentoCm,
    string? Isca,
    bool Solto,
    string? FotoUrl,
    string? Observacao,
    IReadOnlyList<string> Fotos);

public sealed record PescariaDetalhadaDto(
    Guid Id,
    string ShareToken,
    EntradaDePescaria Entrada,
    IReadOnlyList<CapturaDto> Capturas,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    string PrivacidadeDoLocal,
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
    decimal TaxaDeSoltura,
    IReadOnlyList<InsightDto> Insights,
    IReadOnlyList<RecordePorEspecieDto> RecordesPorEspecie,
    IReadOnlyList<IscaPorEspecieDto> IscasPorEspecie,
    IReadOnlyList<ComparacaoDeCondicaoDto> Comparacoes,
    IReadOnlyList<PescariaResumoDto> Pescarias);

public sealed record InsightDto(string Rotulo, string Valor, string Detalhe);

public sealed record RecordePorEspecieDto(
    string Especie,
    decimal PesoKg,
    decimal ComprimentoCm,
    int Capturas);

public sealed record IscaPorEspecieDto(string Especie, string Isca, int Capturas);

public sealed record ComparacaoDeCondicaoDto(
    string Dimensao,
    string Valor,
    double DiferencaPercentual,
    int PescariasCom,
    int CapturasCom,
    int PescariasSem,
    int CapturasSem);

public static class PescariaMapper
{
    public static DadosDaPescaria ParaDados(EntradaDePescaria entrada) =>
        new()
        {
            Local = entrada.Local ?? string.Empty,
            Pescador = entrada.Pescador,
            Cidade = entrada.Cidade,
            Estado = entrada.Estado,
            CorpoOuTrecho = entrada.CorpoOuTrecho,
            Ambiente = entrada.Ambiente,
            Data = entrada.Data,
            Periodo = entrada.Periodo,
            Mare = entrada.Mare,
            Lua = entrada.Lua,
            Clima = entrada.Clima,
            ClarezaDaAgua = entrada.ClarezaDaAgua,
            Observacoes = entrada.Observacoes,
            PrivacidadeDoLocal = entrada.PrivacidadeDoLocal,
            Capturas = (entrada.Capturas ?? [])
                .Select(captura => new DadosDaCaptura(
                    captura.Id,
                    captura.Especie ?? string.Empty,
                    captura.PesoKg,
                    captura.ComprimentoCm,
                    captura.Isca,
                    captura.Solto,
                    captura.FotoUrl,
                    captura.Observacao,
                    captura.Fotos))
                .ToList()
        };

    public static EntradaDePescaria ParaEntrada(Pescaria pescaria) =>
        new()
        {
            Local = pescaria.Local,
            Pescador = pescaria.Pescador,
            Cidade = pescaria.Cidade,
            Estado = pescaria.Estado,
            CorpoOuTrecho = pescaria.CorpoOuTrecho,
            Ambiente = pescaria.Ambiente,
            Data = pescaria.Data,
            Periodo = pescaria.Periodo,
            Mare = pescaria.Mare,
            Lua = pescaria.Lua,
            Clima = pescaria.Clima,
            ClarezaDaAgua = pescaria.ClarezaDaAgua,
            Observacoes = pescaria.Observacoes,
            PrivacidadeDoLocal = pescaria.PrivacidadeDoLocal,
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
                    captura.Observacao,
                    captura.Fotos))
                .ToList()
        };

    public static EntradaDePescaria ParaEntradaPublica(Pescaria pescaria)
    {
        var entrada = ParaEntrada(pescaria);
        return entrada with
        {
            Local = pescaria.LocalPublico,
            Cidade = pescaria.CidadePublica,
            Estado = pescaria.EstadoPublico,
            CorpoOuTrecho = null
        };
    }

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
            pescaria.PrivacidadeDoLocal.ToString(),
            podeEditar);

    public static PescariaDetalhadaDto ParaPublica(Pescaria pescaria, bool podeEditar = false) =>
        new(
            pescaria.Id,
            pescaria.ShareToken,
            ParaEntradaPublica(pescaria),
            pescaria.Capturas
                .OrderBy(captura => captura.Especie, StringComparer.CurrentCultureIgnoreCase)
                .Select(ParaCapturaDto)
                .ToList(),
            pescaria.CriadoEm,
            pescaria.AtualizadoEm,
            pescaria.PrivacidadeDoLocal.ToString(),
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
            captura.Observacao,
            captura.Fotos);
}
