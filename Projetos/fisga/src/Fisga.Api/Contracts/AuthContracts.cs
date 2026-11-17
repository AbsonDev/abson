using Fisga.Domain.Identidade;

namespace Fisga.Api.Contracts;

public sealed record SolicitarLinkRequest(string? Email);

public sealed record SolicitarLinkResponse(string Mensagem, string? LinkDesenvolvimento);

public sealed record AtualizarPerfilRequest(string? Nome, bool AceitaNovidades);

public sealed record ExcluirContaRequest(string? Confirmacao);

public sealed record UsuarioResponse(
    Guid Id,
    string Email,
    string? Nome,
    string NomeExibicao,
    bool AceitaNovidades,
    DateTimeOffset CriadoEm,
    DateTimeOffset? UltimoAcessoEm)
{
    public static UsuarioResponse De(Usuario usuario) =>
        new(
            usuario.Id,
            usuario.Email.Valor,
            usuario.Nome,
            usuario.NomeExibicao,
            usuario.AceitaNovidades,
            usuario.CriadoEm,
            usuario.UltimoAcessoEm);
}

public sealed record MeResponse(bool Autenticado, UsuarioResponse? Usuario);

public sealed record SessaoResponse(
    Guid Id,
    string? UserAgent,
    DateTimeOffset CriadoEm,
    DateTimeOffset UltimoUsoEm,
    DateTimeOffset ExpiraEm,
    bool Atual)
{
    public static SessaoResponse De(Sessao sessao, Guid? sessaoAtualId) =>
        new(
            sessao.Id,
            sessao.UserAgent,
            sessao.CriadoEm,
            sessao.UltimoUsoEm,
            sessao.ExpiraEm,
            sessaoAtualId is { } atual && atual == sessao.Id);
}

public sealed record PermissaoResponse(Guid PescariaId, bool PodeEditar);
