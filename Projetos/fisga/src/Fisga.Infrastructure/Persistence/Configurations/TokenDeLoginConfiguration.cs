using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class TokenDeLoginConfiguration : IEntityTypeConfiguration<TokenDeLogin>
{
    public void Configure(EntityTypeBuilder<TokenDeLogin> builder)
    {
        builder.ToTable("tokens_de_login", "fisga");

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.Email).HasMaxLength(254).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.Email, token.CriadoEm });

        builder.Property(token => token.CriadoEm).IsRequired();
        builder.Property(token => token.ExpiraEm).IsRequired();
        builder.Property(token => token.UsadoEm);
    }
}
