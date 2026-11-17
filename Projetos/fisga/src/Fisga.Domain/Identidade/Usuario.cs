using Fisga.Domain.Common;
using Fisga.Domain.Identidade.Eventos;

namespace Fisga.Domain.Identidade;

/// <summary>
/// Pescador com conta no Fisga. O e-mail é a identidade; o nome é opcional e
/// pode ser preenchido no primeiro acesso.
/// </summary>
public sealed class Usuario : AggregateRoot
{
    private Usuario()
    {
    }

    private Usuario(Guid id, Email email, string? nome, DateTimeOffset agora)
    {
        Id = id;
        Email = email;
        Nome = NormalizarNome(nome);
        CriadoEm = agora;
        AtualizadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Email Email { get; private set; } = null!;

    public string? Nome { get; private set; }

    public bool AceitaNovidades { get; private set; }

    public DateTimeOffset? EmailVerificadoEm { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public DateTimeOffset? UltimoAcessoEm { get; private set; }

    public DateTimeOffset? BemVindoEm { get; private set; }

    public string NomeExibicao =>
        string.IsNullOrWhiteSpace(Nome) ? Email.Valor.Split('@')[0] : Nome;

    public static Usuario Criar(Email email, string? nome, DateTimeOffset agora)
    {
        var usuario = new Usuario(Guid.NewGuid(), email, nome, agora);
        usuario.Registrar(new UsuarioCriado(usuario.Id, usuario.Email.Valor, agora));
        return usuario;
    }

    public void VerificarEmail(DateTimeOffset agora)
    {
        EmailVerificadoEm ??= agora;
        AtualizadoEm = agora;
    }

    public void RegistrarAcesso(DateTimeOffset agora)
    {
        UltimoAcessoEm = agora;
        AtualizadoEm = agora;
    }

    public void MarcarBoasVindas(DateTimeOffset agora)
    {
        BemVindoEm ??= agora;
        AtualizadoEm = agora;
    }

    public void AtualizarPerfil(string? nome, bool aceitaNovidades, DateTimeOffset agora)
    {
        Nome = NormalizarNome(nome);
        AceitaNovidades = aceitaNovidades;
        AtualizadoEm = agora;
    }

    public void DescadastrarNovidades(DateTimeOffset agora)
    {
        AceitaNovidades = false;
        AtualizadoEm = agora;
    }

    private static string? NormalizarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return null;
        }

        var limpo = nome.Trim();

        if (limpo.Length > 80)
        {
            throw new DomainException("nome_longo", "O nome deve ter até 80 caracteres.");
        }

        return limpo;
    }
}
