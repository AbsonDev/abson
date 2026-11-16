using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CardapioNoLucro.Domain.Common;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Infrastructure.Persistence.Configurations;

public sealed class FichaConfiguration : IEntityTypeConfiguration<FichaTecnica>
{
    public void Configure(EntityTypeBuilder<FichaTecnica> builder)
    {
        builder.ToTable("fichas", "cardapio");

        builder.HasKey(ficha => ficha.Id);
        builder.Property(ficha => ficha.Id).ValueGeneratedNever();

        builder.Property(ficha => ficha.ShareToken)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(ficha => ficha.ShareToken).IsUnique();

        builder.Property(ficha => ficha.EditTokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(ficha => ficha.NomeDoPrato).HasMaxLength(120).IsRequired();
        builder.Property(ficha => ficha.Estabelecimento).HasMaxLength(120);
        builder.Property(ficha => ficha.Categoria).HasMaxLength(40);

        builder.Property(ficha => ficha.Arredondamento)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(ficha => ficha.Imposto)
            .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));
        builder.Property(ficha => ficha.CmvAlvo)
            .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));

        builder.Property(ficha => ficha.ValorHoraMaoDeObra)
            .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));
        builder.Property(ficha => ficha.CustosFixosMensais)
            .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));

        builder.OwnsMany(ficha => ficha.Ingredientes, ingredientes =>
        {
            ingredientes.ToTable("ficha_ingredientes", "cardapio");
            ingredientes.WithOwner().HasForeignKey("FichaId");
            ingredientes.Property<Guid>("Id").ValueGeneratedOnAdd();
            ingredientes.HasKey("FichaId", "Id");
            ingredientes.Property(item => item.Descricao).HasMaxLength(120).IsRequired();
            ingredientes.Property(item => item.Quantidade).HasPrecision(18, 3);
            ingredientes.Property(item => item.Unidade).HasMaxLength(12).IsRequired();
            ingredientes.Property(item => item.PrecoUnitario)
                .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));
            ingredientes.Property(item => item.Rendimento)
                .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));
            ingredientes.Ignore(item => item.CustoBruto);
            ingredientes.Ignore(item => item.CustoComRendimento);
        });

        builder.OwnsMany(ficha => ficha.Embalagens, embalagens =>
        {
            embalagens.ToTable("ficha_embalagens", "cardapio");
            embalagens.WithOwner().HasForeignKey("FichaId");
            embalagens.Property<Guid>("Id").ValueGeneratedOnAdd();
            embalagens.HasKey("FichaId", "Id");
            embalagens.Property(item => item.Descricao).HasMaxLength(120).IsRequired();
            embalagens.Property(item => item.CustoPorPorcao)
                .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));
        });

        builder.OwnsMany(ficha => ficha.Canais, canais =>
        {
            canais.ToTable("ficha_canais", "cardapio");
            canais.WithOwner().HasForeignKey("FichaId");
            canais.Property<Guid>("Id").ValueGeneratedOnAdd();
            canais.HasKey("FichaId", "Id");
            canais.Property(canal => canal.Tipo)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            canais.Property(canal => canal.Comissao)
                .HasConversion(percentual => percentual.PontosBase, pontos => new Percentual(pontos));
            canais.Property(canal => canal.TaxaFixaPorPedido)
                .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));
        });
    }
}
