using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Api.Contracts;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Identity;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

/// <summary>
/// Exclusão de conta precisa remover também a credencial da Identity e os
/// tokens sem FK (link mágico/reset), não apenas o usuário de domínio.
/// </summary>
[Collection("api")]
public sealed class ExclusaoContaIdentityTests : IAsyncLifetime
{
    private const string Senha = "senha-segura-123";

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly ICadastroDeUsuario _cadastro;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly ConsentimentoService _consentimento;
    private readonly LegalOptions _legal;

    public ExclusaoContaIdentityTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
        _cadastro = _scope.ServiceProvider.GetRequiredService<ICadastroDeUsuario>();
        _usuarios = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _consentimento = _scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
        _legal = _scope.ServiceProvider.GetRequiredService<LegalOptions>();
    }

    public async Task InitializeAsync()
    {
        await _db.Consentimentos.ExecuteDeleteAsync();
        await _db.UserLogins.ExecuteDeleteAsync();
        await _db.UserTokens.ExecuteDeleteAsync();
        await _db.Sessoes.ExecuteDeleteAsync();
        await _db.TokensDeLogin.ExecuteDeleteAsync();
        await _db.Usuarios.ExecuteDeleteAsync();
        await _db.Users.ExecuteDeleteAsync();
        EmailSenderFake.Limpar();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _scope.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Excluir_conta_remove_identity_logins_tokens_sessoes_e_permite_novo_cadastro()
    {
        var email = $"excluir-{Guid.NewGuid():N}@exemplo.com";
        var usuario = await _cadastro.CriarComSenhaAsync(email, "Pescador", Senha);

        var conta = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.NotNull(conta);
        conta!.EmailConfirmed = true;
        Assert.True((await _usuarios.UpdateAsync(conta)).Succeeded);
        Assert.True((await _usuarios.AddLoginAsync(
            conta,
            new UserLoginInfo(GoogleIdentityValidator.Provider, Guid.NewGuid().ToString("N"), "Google"))).Succeeded);
        Assert.True((await _usuarios.SetAuthenticationTokenAsync(
            conta,
            GoogleIdentityValidator.Provider,
            "access_token",
            "token-de-teste")).Succeeded);

        await _consentimento.RegistrarAsync(usuario.Id, _legal.TermsVersion, _legal.PrivacyVersion);

        var entrar = await _client.PostAsJsonAsync("/api/auth/entrar", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, entrar.StatusCode);

        var linkMagico = await SolicitarLinkAsync(email);
        var linkReset = await SolicitarResetAsync(email);

        using var exclusao = new HttpRequestMessage(HttpMethod.Delete, "/api/conta")
        {
            Content = JsonContent.Create(new { confirmacao = "EXCLUIR" })
        };
        var resposta = await _client.SendAsync(exclusao);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        Assert.Null(await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuario.Id));
        Assert.Null(await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuario.Id));
        Assert.Empty(await _db.UserLogins.AsNoTracking()
            .Where(l => l.UserId == usuario.Id).ToListAsync());
        Assert.Empty(await _db.UserTokens.AsNoTracking()
            .Where(t => t.UserId == usuario.Id).ToListAsync());
        Assert.Empty(await _db.Sessoes.AsNoTracking()
            .Where(s => s.UsuarioId == usuario.Id).ToListAsync());
        Assert.Empty(await _db.TokensDeLogin.AsNoTracking()
            .Where(t => t.Email == email).ToListAsync());

        // O link antigo e o reset antigo não podem ressuscitar a conta.
        using var confirmacaoAntiga = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var respostaAntiga = await confirmacaoAntiga.GetAsync(linkMagico.PathAndQuery);
        Assert.Equal(HttpStatusCode.Found, respostaAntiga.StatusCode);
        Assert.Contains("estado=link_invalido", respostaAntiga.Headers.Location!.ToString());

        var resetAntigo = await _client.PostAsJsonAsync(
            "/api/auth/redefinir-senha",
            new { linkReset.Uid, linkReset.Token, senha = "senha-nova-123" });
        Assert.Equal(HttpStatusCode.BadRequest, resetAntigo.StatusCode);

        // O e-mail fica livre para um cadastro novo, com credencial nova.
        var novoCadastro = await _client.PostAsJsonAsync(
            "/api/auth/registrar",
            new { nome = "Pescador Novo", email, senha = "senha-nova-123" });
        Assert.Equal(HttpStatusCode.Created, novoCadastro.StatusCode);
        Assert.NotNull(await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email));
    }

    private async Task<Uri> SolicitarLinkAsync(string email)
    {
        var resposta = await _client.PostAsJsonAsync("/api/auth/solicitar", new { email });
        Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<SolicitarLinkResponse>();
        Assert.False(string.IsNullOrWhiteSpace(corpo?.LinkDesenvolvimento));
        return new Uri(corpo!.LinkDesenvolvimento!, UriKind.Absolute);
    }

    private async Task<(string Uid, string Token)> SolicitarResetAsync(string email)
    {
        var resposta = await _client.PostAsJsonAsync("/api/auth/esqueci-senha", new { email });
        Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);

        var enviado = Assert.Single(
            EmailSenderFake.Enviados,
            e => e.Destino == email && e.Assunto == "Redefina sua senha no Fisga");

        var url = Regex.Match(enviado.Texto, @"https?://\S+").Value;
        var uri = new Uri(url, UriKind.Absolute);
        var valores = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => parte.Split('=', 2))
            .ToDictionary(parte => parte[0], parte => Uri.UnescapeDataString(parte[1]));

        return (valores["uid"], valores["token"]);
    }
}
