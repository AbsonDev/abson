using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Domain.Identidade;
using Fisga.Domain.Pescarias;

namespace Fisga.Application.Tests;

internal sealed class PescariaRepositorioFake : IPescariaRepository
{
    public List<Pescaria> Pescarias { get; } = [];

    public Task AdicionarAsync(Pescaria pescaria, CancellationToken ct = default)
    {
        Pescarias.Add(pescaria);
        return Task.CompletedTask;
    }

    public Task<Pescaria?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Pescarias.FirstOrDefault(pescaria => pescaria.Id == id));

    public Task<Pescaria?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default) =>
        Task.FromResult(Pescarias.FirstOrDefault(pescaria => pescaria.ShareToken == shareToken));

    public Task<IReadOnlyList<Pescaria>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Pescaria>>(
            Pescarias.Where(pescaria => pescaria.UsuarioId == usuarioId).ToList());

    public Task<IReadOnlyList<string>> RemoverPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var alvo = Pescarias.Where(pescaria => pescaria.UsuarioId == usuarioId).ToList();
        var fotos = alvo
            .SelectMany(pescaria => pescaria.Capturas)
            .Select(captura => captura.FotoUrl)
            .Where(foto => !string.IsNullOrWhiteSpace(foto))
            .Select(foto => foto!)
            .Distinct()
            .ToList();

        foreach (var pescaria in alvo)
        {
            Pescarias.Remove(pescaria);
        }

        return Task.FromResult<IReadOnlyList<string>>(fotos);
    }

    public Task SalvarAsync(Pescaria pescaria, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class UsuarioRepositorioFake : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = [];

    public Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(usuario => usuario.Email.Valor == email.Valor));

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(usuario => usuario.Id == id));

    public void Adicionar(Usuario usuario) => Usuarios.Add(usuario);

    public void Remover(Usuario usuario) => Usuarios.Remove(usuario);
}

internal sealed class SessaoRepositorioFake : ISessaoRepository
{
    public List<Sessao> Sessoes { get; } = [];

    public Task<Sessao?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(Sessoes.FirstOrDefault(sessao => sessao.TokenHash == tokenHash));

    public Task<IReadOnlyList<Sessao>> ListarAtivasAsync(
        Guid usuarioId,
        DateTimeOffset agora,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Sessao>>(Sessoes
            .Where(sessao => sessao.UsuarioId == usuarioId && sessao.EstaAtiva(agora))
            .OrderByDescending(sessao => sessao.UltimoUsoEm)
            .ToList());

    public void Adicionar(Sessao sessao) => Sessoes.Add(sessao);

    public Task RevogarTodasAsync(
        Guid usuarioId,
        DateTimeOffset agora,
        Guid? exceto,
        CancellationToken ct = default)
    {
        foreach (var sessao in Sessoes.Where(sessao =>
                     sessao.UsuarioId == usuarioId
                     && sessao.RevogadaEm is null
                     && (exceto is null || sessao.Id != exceto)))
        {
            sessao.Revogar(agora);
        }

        return Task.CompletedTask;
    }
}

internal sealed class TokenDeLoginRepositorioFake : ITokenDeLoginRepository
{
    public List<TokenDeLogin> Tokens { get; } = [];

    public Task<TokenDeLogin?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

    public Task<TokenDeLogin?> ObterUltimoPorEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(Tokens
            .Where(token => token.Email == email)
            .OrderByDescending(token => token.CriadoEm)
            .FirstOrDefault());

    public Task InvalidarPendentesAsync(
        string email,
        DateTimeOffset agora,
        CancellationToken ct = default)
    {
        foreach (var token in Tokens.Where(token => token.Email == email && token.UsadoEm is null))
        {
            token.Invalidar(agora);
        }

        return Task.CompletedTask;
    }

    public void Adicionar(TokenDeLogin token) => Tokens.Add(token);
}

internal sealed class EmailSenderFake : IEmailSender
{
    public List<(string Destino, string Url)> Enviados { get; } = [];

    public Task EnviarLinkDeAcessoAsync(
        string destino,
        string? nome,
        string url,
        DateTimeOffset expiraEm,
        CancellationToken ct = default)
    {
        Enviados.Add((destino, url));
        return Task.CompletedTask;
    }
}

internal sealed class UnitOfWorkFake : IUnitOfWork
{
    public int Salvamentos { get; private set; }

    public Task<int> SalvarAsync(CancellationToken ct = default)
    {
        Salvamentos++;
        return Task.FromResult(Salvamentos);
    }
}

internal sealed class MidiaStorageFake : IMidiaStorage
{
    public List<string> Excluidos { get; } = [];

    public Task<string> SalvarAsync(Stream conteudo, string contentType, CancellationToken ct = default) =>
        Task.FromResult("/media/teste.jpg");

    public Task ExcluirAsync(string url, CancellationToken ct = default)
    {
        Excluidos.Add(url);
        return Task.CompletedTask;
    }
}

internal sealed class RelogioFixo(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Agora { get; set; } = agora;

    public void Avancar(TimeSpan tempo) => Agora = Agora.Add(tempo);
}

internal sealed class DispatcherFake : IDomainEventDispatcher
{
    public List<Domain.Common.IDomainEvent> Publicados { get; } = [];

    public Task PublicarAsync(IReadOnlyCollection<Domain.Common.IDomainEvent> eventos, CancellationToken ct = default)
    {
        Publicados.AddRange(eventos);
        return Task.CompletedTask;
    }
}

internal static class PescariaDeTeste
{
    public static Pescaria Criar(Guid usuarioId, string local = "Represa do Lago Azul")
    {
        return Pescaria.Criar(
            new DadosDaPescaria
            {
                Local = local,
                Pescador = "Abson",
                Cidade = "Campinas",
                Estado = "SP",
                Ambiente = AmbienteDePesca.Represa,
                Data = new DateOnly(2026, 11, 16),
                Periodo = PeriodoDoDia.Manha,
                Lua = FaseDaLua.Crescente,
                Clima = Clima.Sol,
                ClarezaDaAgua = ClarezaDaAgua.Turva,
                Capturas =
                [
                    new DadosDaCaptura(null, "Tucunaré", 2.4m, 48m, "Popper", true, "/media/tucunare.jpg", null),
                    new DadosDaCaptura(null, "Tilápia", 0.8m, 28m, "Minhoca", false, null, null)
                ]
            },
            Guid.NewGuid(),
            usuarioId,
            Guid.NewGuid().ToString("N"),
            new DateTimeOffset(2026, 11, 16, 6, 30, 0, TimeSpan.FromHours(-3)));
    }
}
