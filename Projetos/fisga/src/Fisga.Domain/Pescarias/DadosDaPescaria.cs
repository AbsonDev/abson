namespace Fisga.Domain.Pescarias;

public sealed record DadosDaCaptura(
    Guid? Id,
    string Especie,
    decimal PesoKg,
    decimal ComprimentoCm,
    string? Isca,
    bool Solto,
    string? FotoUrl,
    string? Observacao,
    IReadOnlyList<string>? Fotos = null);

public sealed record DadosDaPescaria
{
    public string Local { get; init; } = string.Empty;

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

    public IReadOnlyList<DadosDaCaptura> Capturas { get; init; } = [];
}
