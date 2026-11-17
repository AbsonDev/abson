namespace Fisga.Application.Vision;

public sealed class VisionOptions
{
    public string Provider { get; set; } = "Manual";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.5-flash";
}

public sealed record SugestaoDeEspecie(string Especie, double Confianca);

public sealed record SugestaoDeEspecieResultado(
    bool Disponivel,
    string Mensagem,
    IReadOnlyList<SugestaoDeEspecie> Sugestoes);

public interface IReconhecedorDeEspecie
{
    Task<SugestaoDeEspecieResultado> SugerirAsync(
        Stream imagem,
        string contentType,
        CancellationToken ct = default);
}

public sealed class ManualReconhecedor : IReconhecedorDeEspecie
{
    public Task<SugestaoDeEspecieResultado> SugerirAsync(
        Stream imagem,
        string contentType,
        CancellationToken ct = default) =>
        Task.FromResult(new SugestaoDeEspecieResultado(
            false,
            "A sugestão automática ainda não está configurada. Confirme a espécie manualmente.",
            []));
}
