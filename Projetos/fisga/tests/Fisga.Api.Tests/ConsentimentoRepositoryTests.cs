using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;
using Fisga.Infrastructure.Persistence;
using Fisga.Infrastructure.Persistence.Repositories;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class ConsentimentoRepositoryTests : IAsyncLifetime
{
    private readonly IServiceScope _scope;
    private readonly FisgaDbContext _db;
    private readonly IConsentimentoRepository _repository;

    public ConsentimentoRepositoryTests(ApiTestFixture fixture)
    {
        _scope = fixture.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
        _repository = _scope.ServiceProvider.GetRequiredService<IConsentimentoRepository>();
    }

    public async Task InitializeAsync() =>
        await _db.Consentimentos.ExecuteDeleteAsync();

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Repositorio_de_consentimento_esta_registrado_no_di()
    {
        var repository = _scope.ServiceProvider.GetRequiredService<IConsentimentoRepository>();

        Assert.NotNull(repository);
        Assert.IsType<EfConsentimentoRepository>(repository);
    }

    [Fact]
    public async Task ObterUltimoAsync_retorna_o_consentimento_mais_recente_por_aceito_em()
    {
        var baseTempo = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));
        var usuarioId = await CriarUsuarioAsync(baseTempo);

        _db.Consentimentos.Add(Consentimento.Criar(usuarioId, TipoDeConsentimento.Termos, "2026.1", baseTempo));
        _db.Consentimentos.Add(Consentimento.Criar(usuarioId, TipoDeConsentimento.Termos, "2026.2", baseTempo.AddDays(1)));
        _db.Consentimentos.Add(Consentimento.Criar(usuarioId, TipoDeConsentimento.Privacidade, "2026.1", baseTempo.AddDays(2)));
        await _db.SaveChangesAsync();

        var ultimo = await _repository.ObterUltimoAsync(usuarioId, TipoDeConsentimento.Termos);

        Assert.NotNull(ultimo);
        Assert.Equal("2026.2", ultimo!.Versao);
    }

    [Fact]
    public async Task TemVersaoAsync_retorna_verdadeiro_somente_para_usuario_tipo_e_versao_correspondentes()
    {
        var baseTempo = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));
        var usuarioId = await CriarUsuarioAsync(baseTempo);
        var outroUsuarioId = await CriarUsuarioAsync(baseTempo);

        _db.Consentimentos.Add(Consentimento.Criar(usuarioId, TipoDeConsentimento.Termos, "2026.1", baseTempo));
        _db.Consentimentos.Add(Consentimento.Criar(usuarioId, TipoDeConsentimento.Privacidade, "2026.1", baseTempo));
        _db.Consentimentos.Add(Consentimento.Criar(outroUsuarioId, TipoDeConsentimento.Termos, "2026.1", baseTempo));
        await _db.SaveChangesAsync();

        Assert.True(await _repository.TemVersaoAsync(usuarioId, TipoDeConsentimento.Termos, "2026.1"));
        Assert.False(await _repository.TemVersaoAsync(usuarioId, TipoDeConsentimento.Termos, "2026.9"));
        Assert.False(await _repository.TemVersaoAsync(outroUsuarioId, TipoDeConsentimento.Privacidade, "2026.1"));
    }

    private async Task<Guid> CriarUsuarioAsync(DateTimeOffset agora)
    {
        var usuario = Usuario.Criar(Email.Criar($"{Guid.NewGuid():N}@exemplo.com"), null, agora);
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return usuario.Id;
    }
}
