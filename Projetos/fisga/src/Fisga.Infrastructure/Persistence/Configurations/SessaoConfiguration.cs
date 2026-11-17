using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class SessaoConfiguration : IEntityTypeConfiguration<Sessao>
{
    public void Configure(EntityTypeBuilder<Sessao> builder)
    {
        builder.ToTable("sessoes", "fisga");

        builder.HasKey(sessao => sessao.Id);
        builder.Property(sessao => sessao.Id).ValueGeneratedNever();

        builder.Property(sessao => sessao.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(sessao => sessao.TokenHash).IsUnique();

        builder.Property(sessao => sessao.UserAgent).HasMaxLength(300);
        builder.Property(sessao => sessao.IpHash).HasMaxLength(64);
        builder.Property(sessao => sessao.CriadoEm).IsRequired();
        builder.Property(sessao => sessao.UltimoUsoEm).IsRequired();
        builder.Property(sessao => sessao.ExpiraEm).IsRequired();
        builder.Property(sessao => sessao.RevogadaEm);

        builder.HasIndex(sessao => new { sessao.UsuarioId, sessao.ExpiraEm });

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(sessao => sessao.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
