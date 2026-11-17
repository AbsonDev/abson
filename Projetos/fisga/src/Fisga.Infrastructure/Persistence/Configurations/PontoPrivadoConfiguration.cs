using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Pontos;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class PontoPrivadoConfiguration : IEntityTypeConfiguration<PontoPrivado>
{
    public void Configure(EntityTypeBuilder<PontoPrivado> builder)
    {
        builder.ToTable("pontos_privados", "fisga");

        builder.HasKey(ponto => ponto.Id);
        builder.Property(ponto => ponto.Id).ValueGeneratedNever();

        builder.Property(ponto => ponto.UsuarioId).IsRequired();
        builder.HasIndex(ponto => ponto.UsuarioId);

        builder.HasOne<Domain.Identidade.Usuario>()
            .WithMany()
            .HasForeignKey(ponto => ponto.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(ponto => ponto.Nome).HasMaxLength(80).IsRequired();
        builder.Property(ponto => ponto.Descricao).HasMaxLength(1_000);
        builder.Property(ponto => ponto.Latitude).IsRequired();
        builder.Property(ponto => ponto.Longitude).IsRequired();
        builder.Property(ponto => ponto.Tipo).HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
