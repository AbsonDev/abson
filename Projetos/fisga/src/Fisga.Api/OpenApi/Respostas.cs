using Fisga.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Metadata de resposta para o contrato. <see cref="ComErro"/> publica o schema
/// único de erro; <see cref="SemCorpo"/> declara respostas sem corpo (204, 302,
/// 413, 429, 503). O transformer normaliza media types, headers e descrições.
/// </summary>
public static class Respostas
{
    public static RouteHandlerBuilder ComErro(this RouteHandlerBuilder construtor, int status) =>
        construtor.Produces<ErroResponse>(status, RespostasPadrao.TipoProblema);

    public static RouteHandlerBuilder SemCorpo(this RouteHandlerBuilder construtor, int status) =>
        construtor.Produces(status);
}
