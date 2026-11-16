using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Precifica.Domain.Common;
using Precifica.Domain.Pagamentos;

namespace Precifica.Infrastructure.Persistence.Configurations;

public sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos", "pagamentos");

        builder.HasKey(pedido => pedido.Id);
        builder.Property(pedido => pedido.Id).ValueGeneratedNever();

        builder.Property(pedido => pedido.CalculoId).IsRequired();
        builder.Property(pedido => pedido.Email).HasMaxLength(254).IsRequired();

        builder.Property(pedido => pedido.Tipo)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(pedido => pedido.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(pedido => pedido.Valor)
            .HasConversion(dinheiro => dinheiro.Centavos, centavos => new Dinheiro(centavos));

        builder.Property(pedido => pedido.ReferenciaGateway).HasMaxLength(128);
        builder.Property(pedido => pedido.PagamentoGatewayId).HasMaxLength(128);

        builder.HasIndex(pedido => new { pedido.CalculoId, pedido.Status });
        builder.HasIndex(pedido => pedido.ReferenciaGateway);
    }
}
