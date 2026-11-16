using Precifica.Domain.Precificacao;

namespace Precifica.Application.Abstractions;

public interface IGeradorDePdf
{
    byte[] Gerar(
        Calculo calculo,
        ResultadoDaPrecificacao resultado,
        bool comMarcaDagua,
        string urlCompartilhavel);
}

public sealed record ArquivoPdf(byte[] Conteudo, string NomeDoArquivo);
