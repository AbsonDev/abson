namespace Fisga.Application.Condicoes;

public sealed class CondicoesOptions
{
    public string Fuso { get; set; } = "America/Sao_Paulo";

    public int CacheMinutos { get; set; } = 20;

    public string AvisoMare { get; set; } =
        "Maré aproximada por modelo global. Não use para navegação; confira a tábua oficial do porto mais próximo.";
}

public sealed record HoraDeCondicoes(
    DateTimeOffset Hora,
    double TemperaturaC,
    double SensacaoC,
    double ChuvaProbabilidade,
    double PressaoHpa,
    double VentoKmh,
    double RajadaKmh,
    double VentoDirecaoGraus,
    double Nebulosidade,
    double? NivelDoMarM,
    double? TemperaturaDaAguaC,
    double? OndaAlturaM,
    double? OndaPeriodoS);

public sealed record DiaDeCondicoes(
    DateOnly Data,
    DateTimeOffset NascerDoSol,
    DateTimeOffset PorDoSol);

public sealed record DadosDaLua(
    DateTimeOffset? Nascer,
    DateTimeOffset? Por,
    DateTimeOffset? Transito,
    DateTimeOffset? TransitoOposto,
    double FaseAngulo);

public sealed record DadosDeCondicoes(
    IReadOnlyList<HoraDeCondicoes> Horas,
    IReadOnlyList<DiaDeCondicoes> Dias,
    DadosDaLua? Lua,
    bool TemMare,
    string Fonte);

public interface IProvedorDeCondicoes
{
    Task<DadosDeCondicoes?> ObterAsync(
        double latitude,
        double longitude,
        DateOnly diaLocal,
        CancellationToken ct = default);
}

public sealed record ResultadoDeBusca(
    string Nome,
    string? Cidade,
    string? Estado,
    double Latitude,
    double Longitude,
    string? Pais);

public interface IGeocodificador
{
    Task<IReadOnlyList<ResultadoDeBusca>> BuscarAsync(
        string termo,
        CancellationToken ct = default);
}

public sealed record LocalDeCondicaoDto(
    string Nome,
    string? Cidade,
    string? Estado,
    double Latitude,
    double Longitude,
    string Ambiente);

public sealed record AgoraDto(
    string Resumo,
    string? MareEstado,
    string? ProximoEvento,
    string? ProximoEventoEm);

public sealed record JanelaDto(
    DateTimeOffset Inicio,
    DateTimeOffset Fim,
    int Nota,
    string Forca,
    IReadOnlyList<string> Motivos);

public sealed record DiaDto(
    DateOnly Data,
    int Nota,
    string Forca,
    DateTimeOffset? MelhorInicio,
    DateTimeOffset? MelhorFim,
    string Resumo,
    DateTimeOffset NascerDoSol,
    DateTimeOffset PorDoSol);

public sealed record EventoDeMareDto(
    string Tipo,
    DateTimeOffset Hora,
    double AlturaM,
    double? EmHoras);

public sealed record PontoDaMareDto(
    DateTimeOffset Hora,
    double AlturaM);

public sealed record MareDto(
    string Estado,
    string Datum,
    EventoDeMareDto? ProximoEvento,
    IReadOnlyList<EventoDeMareDto> Eventos,
    IReadOnlyList<PontoDaMareDto> Curva);

public sealed record PeriodoSolunarDto(
    string Tipo,
    DateTimeOffset Inicio,
    DateTimeOffset Fim,
    string Motivo,
    string Atividade);

public sealed record SolunarDto(
    string Fase,
    string FaseNome,
    double Iluminacao,
    double IdadeDias,
    IReadOnlyList<PeriodoSolunarDto> Periodos);

public sealed record TempoDto(
    double VentoKmh,
    double RajadaKmh,
    double DirecaoGraus,
    string Direcao,
    double ChuvaProbabilidade,
    double TemperaturaC,
    double SensacaoC,
    double PressaoHpa,
    string PressaoTendencia,
    double? TemperaturaDaAguaC,
    double? OndaAlturaM,
    double? OndaPeriodoS);

public sealed record CondicoesDto(
    LocalDeCondicaoDto Local,
    DateTimeOffset AtualizadoEm,
    string Fuso,
    string Fonte,
    bool TemMare,
    bool MareAproximada,
    string? Aviso,
    AgoraDto Agora,
    JanelaDto? MelhorJanela,
    IReadOnlyList<DiaDto> Dias,
    MareDto? Mare,
    SolunarDto Solunar,
    TempoDto Tempo);
