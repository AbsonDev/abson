using Fisga.Domain.Pescarias;

namespace Fisga.Application.Catalogo;

public sealed record ModeloDeCaptura(
    string Especie,
    decimal PesoKg,
    decimal ComprimentoCm,
    string Isca,
    bool Solto,
    string? Observacao);

public sealed record ModeloDePescaria(
    string Local,
    string Pescador,
    string Cidade,
    string Estado,
    string Ambiente,
    string Data,
    string Periodo,
    string Mare,
    string Lua,
    string Clima,
    string ClarezaDaAgua,
    string? Observacoes,
    IReadOnlyList<ModeloDeCaptura> Capturas);

public sealed record CatalogoDto(
    IReadOnlyList<string> Especies,
    IReadOnlyList<string> Iscas,
    IReadOnlyList<string> Ambientes,
    IReadOnlyList<string> Periodos,
    IReadOnlyList<string> FasesDeMare,
    IReadOnlyList<string> FasesDeLua,
    IReadOnlyList<string> Climas,
    IReadOnlyList<string> ClarezasDeAgua,
    ModeloDePescaria Exemplo);

public interface ICatalogoDePesca
{
    IReadOnlyList<string> Especies { get; }

    IReadOnlyList<string> Iscas { get; }

    string NomeDe(AmbienteDePesca ambiente);

    string NomeDe(PeriodoDoDia periodo);

    string NomeDe(FaseDaMare mare);

    string NomeDe(FaseDaLua lua);

    string NomeDe(Clima clima);

    string NomeDe(ClarezaDaAgua clareza);

    CatalogoDto ObterCatalogo();
}

public sealed class CatalogoDePesca : ICatalogoDePesca
{
    private static readonly IReadOnlyList<string> EspeciesSugeridas =
    [
        "Tucunaré", "Tilápia", "Dourado", "Pintado", "Piraputanga", "Traíra", "Robalo",
        "Tainha", "Corvina", "Garoupa", "Linguado", "Pescada", "Pacu", "Tambaqui",
        "Matrinxã", "Lambari", "Bagre", "Carpa", "Truta", "Anchova"
    ];

    private static readonly IReadOnlyList<string> IscasSugeridas =
    [
        "Minhoca", "Camarão", "Lambari", "Massa", "Milho", "Ração", "Sardinha", "Lula",
        "Artificial — Popper", "Artificial — Jig", "Artificial — Colher", "Artificial — Hélice"
    ];

    public IReadOnlyList<string> Especies => EspeciesSugeridas;

    public IReadOnlyList<string> Iscas => IscasSugeridas;

    public string NomeDe(AmbienteDePesca ambiente) => ambiente switch
    {
        AmbienteDePesca.Rio => "Rio",
        AmbienteDePesca.Mar => "Mar",
        AmbienteDePesca.Lagoa => "Lagoa",
        AmbienteDePesca.Represa => "Represa",
        AmbienteDePesca.PesquePague => "Pesque-pague",
        _ => "Outro"
    };

    public string NomeDe(PeriodoDoDia periodo) => periodo switch
    {
        PeriodoDoDia.Madrugada => "Madrugada",
        PeriodoDoDia.Manha => "Manhã",
        PeriodoDoDia.Tarde => "Tarde",
        _ => "Noite"
    };

    public string NomeDe(FaseDaMare mare) => mare switch
    {
        FaseDaMare.Vazante => "Vazante",
        FaseDaMare.Enchente => "Enchente",
        FaseDaMare.Cheia => "Cheia",
        FaseDaMare.Seca => "Seca",
        _ => "Não se aplica"
    };

    public string NomeDe(FaseDaLua lua) => lua switch
    {
        FaseDaLua.Nova => "Nova",
        FaseDaLua.Crescente => "Crescente",
        FaseDaLua.Cheia => "Cheia",
        FaseDaLua.Minguante => "Minguante",
        _ => "Não informada"
    };

    public string NomeDe(Clima clima) => clima switch
    {
        Clima.Sol => "Sol",
        Clima.Nublado => "Nublado",
        Clima.Chuva => "Chuva",
        Clima.Vento => "Vento",
        _ => "Não informado"
    };

    public string NomeDe(ClarezaDaAgua clareza) => clareza switch
    {
        ClarezaDaAgua.Clara => "Clara",
        ClarezaDaAgua.Turva => "Turva",
        ClarezaDaAgua.Barrenta => "Barrenta",
        _ => "Não informada"
    };

    public CatalogoDto ObterCatalogo() =>
        new(
            EspeciesSugeridas,
            IscasSugeridas,
            ["Rio", "Mar", "Lagoa", "Represa", "Pesque-pague", "Outro"],
            ["Madrugada", "Manhã", "Tarde", "Noite"],
            ["Não se aplica", "Vazante", "Enchente", "Cheia", "Seca"],
            ["Não informada", "Nova", "Crescente", "Cheia", "Minguante"],
            ["Não informado", "Sol", "Nublado", "Chuva", "Vento"],
            ["Não informada", "Clara", "Turva", "Barrenta"],
            new ModeloDePescaria(
                Local: "Represa do Lago Azul",
                Pescador: "Pescador",
                Cidade: "Campinas",
                Estado: "SP",
                Ambiente: "Represa",
                Data: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                Periodo: "Manha",
                Mare: "NaoSeAplica",
                Lua: "Crescente",
                Clima: "Sol",
                ClarezaDaAgua: "Turva",
                Observacoes: "Vento fraco e água um pouco turva.",
                Capturas:
                [
                    new ModeloDeCaptura("Tucunaré", 2.4m, 48m, "Popper", true, "Solto para crescer."),
                    new ModeloDeCaptura("Tilápia", 0.8m, 28m, "Minhoca", false, null)
                ]));
}
