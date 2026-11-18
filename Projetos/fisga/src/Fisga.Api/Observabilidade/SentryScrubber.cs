using System.Text.RegularExpressions;
using Sentry;

namespace Fisga.Api.Observabilidade;

/// <summary>
/// Remove dados pessoais e coordenadas dos eventos antes de enviá-los ao Sentry.
/// O diário é privado por padrão; telemetria não deve carregar local, e-mail ou sessão.
/// </summary>
public static partial class SentryScrubber
{
    private static readonly HashSet<string> ChavesSensiveis = new(StringComparer.OrdinalIgnoreCase)
    {
        "lat",
        "lon",
        "latitude",
        "longitude",
        "nome",
        "cidade",
        "pescador",
        "email",
        "token",
        "uid",
        "shareToken",
        "handoff_code",
        "code",
        // Parâmetros estruturados de log que carregam destinatário ou corpo de e-mail.
        "destino",
        "descadastro",
        "texto",
        "html"
    };

    private const string Filtrado = "[Filtered]";

    public static SentryEvent? ScrubEvent(SentryEvent evento, SentryHint _)
    {
        evento.ServerName = null;
        evento.User = null!;
        LimparRequest(evento.Request);
        LimparMensagens(evento);
        LimparTags(evento);
        LimparExtras(evento);
        return evento;
    }

    /// <summary>
    /// A integração de logging do Sentry promove os parâmetros estruturados do log
    /// (ex.: <c>{Destino}</c>, <c>{Url}</c>) a tags; elas também precisam ser limpas.
    /// </summary>
    private static void LimparTags(SentryEvent evento)
    {
        foreach (var (chave, valor) in evento.Tags.ToArray())
        {
            var limpo = ScrubValor(chave, valor);
            if (!string.Equals(limpo, valor, StringComparison.Ordinal))
            {
                evento.SetTag(chave, limpo ?? Filtrado);
            }
        }
    }

    private static void LimparExtras(SentryEvent evento)
    {
        foreach (var (chave, valor) in evento.Extra.ToArray())
        {
            if (valor is not string texto)
            {
                if (valor is not null && ChavesSensiveis.Contains(chave))
                {
                    evento.SetExtra(chave, Filtrado);
                }

                continue;
            }

            var limpo = ScrubValor(chave, texto);
            if (!string.Equals(limpo, texto, StringComparison.Ordinal))
            {
                evento.SetExtra(chave, limpo);
            }
        }
    }

    private static string? ScrubValor(string chave, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return valor;
        }

        return ChavesSensiveis.Contains(chave) ? Filtrado : ScrubText(valor);
    }

    private static void LimparMensagens(SentryEvent evento)
    {
        if (evento.Message is { } mensagem)
        {
            mensagem.Message = ScrubText(mensagem.Message);
            mensagem.Formatted = ScrubText(mensagem.Formatted);
        }

        if (evento.SentryExceptions is { } excecoes)
        {
            foreach (var excecao in excecoes)
            {
                excecao.Value = ScrubText(excecao.Value);
            }
        }
    }

    public static SentryTransaction? ScrubTransaction(SentryTransaction transacao, SentryHint _)
    {
        transacao.User = null!;
        LimparRequest(transacao.Request);
        return transacao;
    }

    private static void LimparRequest(SentryRequest? request)
    {
        if (request is null)
        {
            return;
        }

        request.Cookies = null;
        request.Data = null;
        request.QueryString = ScrubQuery(request.QueryString);
        request.Url = ScrubUrl(request.Url);
    }

    public static string? ScrubUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var separador = url.IndexOf('?');
        if (separador < 0)
        {
            return url;
        }

        return $"{url[..separador]}?{ScrubQuery(url[(separador + 1)..])}";
    }

    public static string? ScrubQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return query;
        }

        var partes = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('&', partes.Select(parte =>
        {
            var igual = parte.IndexOf('=');
            if (igual <= 0)
            {
                return parte;
            }

            var chave = Uri.UnescapeDataString(parte[..igual]);
            return ChavesSensiveis.Contains(chave) ? $"{parte[..igual]}=[Filtered]" : parte;
        }));
    }

    public static string? ScrubText(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        var semParametros = ParametroSensivel().Replace(texto, "$1[Filtered]");
        return EnderecoDeEmail().Replace(semParametros, Filtrado);
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EnderecoDeEmail();

    [GeneratedRegex(@"(?i)([?&](?:lat|lon|latitude|longitude|nome|cidade|pescador|email|token|uid|handoff_code|code)=)[^&\s]+")]
    private static partial Regex ParametroSensivel();
}
