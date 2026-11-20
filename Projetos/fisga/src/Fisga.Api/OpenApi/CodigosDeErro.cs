using System.Reflection;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Vocabulário dos <c>codigo</c> de erro que a camada HTTP produz. Middlewares e
/// handlers usam as constantes; o contrato e o portal documentam a partir delas.
/// Regras de negócio continuam criando o seu código no domínio, onde a regra vive.
/// </summary>
public static class CodigosDeErro
{
    public const string NaoAutenticado = "nao_autenticado";
    public const string AcessoNegado = "acesso_negado";
    public const string ErroInesperado = "erro_inesperado";
    public const string ConsentimentoPendente = "consentimento_pendente";
    public const string OrigemInvalida = "origem_invalida";
    public const string PescariaNaoEncontrada = "pescaria_nao_encontrada";
    public const string LocalSemCoordenadas = "local_sem_coordenadas";
    public const string CoordenadasInvalidas = "coordenadas_invalidas";
    public const string GoogleIndisponivel = "google_indisponivel";
    public const string MidiaSemFormulario = "midia_sem_formulario";
    public const string MidiaVazia = "midia_vazia";
    public const string MidiaGrande = "midia_grande";
    public const string MidiaTipoInvalido = "midia_tipo_invalido";
    public const string SugestaoSemFormulario = "sugestao_sem_formulario";
    public const string SugestaoSemFoto = "sugestao_sem_foto";
    public const string SugestaoFotoGrande = "sugestao_foto_grande";
    public const string SugestaoTipoInvalido = "sugestao_tipo_invalido";

    /// <summary>Todos os códigos declarados acima, sem chance de esquecer um.</summary>
    public static IReadOnlySet<string> Todos { get; } = typeof(CodigosDeErro)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(campo => campo.IsLiteral && campo.FieldType == typeof(string))
        .Select(campo => (string)campo.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);
}
