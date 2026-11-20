namespace Fisga.Api.OpenApi;

// Tags de domínio do contrato OpenAPI. Cada operação pertence a exatamente uma
// destas áreas; "Fisga.Api" e nomes de classe/handler não são tags válidas.
public static class Tags
{
    public const string Sistema = "Sistema";
    public const string Catalogo = "Catálogo";
    public const string Pescarias = "Pescarias";
    public const string Locais = "Locais";
    public const string Pontos = "Pontos";
    public const string Trilhas = "Trilhas";
    public const string Diario = "Diário";
    public const string Midia = "Mídia";
    public const string Especies = "Espécies";
    public const string Condicoes = "Condições";
    public const string Autenticacao = "Autenticação";
    public const string Conta = "Conta";
    public const string Newsletter = "Newsletter";

    public static IReadOnlyList<string> Todas { get; } =
    [
        Sistema,
        Catalogo,
        Pescarias,
        Locais,
        Pontos,
        Trilhas,
        Diario,
        Midia,
        Especies,
        Condicoes,
        Autenticacao,
        Conta,
        Newsletter
    ];
}
