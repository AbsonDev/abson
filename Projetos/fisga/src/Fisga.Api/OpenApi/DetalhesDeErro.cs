namespace Fisga.Api.OpenApi;

/// <summary>
/// Texto do <c>detalhe</c> dos erros produzidos pela camada HTTP. Middlewares
/// escrevem a resposta e o contrato publica o mesmo texto no
/// <c>info.description</c>: mudar a mensagem num lugar só não deixa o contrato
/// mentindo em silêncio.
/// </summary>
public static class DetalhesDeErro
{
    public const string ConsentimentoPendente =
        "Aceite os termos e a privacidade vigentes para continuar.";

    public const string OrigemInvalida = "Origem da requisição não permitida.";
}
