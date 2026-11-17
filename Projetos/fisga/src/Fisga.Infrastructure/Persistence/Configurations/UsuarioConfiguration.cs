using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Fisga.Domain.Identidade;

namespace Fisga.Infrastructure.Persistence.Configurations;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios", "fisga");

        builder.HasKey(usuario => usuario.Id);
        builder.Property(usuario => usuario.Id).ValueGeneratedNever();

        builder.Property(usuario => usuario.Email)
            .HasConversion(
                email => email.Valor,
                valor => Email.Criar(valor))
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(usuario => usuario.Email).IsUnique();

        builder.Property(usuario => usuario.Nome).HasMaxLength(80);
        builder.Property(usuario => usuario.AceitaNovidades).IsRequired();
        builder.Property(usuario => usuario.EmailVerificadoEm);
        builder.Property(usuario => usuario.CriadoEm).IsRequired();
        builder.Property(usuario => usuario.AtualizadoEm).IsRequired();
        builder.Property(usuario => usuario.UltimoAcessoEm);
        builder.Property(usuario => usuario.BemVindoEm);

        builder.Ignore(usuario => usuario.NomeExibicao);
    }
}
