using Fisga.Domain.Common;

namespace Fisga.Domain.Pescarias;

/// <summary>
/// Um peixe capturado dentro de uma pescaria. Tem identidade própria para
/// gerar o card do troféu e manter o link estável após edições.
/// </summary>
public sealed class Captura
{
    private Captura()
    {
    }

    public Captura(
        Guid id,
        string especie,
        decimal pesoKg,
        decimal comprimentoCm,
        string? isca,
        bool solto,
        string? fotoUrl,
        string? observacao)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("captura_sem_identidade", "Captura sem identificador.");
        }

        ValidarCampos(especie, pesoKg, comprimentoCm, isca, fotoUrl, observacao);

        Id = id;
        Especie = especie.Trim();
        PesoKg = pesoKg;
        ComprimentoCm = comprimentoCm;
        Isca = string.IsNullOrWhiteSpace(isca) ? null : isca.Trim();
        Solto = solto;
        FotoUrl = string.IsNullOrWhiteSpace(fotoUrl) ? null : fotoUrl.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    public Guid Id { get; private set; }

    public string Especie { get; private set; } = string.Empty;

    public decimal PesoKg { get; private set; }

    public decimal ComprimentoCm { get; private set; }

    public string? Isca { get; private set; }

    public bool Solto { get; private set; }

    public string? FotoUrl { get; private set; }

    public string? Observacao { get; private set; }

    internal void Atualizar(
        string especie,
        decimal pesoKg,
        decimal comprimentoCm,
        string? isca,
        bool solto,
        string? fotoUrl,
        string? observacao)
    {
        ValidarCampos(especie, pesoKg, comprimentoCm, isca, fotoUrl, observacao);

        Especie = especie.Trim();
        PesoKg = pesoKg;
        ComprimentoCm = comprimentoCm;
        Isca = string.IsNullOrWhiteSpace(isca) ? null : isca.Trim();
        Solto = solto;
        FotoUrl = string.IsNullOrWhiteSpace(fotoUrl) ? null : fotoUrl.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    private static void ValidarCampos(
        string especie,
        decimal pesoKg,
        decimal comprimentoCm,
        string? isca,
        string? fotoUrl,
        string? observacao)
    {
        if (string.IsNullOrWhiteSpace(especie))
        {
            throw new DomainException("captura_sem_especie", "Informe a espécie do peixe.");
        }

        if (especie.Trim().Length > 80)
        {
            throw new DomainException("especie_longa", "A espécie deve ter até 80 caracteres.");
        }

        if (pesoKg < 0 || pesoKg > 1_000)
        {
            throw new DomainException("captura_peso_invalido", "O peso deve estar entre 0 e 1.000 kg.");
        }

        if (comprimentoCm < 0 || comprimentoCm > 500)
        {
            throw new DomainException("captura_comprimento_invalido", "O comprimento deve estar entre 0 e 500 cm.");
        }

        if (pesoKg == 0 && comprimentoCm == 0)
        {
            throw new DomainException("captura_sem_medida", "Informe o peso ou o comprimento da captura.");
        }

        if (isca?.Trim().Length > 80)
        {
            throw new DomainException("isca_longa", "A isca deve ter até 80 caracteres.");
        }

        if (fotoUrl?.Trim().Length > 300)
        {
            throw new DomainException("foto_url_longa", "O caminho da foto é inválido.");
        }

        if (!string.IsNullOrWhiteSpace(fotoUrl)
            && (!fotoUrl.Trim().StartsWith("/media/", StringComparison.Ordinal)
                || fotoUrl.Contains("..", StringComparison.Ordinal)))
        {
            throw new DomainException("foto_url_invalida", "A foto precisa ser enviada pelo Fisga.");
        }

        if (observacao?.Trim().Length > 300)
        {
            throw new DomainException("observacao_captura_longa", "A observação da captura deve ter até 300 caracteres.");
        }
    }
}
