namespace Fisga.Application.Newsletter;

public sealed class NewsletterOptions
{
    public string AdminToken { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public int MaximoPorEnvio { get; set; } = 100;
}

public sealed record LeituraSazonal(string Titulo, string Url, string Descricao);

public sealed record MensagemDeNewsletter(string Assunto, string Html, string Texto);

public sealed record ResultadoNewsletter(
    string Mes,
    int Elegiveis,
    int Enviados,
    int Falhas,
    bool Simulacao,
    IReadOnlyList<string> Erros);
