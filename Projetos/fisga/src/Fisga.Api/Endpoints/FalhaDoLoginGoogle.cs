using Fisga.Api.Observabilidade;
using Fisga.Application.Identidade;
using Microsoft.AspNetCore.Authentication;

namespace Fisga.Api.Endpoints;

/// <summary>
/// Trata falhas do callback OAuth do Google (<c>/signin-google</c>), como state
/// ausente/inválido ou correlação expirada. Sem isso o handler lança
/// <see cref="AuthenticationFailureException"/> e o usuário recebe um 500
/// genérico; aqui a falha é registrada (log de aviso + métrica) e o usuário
/// volta para a tela de confirmação do site com um estado conhecido.
/// </summary>
public static class FalhaDoLoginGoogle
{
    public const string Estado = "google_sessao_externa_invalida";
    public const string MotivoMetrica = "google_callback_invalido";

    public static Task TratarAsync(RemoteFailureContext contexto)
    {
        var http = contexto.HttpContext;
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(FalhaDoLoginGoogle).FullName!);

        logger.LogWarning(
            contexto.Failure,
            "Falha no callback do Google em {Caminho} (query presente: {TemQuery}): {Motivo}",
            http.Request.Path,
            http.Request.QueryString.HasValue,
            contexto.Failure?.Message);
        Metricas.LoginFalhou(MotivoMetrica);

        var web = http.RequestServices.GetRequiredService<AutenticacaoOptions>().WebUrl.TrimEnd('/');
        contexto.Response.Redirect($"{web}/entrar/confirmar?estado={Estado}");
        contexto.HandleResponse();
        return Task.CompletedTask;
    }
}
