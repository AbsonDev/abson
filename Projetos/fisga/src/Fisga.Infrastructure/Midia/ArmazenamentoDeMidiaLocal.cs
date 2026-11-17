using Microsoft.Extensions.Configuration;
using Fisga.Application.Abstractions;

namespace Fisga.Infrastructure.Midia;

public sealed class ArmazenamentoDeMidiaLocal : IMidiaStorage
{
    private readonly string _pasta;

    public ArmazenamentoDeMidiaLocal(IConfiguration configuration)
    {
        _pasta = ObterPasta(configuration);
        Directory.CreateDirectory(_pasta);
    }

    public async Task<string> SalvarAsync(Stream conteudo, string contentType, CancellationToken ct = default)
    {
        var extensao = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new Domain.Common.DomainException(
                "midia_tipo_invalido",
                "Envie uma imagem JPG, PNG ou WebP.")
        };

        var nome = $"{Guid.NewGuid():N}{extensao}";
        var caminho = Path.Combine(_pasta, nome);

        await using var arquivo = File.Create(caminho);
        await conteudo.CopyToAsync(arquivo, ct);

        return $"/media/{nome}";
    }

    public async Task ExcluirAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !url.StartsWith("/media/", StringComparison.Ordinal)
            || url.Contains("..", StringComparison.Ordinal))
        {
            return;
        }

        var nome = Path.GetFileName(url);
        if (string.IsNullOrWhiteSpace(nome))
        {
            return;
        }

        var caminho = Path.Combine(_pasta, nome);
        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }

        await Task.CompletedTask;
    }

    public static string ObterPasta(IConfiguration configuration)
    {
        var relativo = configuration["Media:Path"] ?? "media";
        return Path.Combine(AppContext.BaseDirectory, relativo);
    }
}
