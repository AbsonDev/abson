using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Locais;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class LocalDePescaConfiguration : IEntityTypeConfiguration<LocalDePesca>
{
    public void Configure(EntityTypeBuilder<LocalDePesca> builder)
    {
        builder.ToTable("locais_de_pesca", "fisga");

        builder.HasKey(local => local.Id);
        builder.Property(local => local.Id).ValueGeneratedNever();

        builder.Property(local => local.UsuarioId).IsRequired();
        builder.HasIndex(local => local.UsuarioId);

        builder.HasOne<Domain.Identidade.Usuario>()
            .WithMany()
            .HasForeignKey(local => local.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(local => local.Nome).HasMaxLength(80).IsRequired();
        builder.Property(local => local.Cidade).HasMaxLength(80);
        builder.Property(local => local.Estado).HasMaxLength(2);
        builder.Property(local => local.Latitude).IsRequired();
        builder.Property(local => local.Longitude).IsRequired();
        builder.Property(local => local.Ambiente).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(local => local.Padrao).IsRequired();

        builder.Ignore(local => local.Localizacao);
    }
}
