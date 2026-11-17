using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Pescarias;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class PescariaConfiguration : IEntityTypeConfiguration<Pescaria>
{
    public void Configure(EntityTypeBuilder<Pescaria> builder)
    {
        builder.ToTable("pescarias", "fisga");

        builder.HasKey(pescaria => pescaria.Id);
        builder.Property(pescaria => pescaria.Id).ValueGeneratedNever();

        builder.Property(pescaria => pescaria.UsuarioId).IsRequired();
        builder.HasIndex(pescaria => pescaria.UsuarioId);

        builder.Property(pescaria => pescaria.ShareToken)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(pescaria => pescaria.ShareToken).IsUnique();

        builder.HasOne<Domain.Identidade.Usuario>()
            .WithMany()
            .HasForeignKey(pescaria => pescaria.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(pescaria => pescaria.Local).HasMaxLength(120).IsRequired();
        builder.Property(pescaria => pescaria.Pescador).HasMaxLength(80);
        builder.Property(pescaria => pescaria.Cidade).HasMaxLength(80);
        builder.Property(pescaria => pescaria.Estado).HasMaxLength(2);
        builder.Property(pescaria => pescaria.Observacoes).HasMaxLength(1_000);

        builder.Property(pescaria => pescaria.Ambiente).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(pescaria => pescaria.Periodo).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(pescaria => pescaria.Mare).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(pescaria => pescaria.Lua).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(pescaria => pescaria.Clima).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(pescaria => pescaria.ClarezaDaAgua).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.OwnsMany(pescaria => pescaria.Capturas, capturas =>
        {
            capturas.ToTable("pescaria_capturas", "fisga");
            capturas.WithOwner().HasForeignKey("PescariaId");
            capturas.Property(captura => captura.Id).ValueGeneratedNever();
            capturas.HasKey("PescariaId", nameof(Captura.Id));
            capturas.Property(captura => captura.Especie).HasMaxLength(80).IsRequired();
            capturas.Property(captura => captura.PesoKg).HasPrecision(18, 3);
            capturas.Property(captura => captura.ComprimentoCm).HasPrecision(18, 2);
            capturas.Property(captura => captura.Isca).HasMaxLength(80);
            capturas.Property(captura => captura.FotoUrl).HasMaxLength(300);
            capturas.Property(captura => captura.Observacao).HasMaxLength(300);
        });
    }
}
