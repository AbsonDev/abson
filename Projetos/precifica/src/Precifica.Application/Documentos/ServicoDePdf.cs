using Precifica.Application.Abstractions;

namespace Precifica.Application.Documentos;

public sealed class ServicoDePdf(
    ICalculoRepository calculos,
    Pagamentos.ServicoDeAcesso acesso,
    IGeradorDePdf gerador)
{
    public async Task<ArquivoPdf> GerarAsync(
        string shareToken,
        bool premium,
        string? email,
        string urlCompartilhavel,
        CancellationToken ct = default)
    {
        var calculo = await calculos.ObterPorShareTokenAsync(shareToken, ct)
                      ?? throw new RecursoNaoEncontradoException("calculo_nao_encontrado", "Cálculo não encontrado.");

        var premiumConcedido = premium
                               && await acesso.TemAcessoPremiumAsync(calculo.Id, email, ct);

        if (premium && !premiumConcedido)
        {
            throw new PagamentoNecessarioException(
                "O PDF sem marca d'água é um produto pago. Finalize a compra ou informe o e-mail de uma assinatura ativa.");
        }

        var resultado = calculo.Calcular();
        var conteudo = gerador.Gerar(calculo, resultado, !premiumConcedido, urlCompartilhavel);

        return new ArquivoPdf(conteudo, $"precifica-{calculo.ShareToken[..8]}.pdf");
    }
}
