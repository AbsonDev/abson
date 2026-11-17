using Fisga.Application.Abstractions;

namespace Fisga.Application.Documentos;

public sealed class ServicoDePdf(
    IPescariaRepository pescarias,
    IGeradorDePdf gerador)
{
    public async Task<ArquivoPdf> GerarAsync(
        string shareToken,
        string urlCompartilhavel,
        CancellationToken ct = default)
    {
        var pescaria = await pescarias.ObterPorShareTokenAsync(shareToken, ct)
                        ?? throw new RecursoNaoEncontradoException("pescaria_nao_encontrada", "Pescaria não encontrada.");

        var conteudo = gerador.Gerar(pescaria, urlCompartilhavel);
        return new ArquivoPdf(conteudo, $"fisga-{pescaria.ShareToken[..8]}.pdf");
    }
}
