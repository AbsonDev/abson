using Fisga.Api.Endpoints;
using Fisga.Application.Identidade;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fisga.Api.Tests;

/// <summary>
/// Callback do Google com state ausente/inválido (FISGA-API-6) não pode virar
/// 500: o handler de falha remota redireciona para o site e encerra a resposta.
/// </summary>
public sealed class FalhaDoLoginGoogleTests
{
    [Fact]
    public async Task Falha_no_callback_redireciona_para_confirmar_e_trata_a_resposta()
    {
        var servicos = new ServiceCollection()
            .AddLogging()
            .AddSingleton(new AutenticacaoOptions { WebUrl = "https://fisga.exemplo.com/" })
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = servicos };
        http.Request.Path = "/signin-google";

        var contexto = new RemoteFailureContext(
            http,
            new AuthenticationScheme(GoogleDefaults.AuthenticationScheme, null, typeof(GoogleHandler)),
            new GoogleOptions(),
            new AuthenticationFailureException("The oauth state was missing or invalid."));

        await FalhaDoLoginGoogle.TratarAsync(contexto);

        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal(
            $"https://fisga.exemplo.com/entrar/confirmar?estado={FalhaDoLoginGoogle.Estado}",
            http.Response.Headers.Location.ToString());
        Assert.True(contexto.Result?.Handled);
    }
}
