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
    public const string TipoPdf = "application/pdf";

    /// <summary>
    /// Descrição do erro com corpo. Só é chamada para status que publicam
    /// <c>application/problem+json</c>; qualquer outro status é falha de
    /// configuração do contrato e estoura na geração do documento.
    /// </summary>
    /// <param name="status">Status HTTP da resposta de erro.</param>
    /// <param name="metodo">
    /// Método da operação. O <c>403</c> só cita <c>origem_invalida</c> em
    /// mutação, porque a checagem de <c>Origin</c> do CSRF não atua em leitura.
    /// </param>
    /// <exception cref="InvalidOperationException">Status sem descrição de erro com corpo.</exception>
    public static string DescricaoComErro(int status, string metodo) => status switch
    {
        StatusCodes.Status400BadRequest =>
            "Requisição inválida. Regra de negócio devolve `codigo` e `detalhe`; corpo ilegível é recusado antes do handler, com `400` sem corpo.",
        StatusCodes.Status401Unauthorized =>
            $"Sessão ausente, expirada ou revogada (`codigo`: `{CodigosDeErro.NaoAutenticado}`).",
        StatusCodes.Status403Forbidden when MetodosHttp.EhMutacao(metodo) =>
            $"Sem permissão (`{CodigosDeErro.AcessoNegado}`), consentimento pendente (`{CodigosDeErro.ConsentimentoPendente}`) ou origem não permitida (`{CodigosDeErro.OrigemInvalida}`).",
        StatusCodes.Status403Forbidden =>
            $"Sem permissão (`{CodigosDeErro.AcessoNegado}`) ou consentimento pendente (`{CodigosDeErro.ConsentimentoPendente}`).",
        StatusCodes.Status404NotFound => "Recurso não encontrado.",
        StatusCodes.Status500InternalServerError =>
            $"Erro inesperado (`codigo`: `{CodigosDeErro.ErroInesperado}`). Tente novamente em instantes.",
        StatusCodes.Status503ServiceUnavailable =>
            "Serviço indisponível no momento. Corpo `ErroResponse` com o motivo.",
        _ => throw new InvalidOperationException($"Status sem descrição de erro com corpo: {status}")
    };

    /// <summary>
    /// Descrição de resposta sem schema de corpo: 201, 202, 204, 302, o 404 sem
    /// corpo, 429 e 503. O catálogo também cobre 413 para a operação que venha a
    /// recusar o upload antes do handler; hoje nenhuma publica esse status.
    /// Status desconhecido devolve <see langword="null"/> e o transformer
    /// preserva a descrição gerada pelo framework.
    /// </summary>
    public static string? DescricaoSemSchema(int status) => status switch
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
