using Fisga.Domain.Common;
using Fisga.Domain.Pescarias.Eventos;

namespace Fisga.Domain.Pescarias;

/// <summary>
/// Raiz do contexto de Pesca: uma saída de pesca com local, condições e as
/// capturas daquele dia. Toda pescaria pertence a um usuário.
/// </summary>
public sealed class Pescaria : AggregateRoot
{
    private readonly List<Captura> _capturas = [];

    private Pescaria()
    {
    }

    private Pescaria(
        Guid id,
        Guid usuarioId,
        string shareToken,
        DateTimeOffset criadoEm)
    {
        Id = id;
        UsuarioId = usuarioId;
        ShareToken = shareToken;
        CriadoEm = criadoEm;
        AtualizadoEm = criadoEm;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public string ShareToken { get; private set; } = string.Empty;

    public string Local { get; private set; } = string.Empty;

    public string? Pescador { get; private set; }

    public string? Cidade { get; private set; }

    public string? Estado { get; private set; }

    public string? CorpoOuTrecho { get; private set; }

    public AmbienteDePesca Ambiente { get; private set; }

    public DateOnly Data { get; private set; }

    public PeriodoDoDia Periodo { get; private set; }

    public FaseDaMare Mare { get; private set; }

    public FaseDaLua Lua { get; private set; }

    public Clima Clima { get; private set; }

    public ClarezaDaAgua ClarezaDaAgua { get; private set; }

    public string? Observacoes { get; private set; }

    public PrivacidadeDoLocal PrivacidadeDoLocal { get; private set; }

    public string LocalPublico => PrivacidadeDoLocal switch
    {
        PrivacidadeDoLocal.Exato => Local,
        PrivacidadeDoLocal.Trecho when !string.IsNullOrWhiteSpace(CorpoOuTrecho) => CorpoOuTrecho!,
        PrivacidadeDoLocal.CorpoAgua when !string.IsNullOrWhiteSpace(CorpoOuTrecho) =>
            CorpoOuTrecho!.Split(',')[0].Trim(),
        PrivacidadeDoLocal.Cidade or PrivacidadeDoLocal.Trecho or PrivacidadeDoLocal.CorpoAgua
            when !string.IsNullOrWhiteSpace(Cidade) =>
            string.IsNullOrWhiteSpace(Estado) ? Cidade! : $"{Cidade} · {Estado}",
        _ => "Local não divulgado"
    };

    public string? CidadePublica =>
        PrivacidadeDoLocal == PrivacidadeDoLocal.Oculto ? null : Cidade;

    public string? EstadoPublico =>
        PrivacidadeDoLocal == PrivacidadeDoLocal.Oculto ? null : Estado;

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public IReadOnlyCollection<Captura> Capturas => _capturas;

    public static Pescaria Criar(
        DadosDaPescaria dados,
        Guid id,
        Guid usuarioId,
        string shareToken,
        DateTimeOffset agora)
    {
        var pescaria = new Pescaria(id, usuarioId, shareToken, agora);
        pescaria.Aplicar(dados, agora);

        pescaria.Registrar(new PescariaRegistrada(
            pescaria.Id,
            pescaria.Local,
            pescaria._capturas.Count,
            agora));

        return pescaria;
    }

    public void Atualizar(DadosDaPescaria dados, DateTimeOffset agora) =>
        Aplicar(dados, agora);

    public bool PertenceA(Guid usuarioId) => usuarioId != Guid.Empty && UsuarioId == usuarioId;

    private void Aplicar(DadosDaPescaria dados, DateTimeOffset agora)
    {
        Local = (dados.Local ?? string.Empty).Trim();
        Pescador = string.IsNullOrWhiteSpace(dados.Pescador) ? null : dados.Pescador.Trim();
        Cidade = string.IsNullOrWhiteSpace(dados.Cidade) ? null : dados.Cidade.Trim();
        Estado = string.IsNullOrWhiteSpace(dados.Estado) ? null : dados.Estado.Trim().ToUpperInvariant();
        CorpoOuTrecho = string.IsNullOrWhiteSpace(dados.CorpoOuTrecho) ? null : dados.CorpoOuTrecho.Trim();
        Ambiente = dados.Ambiente;
        Data = dados.Data;
        Periodo = dados.Periodo;
        Mare = dados.Mare;
        Lua = dados.Lua;
        Clima = dados.Clima;
        ClarezaDaAgua = dados.ClarezaDaAgua;
        Observacoes = string.IsNullOrWhiteSpace(dados.Observacoes) ? null : dados.Observacoes.Trim();
        PrivacidadeDoLocal = dados.PrivacidadeDoLocal;
        AtualizadoEm = agora;

        var existentes = _capturas.ToDictionary(captura => captura.Id);
        var novas = new List<Captura>();

        foreach (var entrada in dados.Capturas ?? [])
        {
            if (entrada.Id is { } id && existentes.TryGetValue(id, out var existente))
            {
                if (novas.Contains(existente))
                {
                    throw new DomainException("captura_duplicada", "Uma captura apareceu mais de uma vez na pescaria.");
                }

                existente.Atualizar(
                    entrada.Especie,
                    entrada.PesoKg,
                    entrada.ComprimentoCm,
                    entrada.Isca,
                    entrada.Solto,
                    entrada.FotoUrl,
                    entrada.Observacao,
                    entrada.Fotos);

                novas.Add(existente);
            }
            else if (entrada.Id is null)
            {
                novas.Add(new Captura(
                    Guid.NewGuid(),
                    entrada.Especie,
                    entrada.PesoKg,
                    entrada.ComprimentoCm,
                    entrada.Isca,
                    entrada.Solto,
                    entrada.FotoUrl,
                    entrada.Observacao,
                    entrada.Fotos));
            }
            else
            {
                throw new DomainException("captura_inexistente", "A captura informada não pertence a esta pescaria.");
            }
        }

        _capturas.Clear();
        _capturas.AddRange(novas);

        Validar();
    }

    private void Validar()
    {
        if (Id == Guid.Empty)
        {
            throw new DomainException("pescaria_sem_identidade", "Pescaria sem identificador.");
        }

        if (UsuarioId == Guid.Empty)
        {
            throw new DomainException("pescaria_sem_usuario", "Pescaria precisa pertencer a um usuário.");
        }

        if (string.IsNullOrWhiteSpace(ShareToken))
        {
            throw new DomainException("pescaria_sem_token", "Pescaria sem token de compartilhamento.");
        }

        if (string.IsNullOrWhiteSpace(Local))
        {
            throw new DomainException("pescaria_sem_local", "Informe o local da pescaria.");
        }

        if (Local.Length > 120)
        {
            throw new DomainException("local_longo", "O local deve ter até 120 caracteres.");
        }

        if (Pescador?.Length > 80)
        {
            throw new DomainException("pescador_longo", "O nome do pescador deve ter até 80 caracteres.");
        }

        if (Cidade?.Length > 80)
        {
            throw new DomainException("cidade_longa", "A cidade deve ter até 80 caracteres.");
        }

        if (CorpoOuTrecho?.Length > 80)
        {
            throw new DomainException("corpo_ou_trecho_longo", "O campo rio, lagoa ou trecho deve ter até 80 caracteres.");
        }

        if (Estado is not null && Estado.Length != 2)
        {
            throw new DomainException("estado_invalido", "Informe a UF com duas letras.");
        }

        if (Data == DateOnly.MinValue)
        {
            throw new DomainException("data_invalida", "Informe a data da pescaria.");
        }

        if (!Enum.IsDefined(Ambiente))
        {
            throw new DomainException("ambiente_invalido", "Escolha um ambiente de pesca válido.");
        }

        if (!Enum.IsDefined(Periodo))
        {
            throw new DomainException("periodo_invalido", "Escolha um período do dia válido.");
        }

        if (!Enum.IsDefined(Mare) || !Enum.IsDefined(Lua) || !Enum.IsDefined(Clima) || !Enum.IsDefined(ClarezaDaAgua))
        {
            throw new DomainException("condicao_invalida", "Uma das condições da pescaria é inválida.");
        }

        if (Observacoes?.Length > 1_000)
        {
            throw new DomainException("observacoes_longas", "As observações devem ter até 1.000 caracteres.");
        }

        if (!Enum.IsDefined(PrivacidadeDoLocal))
        {
            throw new DomainException("privacidade_local_invalida", "Escolha uma opção válida de privacidade do local.");
        }

        if (_capturas.Count > 100)
        {
            throw new DomainException("muitas_capturas", "Uma pescaria pode ter no máximo 100 capturas.");
        }

        if (_capturas.Select(captura => captura.Id).Distinct().Count() != _capturas.Count)
        {
            throw new DomainException("captura_duplicada", "Uma captura apareceu mais de uma vez na pescaria.");
        }
    }
}
