using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class RegistroTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;

    public RegistroTests(ApiTestFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
    }

    public async Task InitializeAsync()
    {
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
    public async Task Registrar_cria_conta_e_retorna_201_sem_revelar_hash_ou_token()
    {
        var resposta = await _client.PostAsJsonAsync("/api/auth/registrar", new
        {
            nome = "Pescador",
            email = "pescador@exemplo.com",
            senha = "senha-segura-123"
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", corpo);
        Assert.DoesNotContain("token", corpo);
        Assert.DoesNotContain("confirmar-email", corpo);

        var usuario = await _db.Usuarios.SingleAsync(u => u.Email == Email.Criar("pescador@exemplo.com"));
        var conta = await _db.Users.SingleAsync(u => u.Email == "pescador@exemplo.com");

        Assert.Equal(usuario.Id, conta.Id);
        Assert.Null(usuario.EmailVerificadoEm);
        Assert.False(conta.EmailConfirmed);
    }

    [Fact]
    public async Task Registrar_envia_aviso_para_quem_nao_criou_a_conta()
    {
        var email = $"aviso-{Guid.NewGuid():N}@exemplo.com";

        var resposta = await _client.PostAsJsonAsync("/api/auth/registrar", new
        {
            nome = "Pescador",
            email,
            senha = "senha-segura-123"
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var enviado = Assert.Single(
            EmailSenderFake.Enviados,
            e => e.Destino == email && e.Assunto == "Confirme seu e-mail no Fisga");

        Assert.Contains("não criou", enviado.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não confirme", enviado.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não criou", enviado.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registrar_rejeita_senha_curta()
    {
        var resposta = await _client.PostAsJsonAsync("/api/auth/registrar", new
        {
            nome = "Pescador",
            email = "curta@exemplo.com",
            senha = "curta"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(0, await _db.Usuarios.CountAsync());
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task Registrar_rejeita_senha_longa()
    {
        var resposta = await _client.PostAsJsonAsync("/api/auth/registrar", new
        {
            nome = "Pescador",
            email = "longa@exemplo.com",
            senha = new string('a', 129)
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(0, await _db.Usuarios.CountAsync());
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task Registrar_rejeita_email_duplicado()
    {
        await _client.PostAsJsonAsync("/api/auth/registrar", new
        {
            nome = "Pescador",
            email = "dup@exemplo.com",
            senha = "senha-segura-123"
        });

        var resposta = await _client.PostAsJsonAsync("/api/auth/registrar", new
        {
            nome = "Outro",
            email = "dup@exemplo.com",
            senha = "outra-senha-456"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(1, await _db.Usuarios.CountAsync(u => u.Email == Email.Criar("dup@exemplo.com")));
        Assert.Equal(1, await _db.Users.CountAsync(u => u.Email == "dup@exemplo.com"));
    }

    [Fact]
    public async Task Registrar_rejeita_email_invalido()
    {
        var resposta = await _client.PostAsJsonAsync("/api/auth/registrar", new
        {
            nome = "Pescador",
            email = "invalido",
            senha = "senha-segura-123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(0, await _db.Usuarios.CountAsync());
        Assert.Equal(0, await _db.Users.CountAsync());
    }
}
