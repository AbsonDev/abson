using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fisga.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FisgaDbContext>
{
    public FisgaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FisgaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=fisga;Username=fisga;Password=fisga")
            .Options;

        return new FisgaDbContext(options);
    }
}
