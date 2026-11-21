namespace Fisga.Api.OpenApi;

/// <summary>
/// Exemplos de corpo publicados no contrato. Só entra aqui resposta cujo corpo
/// é único: um exemplo para um status que carrega códigos diferentes mentiria.
/// </summary>
public static class ExemplosDeResposta
{
    public static (string Nome, string Resumo, string Json)? Para(
        string metodo,
        string rota,
        int status) =>
        (metodo, rota, status) switch
        {
            ("GET", "/api/auth/me", 200) => (
                "semSessao",
                "Sem sessão: 200 com autenticado false, não erro.",
                """
                {"autenticado":false,"usuario":null,"consentimentoPendente":false,"googleConectado":false,"temSenha":false}
                """),
            _ => null
        };
}
