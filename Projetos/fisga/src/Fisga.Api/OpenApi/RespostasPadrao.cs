using Microsoft.AspNetCore.Http;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Media types e descrições padrão das respostas do contrato. A descrição mora
/// aqui, não em cada endpoint, para o mesmo status significar a mesma coisa nas
/// 54 operações.
/// </summary>
public static class RespostasPadrao
{
    public const string TipoProblema = "application/problem+json";
    public const string TipoJson = "application/json";

    public static string ComCorpo(int status) => status switch
    {
        StatusCodes.Status400BadRequest =>
            "Requisição inválida. Regra de negócio devolve `codigo` e `detalhe`; corpo JSON malformado devolve ProblemDetails do framework.",
        StatusCodes.Status401Unauthorized =>
            "Sessão ausente, expirada ou revogada (`codigo`: `nao_autenticado`).",
        StatusCodes.Status403Forbidden =>
            "Sem permissão (`acesso_negado`), consentimento pendente (`consentimento_pendente`) ou origem não permitida (`origem_invalida`).",
        StatusCodes.Status404NotFound => "Recurso não encontrado.",
        StatusCodes.Status500InternalServerError =>
            "Erro inesperado (`codigo`: `erro_inesperado`). Tente novamente em instantes.",
        StatusCodes.Status503ServiceUnavailable => "Serviço indisponível no momento.",
        _ => throw new InvalidOperationException($"Status sem descrição de erro com corpo: {status}")
    };

    public static string? SemCorpo(int status) => status switch
    {
        StatusCodes.Status201Created =>
            "Recurso criado. O header `Location` aponta para ele.",
        StatusCodes.Status202Accepted =>
            "Requisição aceita para processamento.",
        StatusCodes.Status204NoContent => "Resposta sem corpo.",
        StatusCodes.Status302Found => "Redireciona o navegador para outra URL.",
        StatusCodes.Status404NotFound => "Recurso não encontrado. Corpo vazio.",
        StatusCodes.Status413PayloadTooLarge =>
            "Upload recusado pelo servidor antes do handler. Corpo vazio; reduza a imagem e tente de novo.",
        StatusCodes.Status429TooManyRequests =>
            "Muitas requisições. Corpo vazio; o header `Retry-After` informa os segundos até a próxima tentativa.",
        StatusCodes.Status503ServiceUnavailable => "Serviço indisponível. Corpo vazio.",
        _ => null
    };
}
