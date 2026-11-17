using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Trilhas;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class TrilhaPrivadaConfiguration : IEntityTypeConfiguration<TrilhaPrivada>
{
    public void Configure(EntityTypeBuilder<TrilhaPrivada> builder)
    {
        builder.ToTable("trilhas_privadas", "fisga");

        builder.HasKey(trilha => trilha.Id);
        builder.Property(trilha => trilha.Id).ValueGeneratedNever();

        builder.Property(trilha => trilha.UsuarioId).IsRequired();
        builder.HasIndex(trilha => trilha.UsuarioId);

        builder.HasOne<Domain.Identidade.Usuario>()
            .WithMany()
            .HasForeignKey(trilha => trilha.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(trilha => trilha.Nome).HasMaxLength(80).IsRequired();
        builder.Property(trilha => trilha.Descricao).HasMaxLength(1_000);
        builder.Property(trilha => trilha.Tipo).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(trilha => trilha.Pontos)
            .HasConversion(
                pontos => JsonSerializer.Serialize(pontos, (JsonSerializerOptions?)null),
                json => string.IsNullOrWhiteSpace(json)
                    ? new List<Coordenada>()
                    : JsonSerializer.Deserialize<List<Coordenada>>(json, (JsonSerializerOptions?)null)
                      ?? new List<Coordenada>())
            .HasColumnName("Pontos")
            .HasColumnType("text");

        builder.Ignore(trilha => trilha.DistanciaKm);
    }
}
