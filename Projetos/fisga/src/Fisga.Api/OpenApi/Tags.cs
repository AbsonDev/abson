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

    public static IReadOnlyDictionary<string, string> Descricoes { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Sistema] = "Saúde, prontidão e versões legais da API.",
            [Catalogo] = "Espécies, iscas e opções de referência do app.",
            [Pescarias] = "Registro da pescaria, visão pública, edição e PDF.",
            [Locais] = "Locais salvos, busca e geocodificação reversa.",
            [Pontos] = "Pontos privados do pescador.",
            [Trilhas] = "Trilhas privadas do pescador.",
            [Diario] = "Diário, estatísticas e insights do pescador.",
            [Midia] = "Upload de fotos com EXIF tratado no navegador.",
            [Especies] = "Sugestão de espécie a partir de uma foto.",
            [Condicoes] = "Clima, maré aproximada e comparação com o histórico.",
            [Autenticacao] = "Link mágico, senha, Google, sessão e perfil.",
            [Conta] = "Consentimento, senha, sessões, exportação e exclusão.",
            [Newsletter] = "Envio e prévia do digest mensal (token administrativo)."
        };
}
