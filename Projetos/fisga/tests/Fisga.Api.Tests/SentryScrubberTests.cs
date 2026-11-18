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
    public void ScrubUrl_filtra_termo_de_busca_e_preserva_paginacao()
    {
        var url = SentryScrubber.ScrubUrl(
            "https://api.fisga.abson.tech/api/locais/buscar?q=S%C3%A3o%20Jo%C3%A3o&page=2");

        Assert.NotNull(url);
        Assert.Contains("q=[Filtered]", url);
        Assert.Contains("page=2", url);
        Assert.DoesNotContain("S%C3%A3o", url);
    }

    [Fact]
    public void ScrubQuery_filtra_busca_termo_e_chave_desconhecida_por_padrao()
    {
        var query = SentryScrubber.ScrubQuery("q=São João&busca=Paraty&termo=Angra&futuro=segredo&page=3");

        Assert.NotNull(query);
        Assert.DoesNotContain("São João", query);
        Assert.DoesNotContain("Paraty", query);
        Assert.DoesNotContain("Angra", query);
        Assert.DoesNotContain("segredo", query);
        Assert.Contains("page=3", query);
    }

    [Fact]
    public void ScrubText_filtra_q_em_mensagem()
    {
        var texto = SentryScrubber.ScrubText(
            "Falha em /api/locais/buscar?q=S%C3%A3o%20Jo%C3%A3o");

        Assert.DoesNotContain("S%C3%A3o", texto);
        Assert.Contains("q=[Filtered]", texto);
    }

    [Fact]
    public void ScrubEvent_preserva_id_e_remove_pii_do_usuario()
    {
        var evento = new SentryEvent
        {
            User = new SentryUser
            {
                Id = "usuario-123",
                Email = "alguem@exemplo.com",
                Username = "alguem",
                IpAddress = "192.0.2.1",
                Other = new Dictionary<string, string> { ["plano"] = "pro" }
            }
        };

        var limpo = SentryScrubber.ScrubEvent(evento, new SentryHint());

        Assert.Equal("usuario-123", limpo!.User!.Id);
        Assert.Null(limpo.User.Email);
        Assert.Null(limpo.User.Username);
        Assert.Null(limpo.User.IpAddress);
        Assert.Empty(limpo.User.Other);
    }

    [Fact]
    public void ScrubEvent_mantem_sem_usuario_quando_anonimo()
    {
        var limpo = SentryScrubber.ScrubEvent(new SentryEvent(), new SentryHint());

        // O SDK não aceita User nulo: um SentryUser sem Id é semanticamente anônimo.
        Assert.Null(limpo!.User!.Id);
        Assert.Null(limpo.User.Email);
        Assert.Null(limpo.User.Username);
        Assert.Null(limpo.User.IpAddress);
        Assert.Empty(limpo.User.Other);
    }

    [Fact]
    public void ScrubTransaction_preserva_id_e_remove_pii_do_usuario()
    {
        var transacao = new SentryTransaction("GET /api/x", "http.server")
        {
            User = new SentryUser { Id = "usuario-456", Email = "alguem@exemplo.com" }
        };

        var limpa = SentryScrubber.ScrubTransaction(transacao, new SentryHint());

        Assert.Equal("usuario-456", limpa!.User!.Id);
        Assert.Null(limpa.User.Email);
    }
}
