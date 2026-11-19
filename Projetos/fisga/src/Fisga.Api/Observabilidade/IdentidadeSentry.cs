using Sentry;

namespace Fisga.Api.Observabilidade;

/// <summary>
/// Converte o identificador do usuário em uma identidade opaca para o Sentry:
/// apenas o Guid, sem e-mail, nome, usuário ou IP.
/// </summary>
public static class IdentidadeSentry
{
    public static SentryUser ParaUsuario(Guid usuarioId) =>
        new() { Id = usuarioId.ToString() };
}
