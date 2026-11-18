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

    [Fact]
    public void ScrubEvent_remove_destinatario_e_token_das_tags_de_log()
    {
        var evento = new SentryEvent { Message = "Link enviado para pescador@exemplo.com" };
        evento.SetTag("Destino", "pescador@exemplo.com");
        evento.SetTag("Url", "http://localhost:5184/api/auth/confirmar?token=segredo-abc");
        evento.SetTag("Expira", "17/11/2026 17:01");
        evento.SetExtra("Texto", "Acesse http://localhost:4323/confirmar-email?uid=usuario-123&token=segredo-abc");

        var limpo = SentryScrubber.ScrubEvent(evento, new SentryHint());

        Assert.NotNull(limpo);
        Assert.Equal("[Filtered]", limpo!.Tags["Destino"]);
        Assert.Equal("http://localhost:5184/api/auth/confirmar?token=[Filtered]", limpo.Tags["Url"]);
        Assert.Equal("17/11/2026 17:01", limpo.Tags["Expira"]);
        Assert.Equal("[Filtered]", limpo.Extra["Texto"]);
        Assert.DoesNotContain("pescador@exemplo.com", limpo.Message!.Message);
    }

    [Fact]
    public void ScrubText_remove_enderecos_de_email_soltos()
    {
        var texto = SentryScrubber.ScrubText("Para: metrics.1794944797@exemplo.com");

        Assert.Equal("Para: [Filtered]", texto);
    }
}
