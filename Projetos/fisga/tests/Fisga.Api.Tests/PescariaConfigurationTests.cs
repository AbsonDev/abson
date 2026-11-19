using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Fisga.Domain.Pescarias;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Api.Tests;

public sealed class PescariaConfigurationTests
{
    [Fact]
    public void CorpoOuTrecho_e_opcional_e_limitado_a_80_caracteres()
    {
        var options = new DbContextOptionsBuilder<FisgaDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new FisgaDbContext(options);

        var propriedade = db.Model.FindEntityType(typeof(Pescaria))!
            .FindProperty(nameof(Pescaria.CorpoOuTrecho));

        Assert.NotNull(propriedade);
        Assert.Equal(80, propriedade!.GetMaxLength());
        Assert.True(propriedade.IsNullable);
    }
}
