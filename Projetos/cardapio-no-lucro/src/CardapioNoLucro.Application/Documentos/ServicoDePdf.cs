using CardapioNoLucro.Application.Abstractions;

namespace CardapioNoLucro.Application.Documentos;

public sealed class ServicoDePdf(
    IFichaRepository fichas,
    IGeradorDePdf gerador)
{
    public async Task<ArquivoPdf> GerarAsync(
        string shareToken,
        string urlCompartilhavel,
        CancellationToken ct = default)
    {
        var ficha = await fichas.ObterPorShareTokenAsync(shareToken, ct)
                    ?? throw new RecursoNaoEncontradoException("ficha_nao_encontrada", "Ficha não encontrada.");

        var resultado = ficha.Calcular();
        var conteudo = gerador.Gerar(ficha, resultado, urlCompartilhavel);

        return new ArquivoPdf(conteudo, $"cardapio-no-lucro-{ficha.ShareToken[..8]}.pdf");
    }
}
