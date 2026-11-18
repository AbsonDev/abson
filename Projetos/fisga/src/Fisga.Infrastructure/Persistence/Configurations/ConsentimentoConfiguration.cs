using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class ConsentimentoConfiguration : IEntityTypeConfiguration<Consentimento>
{
    public void Configure(EntityTypeBuilder<Consentimento> builder)
    {
        builder.ToTable("consentimentos", "fisga");

        builder.HasKey(consentimento => consentimento.Id);
        builder.Property(consentimento => consentimento.Id).ValueGeneratedNever();

        builder.Property(consentimento => consentimento.Tipo)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(consentimento => consentimento.Versao).HasMaxLength(64).IsRequired();
        builder.Property(consentimento => consentimento.AceitoEm).IsRequired();

        builder.HasIndex(consentimento => new
        {
            consentimento.UsuarioId,
            consentimento.Tipo,
            consentimento.Versao
        }).IsUnique();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(consentimento => consentimento.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
