using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class IdentitySchemaTests(ApiTestFixture fixture)
{
    private readonly ApiTestFixture _fixture = fixture;

    [Fact]
    public async Task Banco_cria_tabelas_da_identity_e_consentimentos()
    {
        var factory = _fixture.Factory;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisgaDbContext>();
        await db.Database.EnsureCreatedAsync();

        var tabelas = await db.Database
            .SqlQueryRaw<string>("select name as Value from sqlite_master where type = 'table'")
            .ToListAsync();

        Assert.Contains("AspNetUsers", tabelas);
        Assert.Contains("consentimentos", tabelas);
    }
}
