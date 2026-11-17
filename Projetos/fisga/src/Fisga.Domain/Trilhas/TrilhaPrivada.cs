using Fisga.Domain.Common;

namespace Fisga.Domain.Trilhas;

public enum TipoDeTrilha
{
    Corrico = 1,
    Deslocamento = 2
}

public sealed record Coordenada(double Latitude, double Longitude);

/// <summary>
/// Uma trilha privada: caminho de corrico ou deslocamento no mapa pessoal.
/// Nunca é publicada e pertence somente ao pescador.
/// </summary>
public sealed class TrilhaPrivada
{
    private TrilhaPrivada()
    {
    }

    public TrilhaPrivada(
        Guid id,
        Guid usuarioId,
        string nome,
        string? descricao,
        TipoDeTrilha tipo,
        IReadOnlyList<Coordenada> pontos,
        DateTimeOffset agora)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("trilha_sem_identidade", "Trilha sem identificador.");
        }

        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("trilha_sem_usuario", "Toda trilha pertence a uma conta.");
        }

        Validar(nome, descricao, tipo, pontos);

        Id = id;
        UsuarioId = usuarioId;
        Nome = nome.Trim();
        Descricao = Normalizar(descricao);
        Tipo = tipo;
        Pontos = pontos.Select(ponto => new Coordenada(ponto.Latitude, ponto.Longitude)).ToList();
        CriadoEm = agora;
        AtualizadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public TipoDeTrilha Tipo { get; private set; }

    public List<Coordenada> Pontos { get; private set; } = [];

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public double DistanciaKm
    {
        get
        {
            var total = 0.0;
            for (var indice = 1; indice < Pontos.Count; indice++)
            {
                total += Distancia(Pontos[indice - 1], Pontos[indice]);
            }

            return Math.Round(total, 2);
        }
    }

    public void Atualizar(
        string nome,
        string? descricao,
        TipoDeTrilha tipo,
        IReadOnlyList<Coordenada> pontos,
        DateTimeOffset agora)
    {
        Validar(nome, descricao, tipo, pontos);

        Nome = nome.Trim();
        Descricao = Normalizar(descricao);
        Tipo = tipo;
        Pontos = pontos.Select(ponto => new Coordenada(ponto.Latitude, ponto.Longitude)).ToList();
        AtualizadoEm = agora;
    }

    private static void Validar(
        string nome,
        string? descricao,
        TipoDeTrilha tipo,
        IReadOnlyList<Coordenada> pontos)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("trilha_sem_nome", "Dê um nome para a trilha.");
        }

        if (nome.Trim().Length > 80)
        {
            throw new DomainException("trilha_nome_longa", "O nome da trilha deve ter até 80 caracteres.");
        }

        if (descricao?.Trim().Length > 1_000)
        {
            throw new DomainException("trilha_descricao_longa", "As notas devem ter até 1.000 caracteres.");
        }

        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException("trilha_tipo_invalido", "Escolha um tipo válido de trilha.");
        }

        if (pontos.Count < 2)
        {
            throw new DomainException("trilha_curta", "Uma trilha precisa de pelo menos dois pontos.");
        }

        if (pontos.Count > 500)
        {
            throw new DomainException("trilha_longa", "Uma trilha pode ter no máximo 500 pontos.");
        }

        if (pontos.Any(ponto =>
                double.IsNaN(ponto.Latitude)
                || double.IsNaN(ponto.Longitude)
                || ponto.Latitude is < -90 or > 90
                || ponto.Longitude is < -180 or > 180))
        {
            throw new DomainException("trilha_coordenada_invalida", "Uma das coordenadas da trilha é inválida.");
        }
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static double Distancia(Coordenada a, Coordenada b)
    {
        const double raioTerraKm = 6371;
        var dLat = (b.Latitude - a.Latitude) * Math.PI / 180;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180;
        var lat1 = a.Latitude * Math.PI / 180;
        var lat2 = b.Latitude * Math.PI / 180;

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * raioTerraKm * Math.Asin(Math.Sqrt(h));
    }
}
