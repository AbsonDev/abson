using Fisga.Application.Abstractions;
using Fisga.Domain.Identidade;

namespace Fisga.Api;

public static class HttpContextExtensions
{
    private const string ChaveUsuario = "fisga.usuario";
    private const string ChaveSessao = "fisga.sessao";

    public static Usuario? UsuarioAtual(this HttpContext http) =>
        http.Items[ChaveUsuario] as Usuario;

    public static Guid? UsuarioId(this HttpContext http) => http.UsuarioAtual()?.Id;

    public static Sessao? SessaoAtual(this HttpContext http) =>
        http.Items[ChaveSessao] as Sessao;

    public static Guid ExigirUsuarioId(this HttpContext http) =>
        http.UsuarioId() ?? throw new NaoAutenticadoException();

    internal static void DefinirSessao(this HttpContext http, Sessao sessao, Usuario usuario)
    {
        http.Items[ChaveUsuario] = usuario;
        http.Items[ChaveSessao] = sessao;
    }
}
