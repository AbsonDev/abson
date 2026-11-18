using Fisga.Application.Abstractions;
using Fisga.Application.Condicoes;
using Fisga.Application.Identidade;
using Fisga.Application.Locais;
using Fisga.Application.Pontos;
using Fisga.Application.Trilhas;
using Fisga.Domain.Identidade;
using Fisga.Domain.Locais;
using Fisga.Domain.Pontos;
using Fisga.Domain.Trilhas;
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

internal sealed class LocalDePescaRepositorioFake : ILocalDePescaRepository
{
    public List<LocalDePesca> Locais { get; } = [];

    public Task<IReadOnlyList<LocalDePesca>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LocalDePesca>>(
            Locais.Where(local => local.UsuarioId == usuarioId).ToList());

    public Task<LocalDePesca?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Locais.FirstOrDefault(local => local.Id == id));

    public Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult(Locais.Count(local => local.UsuarioId == usuarioId));

    public Task AdicionarAsync(LocalDePesca local, CancellationToken ct = default)
    {
        Locais.Add(local);
        return Task.CompletedTask;
    }

    public Task SalvarAsync(LocalDePesca local, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoverAsync(LocalDePesca local, CancellationToken ct = default)
    {
        Locais.Remove(local);
        return Task.CompletedTask;
    }

    public Task DesmarcarPadraoAsync(Guid usuarioId, CancellationToken ct = default)
    {
        foreach (var local in Locais.Where(local => local.UsuarioId == usuarioId && local.Padrao))
        {
            local.DefinirPadrao(false, DateTimeOffset.UtcNow);
        }

        return Task.CompletedTask;
    }
}

internal sealed class PontoPrivadoRepositorioFake : IPontoPrivadoRepository
{
    public List<PontoPrivado> Pontos { get; } = [];

    public Task<IReadOnlyList<PontoPrivado>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PontoPrivado>>(
            Pontos.Where(ponto => ponto.UsuarioId == usuarioId).ToList());

    public Task<PontoPrivado?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Pontos.FirstOrDefault(ponto => ponto.Id == id));

    public Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult(Pontos.Count(ponto => ponto.UsuarioId == usuarioId));

    public Task AdicionarAsync(PontoPrivado ponto, CancellationToken ct = default)
    {
        Pontos.Add(ponto);
        return Task.CompletedTask;
    }

    public Task SalvarAsync(PontoPrivado ponto, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoverAsync(PontoPrivado ponto, CancellationToken ct = default)
    {
        Pontos.Remove(ponto);
        return Task.CompletedTask;
    }
}

internal sealed class TrilhaPrivadaRepositorioFake : ITrilhaPrivadaRepository
{
    public List<TrilhaPrivada> Trilhas { get; } = [];

    public Task<IReadOnlyList<TrilhaPrivada>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TrilhaPrivada>>(
            Trilhas.Where(trilha => trilha.UsuarioId == usuarioId).ToList());

    public Task<TrilhaPrivada?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Trilhas.FirstOrDefault(trilha => trilha.Id == id));

    public Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult(Trilhas.Count(trilha => trilha.UsuarioId == usuarioId));

    public Task AdicionarAsync(TrilhaPrivada trilha, CancellationToken ct = default)
    {
        Trilhas.Add(trilha);
        return Task.CompletedTask;
    }

    public Task SalvarAsync(TrilhaPrivada trilha, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoverAsync(TrilhaPrivada trilha, CancellationToken ct = default)
    {
        Trilhas.Remove(trilha);
        return Task.CompletedTask;
    }
}

internal sealed class ProvedorDeCondicoesFake(DadosDeCondicoes dados) : IProvedorDeCondicoes
{
    public Task<DadosDeCondicoes?> ObterAsync(
        double latitude,
        double longitude,
        DateOnly diaLocal,
        CancellationToken ct = default) =>
        Task.FromResult<DadosDeCondicoes?>(dados);
}

internal sealed class GeocodificadorFake : IGeocodificador
{
    public Task<IReadOnlyList<ResultadoDeBusca>> BuscarAsync(
        string termo,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ResultadoDeBusca>>(
            [new ResultadoDeBusca("Aracaju", "Aracaju", "SE", -10.911, -37.071, "Brasil")]);

    public Task<ResultadoDeBusca?> ReversoAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default) =>
        Task.FromResult<ResultadoDeBusca?>(
            new ResultadoDeBusca("Aracaju", "Aracaju", "SE", latitude, longitude, "Brasil"));
}

internal sealed class UsuarioRepositorioFake : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = [];

    public Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(usuario => usuario.Email.Valor == email.Valor));

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(usuario => usuario.Id == id));

    public Task<IReadOnlyList<Usuario>> ListarAceitaNovidadesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Usuario>>(Usuarios.Where(usuario => usuario.AceitaNovidades).ToList());

    public void Adicionar(Usuario usuario) => Usuarios.Add(usuario);

    public void Remover(Usuario usuario) => Usuarios.Remove(usuario);
}

internal sealed class SessaoRepositorioFake : ISessaoRepository
{
    public List<Sessao> Sessoes { get; } = [];

    public Task<Sessao?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Sessoes.FirstOrDefault(sessao => sessao.Id == id));

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

    public Task EnviarAsync(
        string destino,
        string assunto,
        string html,
        string texto,
        string? listUnsubscribeUrl = null,
        CancellationToken ct = default)
    {
        Enviados.Add((destino, assunto));
        Mensagens.Add((destino, assunto, html, texto, listUnsubscribeUrl));
        return Task.CompletedTask;
    }

    public List<(string Destino, string Assunto, string Html, string Texto, string? Descadastro)> Mensagens { get; } = [];
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

internal sealed class ExclusaoDeIdentidadeFake : IExclusaoDeIdentidade
{
    public List<Guid> Excluidos { get; } = [];

    public Task ExcluirAsync(Usuario usuario, CancellationToken ct = default)
    {
        Excluidos.Add(usuario.Id);
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
