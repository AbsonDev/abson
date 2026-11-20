namespace Fisga.Api.OpenApi;

// Mapa congelado de (método, rota) -> operationId. A chave é
// "<MÉTODO> <rota sem barra inicial>", exatamente como no contrato OpenAPI.
// O preenchimento das 54 operações acontece junto com o transformer que
// escreve o operationId; os testes de contrato exigem a bijeção entre spec e mapa.
public static class MapaDeOperacoes
{
    private static readonly IReadOnlyDictionary<string, string> Mapa =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string> Todos => Mapa;

    public static string? Para(string metodo, string rota) =>
        Mapa.TryGetValue(Chave(metodo, rota), out var operationId) ? operationId : null;

    private static string Chave(string metodo, string rota) =>
        $"{metodo.ToUpperInvariant()} {rota.Trim('/')}";
}
