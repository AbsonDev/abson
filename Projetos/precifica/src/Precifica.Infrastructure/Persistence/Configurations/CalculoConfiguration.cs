using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Precifica.Domain.Common;
using Precifica.Domain.Precificacao;

namespace Precifica.Infrastructure.Persistence.Configurations;

public sealed class CalculoConfiguration : IEntityTypeConfiguration<Calculo>
{
    public void Configure(EntityTypeBuilder<Calculo> builder)
    {
        builder.ToTable("calculos", "precificacao");

        builder.HasKey(calculo => calculo.Id);
        builder.Property(calculo => calculo.Id).ValueGeneratedNever();

        builder.Property(calculo => calculo.ShareToken)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(calculo => calculo.ShareToken).IsUnique();

        builder.Property(calculo => calculo.EditTokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(calculo => calculo.Nicho)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(calculo => calculo.Titulo)
            .HasMaxLength(140)
            .IsRequired();

        builder.Property(calculo => calculo.Arredondamento)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(calculo => calculo.PerdaDeMaterial)
            .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));

        builder.Property(calculo => calculo.Imposto)
            .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));

        builder.Property(calculo => calculo.TaxaCartao)
            .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));

        builder.Property(calculo => calculo.MargemDesejada)
            .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));

        builder.Property(calculo => calculo.ValorHora)
            .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));

        builder.Property(calculo => calculo.CustosFixosMensais)
            .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));

        builder.Property(calculo => calculo.TaxaFixaPorVenda)
            .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));

        builder.OwnsMany(calculo => calculo.Materiais, materiais =>
        {
            materiais.ToTable("calculo_materiais", "precificacao");
            materiais.WithOwner().HasForeignKey("CalculoId");
            materiais.Property<Guid>("Id").ValueGeneratedOnAdd();
            materiais.HasKey("CalculoId", "Id");
            materiais.Property(material => material.Descricao).HasMaxLength(120).IsRequired();
            materiais.Property(material => material.Quantidade).HasPrecision(18, 3);
            materiais.Property(material => material.Unidade).HasMaxLength(12).IsRequired();
            materiais.Property(material => material.CustoUnitario)
                .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));
            materiais.Ignore(material => material.CustoTotal);
        });

        builder.OwnsMany(calculo => calculo.CustosExtras, extras =>
        {
            extras.ToTable("calculo_custos_extras", "precificacao");
            extras.WithOwner().HasForeignKey("CalculoId");
            extras.Property<Guid>("Id").ValueGeneratedOnAdd();
            extras.HasKey("CalculoId", "Id");
            extras.Property(extra => extra.Descricao).HasMaxLength(120).IsRequired();
            extras.Property(extra => extra.Valor)
                .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));
        });
    }
}
