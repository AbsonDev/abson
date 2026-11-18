using Fisga.Api.Observabilidade;
using Sentry;
using Sentry.Protocol;

namespace Fisga.Api.Tests;

public sealed class SentryScrubberTests
{
    [Fact]
    public void ScrubEvent_remove_token_e_uid_de_mensagem_e_excecao()
    {
        var evento = new SentryEvent
        {
            Message = "Falha ao confirmar /api/auth/confirmar?uid=usuario-123&token=segredo-abc",
            SentryExceptions =
            [
                new SentryException
                {
                    Type = "InvalidOperationException",
                    Value = "Link inválido: /redefinir-senha?uid=usuario-123&token=segredo-abc"
                }
            ]
        };

        var limpo = SentryScrubber.ScrubEvent(evento, new SentryHint());

        Assert.NotNull(limpo);
        Assert.DoesNotContain("segredo-abc", limpo!.Message!.Message);
        Assert.DoesNotContain("usuario-123", limpo.Message.Message);
        Assert.DoesNotContain("segredo-abc", limpo.SentryExceptions!.First().Value);
        Assert.DoesNotContain("usuario-123", limpo.SentryExceptions!.First().Value);
    }
}
