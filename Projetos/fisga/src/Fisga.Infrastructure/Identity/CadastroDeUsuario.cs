using Microsoft.AspNetCore.Identity;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Provisionamento transacional de contas: cria o <see cref="Usuario"/> de domínio
/// e a <see cref="ApplicationUser"/> da Identity com o mesmo <see cref="Guid"/>,
/// garantindo que nunca reste um usuário de domínio órfão em caso de falha.
/// </summary>
public sealed class CadastroDeUsuario(
    UserManager<ApplicationUser> usuarios,
    FisgaDbContext db,
    IUsuarioRepository repositorio,
    IUnitOfWork unidade,
    IRelogio relogio,
    IDomainEventDispatcher eventos) : ICadastroDeUsuario
{
    private const int TamanhoMinimoDaSenha = 10;
    private const int TamanhoMaximoDaSenha = 128;

    public async Task<Usuario> GarantirUsuarioAsync(
        Email email,
        string? nome,
        bool emailVerificado,
        CancellationToken ct = default)
    {
        var existente = await repositorio.ObterPorEmailAsync(email, ct);
        if (existente is not null)
        {
            return existente;
        }

        var agora = relogio.Agora;
        var usuario = Usuario.Criar(email, nome, agora);
        if (emailVerificado)
        {
            usuario.VerificarEmail(agora);
        }

        repositorio.Adicionar(usuario);
        await unidade.SalvarAsync(ct);

        var pendentes = usuario.Eventos.ToList();
        await eventos.PublicarAsync(pendentes, ct);
        usuario.LimparEventos();

        return usuario;
    }

    public async Task<Usuario> CriarComSenhaAsync(
        string email,
        string? nome,
        string senha,
        CancellationToken ct = default)
    {
        var emailNormalizado = Email.Criar(email);
        ValidarSenha(senha);
        await GarantirEmailLivreAsync(emailNormalizado, ct);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        Usuario? usuario = null;
        try
        {
            usuario = Usuario.Criar(emailNormalizado, nome, relogio.Agora);
            repositorio.Adicionar(usuario);

            var conta = new ApplicationUser
            {
                Id = usuario.Id,
                Email = emailNormalizado.Valor,
                UserName = emailNormalizado.Valor,
                EmailConfirmed = false
            };

            var resultado = await usuarios.CreateAsync(conta, senha);
            if (!resultado.Succeeded)
            {
                throw new DomainException(
                    "falha_ao_criar_conta",
                    resultado.Errors.FirstOrDefault()?.Description ?? "Não foi possível criar a conta.");
            }

            await unidade.SalvarAsync(ct);

            var pendentes = usuario.Eventos.ToList();
            await eventos.PublicarAsync(pendentes, ct);
            usuario.LimparEventos();

            await transacao.CommitAsync(ct);
            return usuario;
        }
        catch
        {
            if (usuario is not null)
            {
                // Remove o usuário de domínio do contexto (entidade em estado Added
                // vira Detached), evitando que reste um rastro após o rollback.
                repositorio.Remover(usuario);
            }

            await transacao.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Garante a <see cref="ApplicationUser"/> correspondente ao <see cref="Usuario"/>,
    /// criando-a (sem senha) quando ainda não existir — usada para contas de acesso
    /// por link ou por login externo.
    /// </summary>
    public async Task<ApplicationUser> GarantirApplicationUserAsync(
        Usuario usuario,
        CancellationToken ct = default)
    {
        var existente = await usuarios.FindByIdAsync(usuario.Id.ToString());
        if (existente is not null)
        {
            return existente;
        }

        var conta = new ApplicationUser
        {
            Id = usuario.Id,
            Email = usuario.Email.Valor,
            UserName = usuario.Email.Valor,
            EmailConfirmed = usuario.EmailVerificadoEm is not null
        };

        var resultado = await usuarios.CreateAsync(conta);
        if (!resultado.Succeeded)
        {
            throw new DomainException(
                "falha_ao_criar_conta",
                resultado.Errors.FirstOrDefault()?.Description ?? "Não foi possível criar a conta.");
        }

        return conta;
    }

    public Task<Usuario?> ObterUsuarioAsync(Guid id, CancellationToken ct = default) =>
        repositorio.ObterPorIdAsync(id, ct);

    private async Task GarantirEmailLivreAsync(Email email, CancellationToken ct)
    {
        var usuario = await repositorio.ObterPorEmailAsync(email, ct);
        if (usuario is not null)
        {
            throw new DomainException("email_em_uso", "Este e-mail já está em uso.");
        }

        var conta = await usuarios.FindByEmailAsync(email.Valor);
        if (conta is not null)
        {
            throw new DomainException("email_em_uso", "Este e-mail já está em uso.");
        }
    }

    private static void ValidarSenha(string senha)
    {
        if (senha.Length < TamanhoMinimoDaSenha)
        {
            throw new DomainException("senha_curta", "A senha deve ter pelo menos 10 caracteres.");
        }

        if (senha.Length > TamanhoMaximoDaSenha)
        {
            throw new DomainException("senha_longa", "A senha deve ter no máximo 128 caracteres.");
        }
    }
}
