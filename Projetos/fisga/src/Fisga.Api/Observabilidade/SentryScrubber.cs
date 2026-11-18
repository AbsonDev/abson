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
        "q",
        "busca",
        "termo"
    };

    // Allowlist explícita: qualquer chave fora dela é filtrada por padrão. Assim
    // um parâmetro novo (ex.: termo de busca) não vaza antes de entrar na lista
    // de sensíveis. Só ficam visíveis chaves operacionais sem dado pessoal.
    private static readonly HashSet<string> ChavesPublicas = new(StringComparer.OrdinalIgnoreCase)
    {
        "page",
        "pagina",
        "per_page",
        "limite",
        "offset",
        "ordenar",
        "ordem",
        "ts",
        "v",
        "lang",
        "idioma",
        "utm_source",
        "utm_medium",
        "utm_campaign",
        "utm_content"
    };

    public static SentryEvent? ScrubEvent(SentryEvent evento, SentryHint _)
    {
        evento.ServerName = null;
        evento.User = null!;
        LimparRequest(evento.Request);
        LimparMensagens(evento);
        return evento;
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
            var filtrar = ChavesSensiveis.Contains(chave) || !ChavesPublicas.Contains(chave);
            return filtrar ? $"{parte[..igual]}=[Filtered]" : parte;
        }));
    }

    public static string? ScrubText(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        return ParametroSensivel().Replace(texto, "$1[Filtered]");
    }

    [GeneratedRegex(@"(?i)([?&](?:lat|lon|latitude|longitude|nome|cidade|pescador|email|token|uid|handoff_code|code|q|busca|termo)=)[^&\s]+")]
    private static partial Regex ParametroSensivel();
}
