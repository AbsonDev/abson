using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CardapioNoLucro.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CardapioDbContext>
{
    public CardapioDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CardapioDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=cardapio;Username=cardapio;Password=cardapio")
            .Options;

        return new CardapioDbContext(options);
    }
}
