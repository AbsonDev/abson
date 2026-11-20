namespace Fisga.Api.OpenApi;

// Mapa congelado de (método, rota) -> operationId. A chave é
// "<MÉTODO> <rota sem barra inicial>", exatamente como no contrato OpenAPI.
// O preenchimento das 54 operações acontece junto com o transformer que
// escreve o operationId; os testes de contrato exigem a bijeção entre spec e mapa.
public static class MapaDeOperacoes
{
    private static readonly IReadOnlyDictionary<string, string> Mapa =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GET api/auth/confirmar"] = "confirmarLink",
            ["POST api/auth/confirmar-email"] = "confirmarEmail",
            ["POST api/auth/entrar"] = "entrarComSenha",
            ["POST api/auth/esqueci-senha"] = "esquecerSenha",
            ["GET api/auth/google/concluir"] = "concluirLoginGoogle",
            ["GET api/auth/google/conectar"] = "conectarGoogle",
            ["GET api/auth/google/conectar/concluir"] = "concluirConexaoGoogle",
            ["GET api/auth/google/disponivel"] = "googleDisponivel",
            ["GET api/auth/google/iniciar"] = "iniciarLoginGoogle",
            ["GET api/auth/me"] = "obterSessao",
            ["PATCH api/auth/perfil"] = "atualizarPerfil",
            ["POST api/auth/redefinir-senha"] = "redefinirSenha",
            ["POST api/auth/registrar"] = "registrarConta",
            ["POST api/auth/sair"] = "sair",
            ["POST api/auth/sair-de-todos"] = "sairDeTodos",
            ["POST api/auth/solicitar"] = "solicitarLink",
            ["GET api/catalogo"] = "obterCatalogo",
            ["GET api/condicoes"] = "obterCondicoes",
            ["GET api/condicoes/comparar"] = "compararCondicoes",
            ["DELETE api/conta"] = "excluirConta",
            ["POST api/conta/consentimento"] = "registrarConsentimento",
            ["GET api/conta/exportar"] = "exportarConta",
            ["POST api/conta/senha"] = "alterarSenha",
            ["GET api/conta/sessoes"] = "listarSessoes",
            ["GET api/diario"] = "obterDiario",
            ["POST api/especies/sugerir"] = "sugerirEspecies",
            ["GET api/legal/versoes"] = "versoesLegais",
            ["GET api/locais"] = "listarLocais",
            ["POST api/locais"] = "criarLocal",
            ["PUT api/locais/{id}"] = "atualizarLocal",
            ["DELETE api/locais/{id}"] = "excluirLocal",
            ["POST api/locais/{id}/padrao"] = "definirLocalPadrao",
            ["GET api/locais/buscar"] = "buscarLocais",
            ["GET api/locais/reverso"] = "reverterCoordenadas",
            ["POST api/midia"] = "enviarMidia",
            ["GET api/newsletter/descadastrar"] = "descadastrarNewsletter",
            ["POST api/newsletter/enviar"] = "enviarNewsletter",
            ["GET api/newsletter/preview"] = "previsualizarNewsletter",
            ["POST api/pescarias"] = "criarPescaria",
            ["PUT api/pescarias/{id}"] = "atualizarPescaria",
            ["GET api/pescarias/{id}/editar"] = "obterPescariaDoDono",
            ["GET api/pescarias/{shareToken}"] = "obterPescariaPorToken",
            ["GET api/pescarias/{shareToken}/pdf"] = "exportarPescariaPdf",
            ["GET api/pescarias/{shareToken}/permissao"] = "obterPermissaoPescaria",
            ["GET api/pontos"] = "listarPontos",
            ["POST api/pontos"] = "criarPonto",
            ["PUT api/pontos/{id}"] = "atualizarPonto",
            ["DELETE api/pontos/{id}"] = "excluirPonto",
            ["GET api/trilhas"] = "listarTrilhas",
            ["POST api/trilhas"] = "criarTrilha",
            ["PUT api/trilhas/{id}"] = "atualizarTrilha",
            ["DELETE api/trilhas/{id}"] = "excluirTrilha",
            ["GET health"] = "obterSaude",
            ["GET ready"] = "obterProntidao"
        };

    public static IReadOnlyDictionary<string, string> Todos => Mapa;

    public static string? Para(string metodo, string rota) =>
        Mapa.TryGetValue(Chave(metodo, rota), out var operationId) ? operationId : null;

    private static string Chave(string metodo, string rota) =>
        $"{metodo.ToUpperInvariant()} {rota.TrimStart('/')}";
}
