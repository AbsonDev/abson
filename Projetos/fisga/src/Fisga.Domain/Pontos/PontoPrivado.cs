using Fisga.Domain.Common;

namespace Fisga.Domain.Pontos;

public enum TipoDePonto
{
    Ponto = 1,
    Trilha = 2,
    Poita = 3,
    Estrutura = 4,
    Rampa = 5
}

/// <summary>
/// Um ponto privado do pescador: waypoint, poita, estrutura, trilha ou rampa.
/// Nunca é publicado; serve para o mapa pessoal e para o diário.
/// </summary>
public sealed class PontoPrivado
{
    private PontoPrivado()
    {
    }

    public PontoPrivado(
        Guid id,
        Guid usuarioId,
        string nome,
        string? descricao,
        double latitude,
        double longitude,
        TipoDePonto tipo,
        DateTimeOffset agora)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("ponto_sem_identidade", "Ponto sem identificador.");
        }

        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("ponto_sem_usuario", "Todo ponto pertence a uma conta.");
        }

        Validar(nome, descricao, latitude, longitude, tipo);

        Id = id;
        UsuarioId = usuarioId;
        Nome = nome.Trim();
        Descricao = Normalizar(descricao);
        Latitude = latitude;
        Longitude = longitude;
        Tipo = tipo;
        CriadoEm = agora;
        AtualizadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public TipoDePonto Tipo { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public void Atualizar(
        string nome,
        string? descricao,
        double latitude,
        double longitude,
        TipoDePonto tipo,
        DateTimeOffset agora)
    {
        Validar(nome, descricao, latitude, longitude, tipo);

        Nome = nome.Trim();
        Descricao = Normalizar(descricao);
        Latitude = latitude;
        Longitude = longitude;
        Tipo = tipo;
        AtualizadoEm = agora;
    }

    private static void Validar(
        string nome,
        string? descricao,
        double latitude,
        double longitude,
        TipoDePonto tipo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("ponto_sem_nome", "Dê um nome para o ponto.");
        }

        if (nome.Trim().Length > 80)
        {
            throw new DomainException("ponto_nome_longo", "O nome do ponto deve ter até 80 caracteres.");
        }

        if (descricao?.Trim().Length > 1_000)
        {
            throw new DomainException("ponto_descricao_longa", "As notas devem ter até 1.000 caracteres.");
        }

        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new DomainException("ponto_latitude_invalida", "A latitude deve estar entre -90 e 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new DomainException("ponto_longitude_invalida", "A longitude deve estar entre -180 e 180.");
        }

        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException("ponto_tipo_invalido", "Escolha um tipo válido de ponto.");
        }
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
