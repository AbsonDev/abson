using System.Security.Claims;
using Fisga.Api.Middleware;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;
using Microsoft.AspNetCore.Http;
using Sentry;

namespace Fisga.Api.Tests;

// O SDK Sentry é global; a collection "api" serializa este teste com os demais.
[Collection("api")]
public sealed class SessaoIdentitySentryTests
{
    [Fact]
    public async Task InvokeAsync_define_id_do_usuario_no_escopo_do_sentry()
    {
        using var sdk = SentrySdk.Init(opcoes =>
        {
            opcoes.Dsn = "https://publico@fake.invalid/1";
            opcoes.TracesSampleRate = 0;
            opcoes.SendDefaultPii = false;
        });

        var agora = DateTimeOffset.UtcNow;
        var usuario = Usuario.Criar(Email.Criar($"{Guid.NewGuid():N}@exemplo.com"), "Pescador", agora);
        var sessao = Sessao.Criar(
            usuario.Id,
            new string('a', 64),
            "agente-de-teste",
            null,
            agora,
            TimeSpan.FromDays(30));

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/me";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim("sessao_id", sessao.Id.ToString())
        ], "autenticacao-de-teste", ClaimTypes.NameIdentifier, ClaimTypes.Role));

        var chamouSeguinte = false;
        var middleware = new SessaoIdentityMiddleware(_ =>
        {
            chamouSeguinte = true;
            return Task.CompletedTask;
        });

        using (SentrySdk.PushScope())
        {
            await middleware.InvokeAsync(
                context,
                new SessoesFake(sessao),
                new UsuariosFake(usuario),
                new UnidadeFake(),
                new RelogioFake(agora));

            string? id = null;
            string? email = "nao-nulo";
            string? ip = "nao-nulo";
            SentrySdk.ConfigureScope(escopo =>
            {
                id = escopo.User?.Id;
                email = escopo.User?.Email;
                ip = escopo.User?.IpAddress;
            });

            Assert.True(chamouSeguinte);
            Assert.Equal(usuario.Id.ToString(), id);
            Assert.Null(email);
            Assert.Null(ip);
        }
    }

    [Fact]
    public async Task InvokeAsync_sem_claim_de_sessao_nao_define_usuario_no_sentry()
    {
        using var sdk = SentrySdk.Init(opcoes =>
        {
            opcoes.Dsn = "https://publico@fake.invalid/1";
            opcoes.TracesSampleRate = 0;
            opcoes.SendDefaultPii = false;
        });

        var agora = DateTimeOffset.UtcNow;
        var usuario = Usuario.Criar(Email.Criar($"{Guid.NewGuid():N}@exemplo.com"), "Pescador", agora);
        var sessao = Sessao.Criar(
            usuario.Id,
            new string('a', 64),
            "agente-de-teste",
            null,
            agora,
            TimeSpan.FromDays(30));

        // Autenticado, mas sem sessao_id: o middleware retorna antes de validar.
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/me";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString())
        ], "autenticacao-de-teste", ClaimTypes.NameIdentifier, ClaimTypes.Role));

        var middleware = new SessaoIdentityMiddleware(_ => Task.CompletedTask);

        using (SentrySdk.PushScope())
        {
            await middleware.InvokeAsync(
                context,
                new SessoesFake(sessao),
                new UsuariosFake(usuario),
                new UnidadeFake(),
                new RelogioFake(agora));

            string? id = "nao-nulo";
            SentrySdk.ConfigureScope(escopo => id = escopo.User?.Id);

            Assert.Null(id);
        }
    }

    private sealed class SessoesFake(Sessao sessao) : ISessaoRepository
    {
        public Task<Sessao?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Sessao?>(sessao);

        public Task<Sessao?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Sessao>> ListarAtivasAsync(
            Guid usuarioId, DateTimeOffset agora, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public void Adicionar(Sessao sessao) => throw new NotImplementedException();

        public Task RevogarTodasAsync(
            Guid usuarioId, DateTimeOffset agora, Guid? exceto, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class UsuariosFake(Usuario usuario) : IUsuarioRepository
    {
        public Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Usuario?>(usuario);

        public Task<IReadOnlyList<Usuario>> ListarAceitaNovidadesAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();

        public void Adicionar(Usuario usuario) => throw new NotImplementedException();

        public void Remover(Usuario usuario) => throw new NotImplementedException();
    }

    private sealed class UnidadeFake : IUnitOfWork
    {
        public Task<int> SalvarAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class RelogioFake(DateTimeOffset agora) : IRelogio
    {
        public DateTimeOffset Agora => agora;
    }
}
