namespace Fisga.Api;

/// <summary>
/// Classificação de método HTTP usada por mais de um ponto do pipeline. CSRF só
/// atua em mutação e o contrato só anuncia <c>origem_invalida</c> onde a mutação
/// existe; manter a lista aqui evita que os dois lados divirjam.
/// </summary>
public static class MetodosHttp
{
    public static bool EhMutacao(string metodo) =>
        HttpMethods.IsPost(metodo)
        || HttpMethods.IsPut(metodo)
        || HttpMethods.IsPatch(metodo)
        || HttpMethods.IsDelete(metodo);
}
