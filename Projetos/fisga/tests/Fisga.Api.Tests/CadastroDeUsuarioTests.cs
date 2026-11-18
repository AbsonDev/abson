using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class CadastroDeUsuarioTests : IAsyncLifetime
{
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;

    public CadastroDeUsuarioTests(ApiTestFixture fixture)
    {
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
        _cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public async Task InitializeAsync()
    {
        await _db.Usuarios.ExecuteDeleteAsync();
        await _db.Users.ExecuteDeleteAsync();
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Cadastro_de_usuario_esta_registrado_no_di()
    {
        var cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();

        Assert.NotNull(cadastro);
        Assert.IsType<CadastroDeUsuario>(cadastro);
    }

    [Fact]
    public async Task CriarComSenha_cria_usuario_e_application_user_com_o_mesmo_id()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";

        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", "senha-segura-123");

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        var usuarioPersistido = await _db.Usuarios.SingleAsync(u => u.Id == usuario.Id);

        Assert.NotNull(conta);
        Assert.NotNull(usuarioPersistido);
        Assert.Equal(usuario.Id, conta!.Id);
        Assert.Equal(usuario.Id, usuarioPersistido.Id);
        Assert.Equal(email, conta.Email);
        Assert.Equal(email, conta.UserName);
        Assert.Equal(email, usuario.Email.Valor);
    }

    [Fact]
    public async Task CriarComSenha_email_duplicado_nao_cria_segundo_usuario()
    {
        var email = $"dup-{Guid.NewGuid():N}@exemplo.com";
        await _cadastro.CriarComSenhaAsync(email, "Pescador", "senha-segura-123");

        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => _cadastro.CriarComSenhaAsync(email, "Outro", "outra-senha-456"));

        Assert.Equal("email_em_uso", excecao.Codigo);
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    [InlineData("123456789")]
    public async Task CriarComSenha_rejeita_senha_curta(string senha)
    {
        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => _cadastro.CriarComSenhaAsync($"{Guid.NewGuid():N}@exemplo.com", null, senha));

        Assert.Equal("senha_curta", excecao.Codigo);
        Assert.Equal(0, await _db.Usuarios.CountAsync());
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task CriarComSenha_rejeita_senha_longa()
    {
        var senha = new string('a', 129);

        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => _cadastro.CriarComSenhaAsync($"{Guid.NewGuid():N}@exemplo.com", null, senha));

        Assert.Equal("senha_longa", excecao.Codigo);
        Assert.Equal(0, await _db.Usuarios.CountAsync());
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task CriarComSenha_email_verificado_comeca_nulo()
    {
        var usuario = await _cadastro.CriarComSenhaAsync($"{Guid.NewGuid():N}@exemplo.com", null, "senha-segura-123");

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());

        Assert.Null(usuario.EmailVerificadoEm);
        Assert.NotNull(conta);
        Assert.False(conta!.EmailConfirmed);
    }

    [Fact]
    public async Task GarantirUsuarioAsync_cria_uma_vez_e_reusa_a_existente()
    {
        var email = Email.Criar($"{Guid.NewGuid():N}@exemplo.com");

        var primeiro = await _cadastro.GarantirUsuarioAsync(email, "Pescador", emailVerificado: false);
        var segundo = await _cadastro.GarantirUsuarioAsync(email, "Outro Nome", emailVerificado: false);

        Assert.Equal(primeiro.Id, segundo.Id);
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == email));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email.Valor));
    }

    [Fact]
    public async Task GarantirUsuarioAsync_email_verificado_cria_os_dois_lados_verificados()
    {
        var email = Email.Criar($"{Guid.NewGuid():N}@exemplo.com");

        var usuario = await _cadastro.GarantirUsuarioAsync(email, "Pescador", emailVerificado: true);

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(usuario.EmailVerificadoEm);
        Assert.NotNull(conta);
        Assert.Equal(usuario.Id, conta!.Id);
        Assert.Equal(email.Valor, conta.Email);
        Assert.Equal(email.Valor, conta.UserName);
        Assert.True(conta.EmailConfirmed);
        Assert.Null(conta.PasswordHash);
    }

    [Fact]
    public async Task GarantirUsuarioAsync_email_nao_verificado_deixa_os_dois_lados_nao_verificados()
    {
        var email = Email.Criar($"{Guid.NewGuid():N}@exemplo.com");

        var usuario = await _cadastro.GarantirUsuarioAsync(email, null, emailVerificado: false);

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.Null(usuario.EmailVerificadoEm);
        Assert.NotNull(conta);
        Assert.False(conta!.EmailConfirmed);
        Assert.Null(conta.PasswordHash);
    }

    [Fact]
    public async Task GarantirUsuarioAsync_confirma_email_de_conta_de_senha_existente()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", "senha-segura-123");

        var contaAntes = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(contaAntes);
        Assert.False(contaAntes!.EmailConfirmed);
        Assert.Null(usuario.EmailVerificadoEm);
        var stampAntes = contaAntes.SecurityStamp;

        var garantido = await _cadastro.GarantirUsuarioAsync(
            Email.Criar(email), null, emailVerificado: true);

        Assert.Equal(usuario.Id, garantido.Id);
        Assert.NotNull(garantido.EmailVerificadoEm);

        var contaDepois = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(contaDepois);
        Assert.True(contaDepois!.EmailConfirmed);
        Assert.True(await _usuarios.CheckPasswordAsync(contaDepois, "senha-segura-123"),
            "A credencial de senha deve ser preservada.");
        Assert.True(
            string.Equals(stampAntes, contaDepois.SecurityStamp, StringComparison.Ordinal),
            "Confirmar o e-mail não pode rotacionar o SecurityStamp e invalidar cookies ativos.");
    }

    [Fact]
    public async Task GarantirUsuarioAsync_idempotente_nao_duplica_nem_rebaixa_verificacao()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", "senha-segura-123");

        var primeiro = await _cadastro.GarantirUsuarioAsync(Email.Criar(email), null, emailVerificado: true);
        var segundo = await _cadastro.GarantirUsuarioAsync(Email.Criar(email), null, emailVerificado: true);
        var terceiro = await _cadastro.GarantirUsuarioAsync(Email.Criar(email), null, emailVerificado: false);

        Assert.Equal(usuario.Id, primeiro.Id);
        Assert.Equal(usuario.Id, segundo.Id);
        Assert.Equal(usuario.Id, terceiro.Id);
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        Assert.True(conta!.EmailConfirmed, "Chamar com emailVerificado: false não pode rebaixar a conta.");
        Assert.NotNull(terceiro.EmailVerificadoEm);
    }

    [Fact]
    public async Task GarantirUsuarioAsync_cria_application_user_faltante_para_usuario_legado()
    {
        var email = Email.Criar($"{Guid.NewGuid():N}@exemplo.com");
        var legado = Usuario.Criar(email, "Pescador", DateTimeOffset.UtcNow);
        _db.Usuarios.Add(legado);
        await _db.SaveChangesAsync();

        Assert.Null(await _usuarios.FindByIdAsync(legado.Id.ToString()));

        var garantido = await _cadastro.GarantirUsuarioAsync(email, null, emailVerificado: true);

        Assert.Equal(legado.Id, garantido.Id);
        var conta = await _usuarios.FindByIdAsync(legado.Id.ToString());
        Assert.NotNull(conta);
        Assert.Equal(legado.Id, conta!.Id);
        Assert.Equal(email.Valor, conta.Email);
        Assert.True(conta.EmailConfirmed);
        Assert.Null(conta.PasswordHash);
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == email));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email.Valor));
    }

    [Fact]
    public async Task GarantirApplicationUserAsync_cria_conta_para_usuario_sem_senha()
    {
        var usuario = await _cadastro.GarantirUsuarioAsync(
            Email.Criar($"{Guid.NewGuid():N}@exemplo.com"), null, emailVerificado: true);

        // GarantirUsuarioAsync já cria a ApplicationUser; chamar de novo é idempotente.
        var cadastroConcreto = (CadastroDeUsuario)_cadastro;
        var conta = await cadastroConcreto.GarantirApplicationUserAsync(usuario);

        Assert.NotNull(conta);
        Assert.Equal(usuario.Id, conta.Id);
        Assert.True(conta.EmailConfirmed);
        Assert.Null(conta.PasswordHash);
    }

    [Fact]
    public async Task ObterUsuarioAsync_retorna_usuario_por_id_ou_null()
    {
        var usuario = await _cadastro.CriarComSenhaAsync($"{Guid.NewGuid():N}@exemplo.com", null, "senha-segura-123");

        var obtido = await _cadastro.ObterUsuarioAsync(usuario.Id);
        var inexistente = await _cadastro.ObterUsuarioAsync(Guid.NewGuid());

        Assert.NotNull(obtido);
        Assert.Equal(usuario.Id, obtido!.Id);
        Assert.Null(inexistente);
    }

    [Fact]
    public async Task CriarComSenha_nao_cria_usuario_quando_ja_existe_application_user_orfa()
    {
        var email = $"orfa-{Guid.NewGuid():N}@exemplo.com";
        var orfa = new ApplicationUser { Email = email, UserName = email };
        Assert.True((await _usuarios.CreateAsync(orfa, "senha-segura-123")).Succeeded);

        try
        {
            var excecao = await Assert.ThrowsAsync<DomainException>(
                () => _cadastro.CriarComSenhaAsync(email, "Pescador", "outra-senha-456"));

            Assert.Equal("email_em_uso", excecao.Codigo);
            Assert.Equal(0, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
            Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == email));
        }
        finally
        {
            await _usuarios.DeleteAsync(orfa);
        }
    }

    [Fact]
    public async Task CriarComSenha_faz_rollback_do_usuario_de_dominio_quando_a_identity_falha()
    {
        var email = $"{Guid.NewGuid():N}@exemplo.com";

        // Força uma falha controlada DENTRO do CreateAsync, depois que o usuário de
        // domínio já foi adicionado ao contexto, para exercitar o caminho de rollback.
        _usuarios.PasswordValidators.Add(new SenhaBloqueadaValidator());

        var excecao = await Assert.ThrowsAsync<DomainException>(
            () => _cadastro.CriarComSenhaAsync(email, "Pescador", SenhaBloqueadaValidator.Senha));

        Assert.Equal("falha_ao_criar_conta", excecao.Codigo);
        Assert.Equal(0, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar(email)));
        Assert.Equal(0, await _db.Users.CountAsync(u => u.Email == email));
        // O rollback também remove o usuário de domínio do contexto (Added → Detached).
        Assert.DoesNotContain(_db.ChangeTracker.Entries<Usuario>(), entrada => entrada.State == EntityState.Added);
    }

    private sealed class SenhaBloqueadaValidator : IPasswordValidator<ApplicationUser>
    {
        public const string Senha = "senha-bloqueada-123";

        public Task<IdentityResult> ValidateAsync(
            UserManager<ApplicationUser> manager,
            ApplicationUser user,
            string? password)
        {
            if (string.Equals(password, Senha, StringComparison.Ordinal))
            {
                return Task.FromResult(IdentityResult.Failed(
                    new IdentityError { Code = "senha_bloqueada", Description = "Senha bloqueada para teste de rollback." }));
            }

            return Task.FromResult(IdentityResult.Success);
        }
    }
}
