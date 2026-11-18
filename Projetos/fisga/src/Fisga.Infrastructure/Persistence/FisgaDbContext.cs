using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Fisga.Domain.Identidade;
using Fisga.Domain.Locais;
using Fisga.Domain.Pescarias;
using Fisga.Domain.Pontos;
using Fisga.Domain.Trilhas;
using Fisga.Infrastructure.Identity;

namespace Fisga.Infrastructure.Persistence;

public sealed class FisgaDbContext(DbContextOptions<FisgaDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Pescaria> Pescarias => Set<Pescaria>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Sessao> Sessoes => Set<Sessao>();

    public DbSet<TokenDeLogin> TokensDeLogin => Set<TokenDeLogin>();

    public DbSet<LocalDePesca> LocaisDePesca => Set<LocalDePesca>();

    public DbSet<PontoPrivado> PontosPrivados => Set<PontoPrivado>();

    public DbSet<TrilhaPrivada> TrilhasPrivadas => Set<TrilhaPrivada>();

    public DbSet<Consentimento> Consentimentos => Set<Consentimento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("fisga");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FisgaDbContext).Assembly);
    }
}
