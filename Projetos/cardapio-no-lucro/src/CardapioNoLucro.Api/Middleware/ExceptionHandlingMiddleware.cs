using CardapioNoLucro.Application.Abstractions;
using CardapioNoLucro.Domain.Common;

namespace CardapioNoLucro.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException excecao)
        {
            await EscreverAsync(context, StatusCodes.Status400BadRequest, excecao.Codigo, excecao.Message);
        }
        catch (RecursoNaoEncontradoException excecao)
        {
            await EscreverAsync(context, StatusCodes.Status404NotFound, excecao.Codigo, excecao.Message);
        }
        catch (AcessoNegadoException excecao)
        {
            await EscreverAsync(context, StatusCodes.Status403Forbidden, "acesso_negado", excecao.Message);
        }
        catch (Exception excecao)
        {
            logger.LogError(excecao, "Erro não tratado em {Metodo} {Caminho}", context.Request.Method, context.Request.Path);
            await EscreverAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "erro_inesperado",
                "Algo deu errado do nosso lado. Tente novamente em instantes.");
        }
    }

    private static async Task EscreverAsync(HttpContext context, int status, string codigo, string detalhe)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new Contracts.ErroResponse(codigo, detalhe));
    }
}
