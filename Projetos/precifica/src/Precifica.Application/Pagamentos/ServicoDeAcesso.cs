using Microsoft.Extensions.Options;
using Precifica.Application.Abstractions;
using Precifica.Application.Calculos;
using Precifica.Domain.Pagamentos;

namespace Precifica.Application.Pagamentos;

public sealed record AcessoDto(bool PdfAvulsoPago, bool AssinaturaAtiva, bool Premium);

/// <summary>
/// Regras de acesso ao PDF sem marca d'água. Compra avulsa é vinculada ao cálculo;
/// assinatura é vinculada ao e-mail informado no download.
/// </summary>
public sealed class ServicoDeAcesso(
    IPedidoRepository pedidos,
    IAssinaturaRepository assinaturas,
    IRelogio relogio,
    IOptions<MonetizacaoOptions> monetizacao)
{
    public async Task<AcessoDto> ObterAsync(Guid calculoId, string? email = null, CancellationToken ct = default)
    {
        // Período promocional: o produto inteiro é gratuito, sem pedido nem assinatura.
        if (!monetizacao.Value.Ativa)
        {
            return new AcessoDto(PdfAvulsoPago: false, AssinaturaAtiva: false, Premium: true);
        }

        var pago = await pedidos.ExistePagoParaCalculoAsync(calculoId, ct);

        var assinaturaAtiva = false;
        var emailNormalizado = (email ?? string.Empty).Trim().ToLowerInvariant();

        if (emailNormalizado.Length > 0)
        {
            var assinatura = await assinaturas.ObterPorEmailAsync(emailNormalizado, ct);
            assinaturaAtiva = assinatura?.EstaAtiva(relogio.Agora) == true;
        }

        return new AcessoDto(pago, assinaturaAtiva, pago || assinaturaAtiva);
    }

    public async Task<bool> TemAcessoPremiumAsync(Guid calculoId, string? email, CancellationToken ct = default) =>
        (await ObterAsync(calculoId, email, ct)).Premium;
}
