using Fisga.Domain.Common;
using Fisga.Domain.Pescarias;

namespace Fisga.Domain.Locais;

/// <summary>
/// Um local de pesca salvo pelo usuário. É privado por padrão e serve para
/// abrir as condições já no lugar certo e pré-preencher o diário.
/// </summary>
public sealed class LocalDePesca
{
    private LocalDePesca()
    {
    }

    public LocalDePesca(
        Guid id,
        Guid usuarioId,
        string nome,
        string? cidade,
        string? estado,
        double latitude,
        double longitude,
        AmbienteDePesca ambiente,
        DateTimeOffset agora)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("local_sem_identidade", "Local sem identificador.");
        }

        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("local_sem_usuario", "Todo local pertence a uma conta.");
        }

        Validar(nome, cidade, estado, latitude, longitude, ambiente);

        Id = id;
        UsuarioId = usuarioId;
        Nome = nome.Trim();
        Cidade = Normalizar(cidade);
        Estado = NormalizarEstado(estado);
        Latitude = latitude;
        Longitude = longitude;
        Ambiente = ambiente;
        CriadoEm = agora;
        AtualizadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string? Cidade { get; private set; }

    public string? Estado { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public AmbienteDePesca Ambiente { get; private set; }

    public bool Padrao { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public string Localizacao =>
        string.Join(
            " · ",
            new[] { Cidade, Estado }
                .Where(valor => !string.IsNullOrWhiteSpace(valor)));

    public void Atualizar(
        string nome,
        string? cidade,
        string? estado,
        double latitude,
        double longitude,
        AmbienteDePesca ambiente,
        DateTimeOffset agora)
    {
        Validar(nome, cidade, estado, latitude, longitude, ambiente);

        Nome = nome.Trim();
        Cidade = Normalizar(cidade);
        Estado = NormalizarEstado(estado);
        Latitude = latitude;
        Longitude = longitude;
        Ambiente = ambiente;
        AtualizadoEm = agora;
    }

    public void DefinirPadrao(bool padrao, DateTimeOffset agora)
    {
        Padrao = padrao;
        AtualizadoEm = agora;
    }

    private static void Validar(
        string nome,
        string? cidade,
        string? estado,
        double latitude,
        double longitude,
        AmbienteDePesca ambiente)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("local_sem_nome", "Dê um nome para o local.");
        }

        if (nome.Trim().Length > 80)
        {
            throw new DomainException("local_nome_longo", "O nome do local deve ter até 80 caracteres.");
        }

        if (cidade?.Trim().Length > 80)
        {
            throw new DomainException("local_cidade_longa", "A cidade deve ter até 80 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(estado) && estado.Trim().Length != 2)
        {
            throw new DomainException("local_uf_invalida", "A UF deve ter 2 letras.");
        }

        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new DomainException("local_latitude_invalida", "A latitude deve estar entre -90 e 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new DomainException("local_longitude_invalida", "A longitude deve estar entre -180 e 180.");
        }

        if (!Enum.IsDefined(ambiente))
        {
            throw new DomainException("local_ambiente_invalido", "Escolha um ambiente válido.");
        }
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? NormalizarEstado(string? estado) =>
        string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
}
