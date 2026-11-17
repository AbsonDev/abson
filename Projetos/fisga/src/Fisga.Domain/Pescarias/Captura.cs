using Fisga.Domain.Common;

namespace Fisga.Domain.Pescarias;

/// <summary>
/// Um peixe capturado dentro de uma pescaria. Tem identidade própria para
/// gerar o card do troféu e manter o link estável após edições. Cada captura
/// pode ter até quatro fotos, com uma capa usada no troféu.
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
        string? observacao,
        IReadOnlyList<string>? fotos = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("captura_sem_identidade", "Captura sem identificador.");
        }

        ValidarCampos(especie, pesoKg, comprimentoCm, isca, observacao);

        Id = id;
        Especie = especie.Trim();
        PesoKg = pesoKg;
        ComprimentoCm = comprimentoCm;
        Isca = string.IsNullOrWhiteSpace(isca) ? null : isca.Trim();
        Solto = solto;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        DefinirFotos(fotoUrl, fotos);
    }

    public Guid Id { get; private set; }

    public string Especie { get; private set; } = string.Empty;

    public decimal PesoKg { get; private set; }

    public decimal ComprimentoCm { get; private set; }

    public string? Isca { get; private set; }

    public bool Solto { get; private set; }

    /// <summary>Galeria de fotos da captura. A ordem é a ordem de exibição.</summary>
    public List<string> Fotos { get; private set; } = [];

    /// <summary>Foto usada no card do troféu e como capa da captura.</summary>
    public string? FotoCapa { get; private set; }

    /// <summary>Compatibilidade com o nome antigo de foto principal.</summary>
    public string? FotoUrl => FotoCapa;

    public string? Observacao { get; private set; }

    internal void Atualizar(
        string especie,
        decimal pesoKg,
        decimal comprimentoCm,
        string? isca,
        bool solto,
        string? fotoUrl,
        string? observacao,
        IReadOnlyList<string>? fotos = null)
    {
        ValidarCampos(especie, pesoKg, comprimentoCm, isca, observacao);

        Especie = especie.Trim();
        PesoKg = pesoKg;
        ComprimentoCm = comprimentoCm;
        Isca = string.IsNullOrWhiteSpace(isca) ? null : isca.Trim();
        Solto = solto;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        DefinirFotos(fotoUrl, fotos);
    }

    private void DefinirFotos(string? fotoUrl, IReadOnlyList<string>? fotos)
    {
        // Compatibilidade: clientes antigos enviam apenas fotoUrl. Se nada foi
        // enviado, preserva a galeria atual (importante em edições parciais).
        if (fotos is null && fotoUrl is null)
        {
            return;
        }

        var lista = (fotos ?? [])
            .Where(foto => !string.IsNullOrWhiteSpace(foto))
            .Select(foto => foto.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (lista.Count == 0 && !string.IsNullOrWhiteSpace(fotoUrl))
        {
            lista.Add(fotoUrl.Trim());
        }

        var capa = string.IsNullOrWhiteSpace(fotoUrl) ? lista.FirstOrDefault() : fotoUrl.Trim();

        if (capa is not null && !lista.Contains(capa, StringComparer.Ordinal))
        {
            lista.Insert(0, capa);
        }

        if (lista.Count > 4)
        {
            throw new DomainException("muitas_fotos", "Uma captura pode ter no máximo 4 fotos.");
        }

        foreach (var foto in lista)
        {
            ValidarFoto(foto);
        }

        Fotos = lista;
        FotoCapa = capa;
    }

    private static void ValidarCampos(
        string especie,
        decimal pesoKg,
        decimal comprimentoCm,
        string? isca,
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

        if (observacao?.Trim().Length > 300)
        {
            throw new DomainException("observacao_captura_longa", "A observação da captura deve ter até 300 caracteres.");
        }
    }

    private static void ValidarFoto(string? foto)
    {
        if (string.IsNullOrWhiteSpace(foto))
        {
            return;
        }

        if (foto.Trim().Length > 300)
        {
            throw new DomainException("foto_url_longa", "O caminho da foto é inválido.");
        }

        if (!foto.Trim().StartsWith("/media/", StringComparison.Ordinal)
            || foto.Contains("..", StringComparison.Ordinal))
        {
            throw new DomainException("foto_url_invalida", "A foto precisa ser enviada pelo Fisga.");
        }
    }
}
