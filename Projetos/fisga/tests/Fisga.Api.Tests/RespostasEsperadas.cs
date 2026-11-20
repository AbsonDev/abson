namespace Fisga.Api.Tests;

/// <summary>
/// Status que cada operação devolve de fato, medidos no handler e no middleware.
/// Não é a intenção do contrato: é a lista que o contrato precisa declarar.
/// </summary>
/// <remarks>
/// A tabela enumera o que a operação produz: status do handler, guardas de
/// consentimento e autenticação, um <c>403</c> específico da operação e o
/// <c>429</c>, porque o contrato expõe a política de rate limit em cada operação.
/// O <c>500</c> só entra onde a operação tem caminho de falha próprio (provedor
/// externo, banco, saúde) — não é o <c>500</c> genérico do
/// <c>ExceptionHandlingMiddleware</c>, que vale para qualquer exceção não
/// tratada e é documentado uma única vez em <c>inventario.md</c> § Regras
/// transversais. Pelo mesmo motivo, o <c>403 origem_invalida</c> do
/// <c>ProtecaoCsrfMiddleware</c> (toda mutação <c>/api</c> com <c>Origin</c>
/// externa) nunca é enumerado aqui. A regra é o que a tabela pratica: em dúvida,
/// compare a linha com o handler antes de acrescentar um status universal.
/// </remarks>
internal static class RespostasEsperadas
{
    public static IReadOnlyDictionary<(string Metodo, string Rota), int[]> PorOperacao { get; } =
        new Dictionary<(string Metodo, string Rota), int[]>
        {
            // Sistema & Legal
            [("GET", "/health")] = [200, 500],
            [("GET", "/ready")] = [200, 503, 500],
            [("GET", "/api/legal/versoes")] = [200, 429],

            // Catálogo
            [("GET", "/api/catalogo")] = [200, 429],

            // Autenticação & Sessão
            [("POST", "/api/auth/solicitar")] = [202, 400, 429],
            [("POST", "/api/auth/registrar")] = [201, 400, 429],
            [("POST", "/api/auth/entrar")] = [200, 400, 429],
            [("POST", "/api/auth/confirmar-email")] = [200, 400, 429],
            [("POST", "/api/auth/esqueci-senha")] = [202, 400, 429],
            [("POST", "/api/auth/redefinir-senha")] = [200, 400, 429],
            [("GET", "/api/auth/confirmar")] = [302, 429, 500],
            [("GET", "/api/auth/google/disponivel")] = [200, 429],
            [("GET", "/api/auth/google/iniciar")] = [302, 503, 429],
            [("GET", "/api/auth/google/concluir")] = [302, 429],
            [("GET", "/api/auth/google/conectar")] = [302, 401, 403, 503, 429],
            [("GET", "/api/auth/google/conectar/concluir")] = [302, 403, 429],
            [("GET", "/api/auth/me")] = [200, 429],
            [("PATCH", "/api/auth/perfil")] = [200, 400, 401, 403, 429],
            [("POST", "/api/auth/sair")] = [204, 429],
            [("POST", "/api/auth/sair-de-todos")] = [204, 401, 403, 429],

            // Conta & Consentimento
            [("POST", "/api/conta/consentimento")] = [204, 400, 401, 429],
            [("POST", "/api/conta/senha")] = [204, 400, 401, 403, 429],
            [("GET", "/api/conta/sessoes")] = [200, 401, 403, 429],
            [("GET", "/api/conta/exportar")] = [200, 401, 403, 429],
            [("DELETE", "/api/conta")] = [204, 400, 401, 403, 429],

            // Pescarias & Compartilhamento
            [("POST", "/api/pescarias")] = [201, 400, 401, 403, 429],
            [("GET", "/api/pescarias/{shareToken}")] = [200, 404, 429],
            [("GET", "/api/pescarias/{shareToken}/permissao")] = [200, 404, 429],
            [("GET", "/api/pescarias/{id}/editar")] = [200, 401, 403, 404, 429],
            [("PUT", "/api/pescarias/{id}")] = [200, 400, 401, 403, 404, 429],
            [("GET", "/api/pescarias/{shareToken}/pdf")] = [200, 404, 429],

            // Diário
            [("GET", "/api/diario")] = [200, 401, 403, 429],

            // Condições
            [("GET", "/api/condicoes")] = [200, 400, 429, 500],
            [("GET", "/api/condicoes/comparar")] = [200, 400, 401, 403, 429, 500],

            // Locais
            [("GET", "/api/locais/reverso")] = [200, 400, 404, 429],
            [("GET", "/api/locais/buscar")] = [200, 429],
            [("GET", "/api/locais")] = [200, 401, 403, 429],
            [("POST", "/api/locais")] = [201, 400, 401, 403, 429],
            [("PUT", "/api/locais/{id}")] = [200, 400, 401, 403, 404, 429],
            [("POST", "/api/locais/{id}/padrao")] = [200, 401, 403, 404, 429],
            [("DELETE", "/api/locais/{id}")] = [204, 401, 403, 404, 429],

            // Pontos
            [("GET", "/api/pontos")] = [200, 401, 403, 429],
            [("POST", "/api/pontos")] = [201, 400, 401, 403, 429],
            [("PUT", "/api/pontos/{id}")] = [200, 400, 401, 403, 404, 429],
            [("DELETE", "/api/pontos/{id}")] = [204, 401, 403, 404, 429],

            // Trilhas
            [("GET", "/api/trilhas")] = [200, 401, 403, 429],
            [("POST", "/api/trilhas")] = [201, 400, 401, 403, 429],
            [("PUT", "/api/trilhas/{id}")] = [200, 400, 401, 403, 404, 429],
            [("DELETE", "/api/trilhas/{id}")] = [204, 401, 403, 404, 429],

            // Mídia
            [("POST", "/api/midia")] = [200, 400, 401, 403, 429],

            // Visão/Espécies
            [("POST", "/api/especies/sugerir")] = [200, 400, 401, 403, 429, 500],

            // Newsletter/Admin
            [("POST", "/api/newsletter/enviar")] = [200, 404, 500],
            [("GET", "/api/newsletter/preview")] = [200, 404, 500],
            [("GET", "/api/newsletter/descadastrar")] = [302, 429]
        };

    // 404 sem corpo: token ausente nas rotas administrativas e coordenada sem
    // local no geocodificador. Todo outro 404 carrega ErroResponse.
    private static readonly HashSet<(string Metodo, string Rota)> NotFoundSemCorpo =
    [
        ("GET", "/api/locais/reverso"),
        ("POST", "/api/newsletter/enviar"),
        ("GET", "/api/newsletter/preview")
    ];

    public static bool TemCorpo((string Metodo, string Rota) operacao, int status) => status switch
    {
        204 or 302 or 429 => false,
        404 => !NotFoundSemCorpo.Contains(operacao),
        503 => operacao != ("GET", "/ready"),
        _ => true
    };
}
