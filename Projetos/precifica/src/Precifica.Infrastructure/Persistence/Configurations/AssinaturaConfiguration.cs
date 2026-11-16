using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;

namespace Precifica.Infrastructure.Persistence.Configurations;

public sealed class AssinaturaConfiguration : IEntityTypeConfiguration<Assinatura>
{
    public void Configure(EntityTypeBuilder<Assinatura> builder)
    {
        builder.ToTable("assinaturas", "pagamentos");

        builder.HasKey(assinatura => assinatura.Id);
        builder.Property(assinatura => assinatura.Id).ValueGeneratedNever();

        builder.Property(assinatura => assinatura.Email).HasMaxLength(254).IsRequired();

        builder.Property(assinatura => assinatura.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(assinatura => assinatura.ValorMensal)
            .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));

        builder.Property(assinatura => assinatura.ReferenciaGateway).HasMaxLength(128);
        builder.Property(assinatura => assinatura.CriadaEm).IsRequired();

        builder.HasIndex(assinatura => assinatura.Email);
        builder.HasIndex(assinatura => assinatura.ReferenciaGateway);
    }
}
