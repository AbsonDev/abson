using Fisga.Domain.Pescarias;

namespace Fisga.Application.Abstractions;

public sealed class RecursoNaoEncontradoException(string codigo, string mensagem) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}

public sealed class AcessoNegadoException(string mensagem) : Exception(mensagem);

public sealed class NaoAutenticadoException(string mensagem = "Entre na sua conta para continuar.") : Exception(mensagem);

public interface IPescariaRepository
{
    Task AdicionarAsync(Pescaria pescaria, CancellationToken ct = default);

    Task<Pescaria?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<Pescaria?> ObterPorShareTokenAsync(string shareToken, CancellationToken ct = default);

    Task<IReadOnlyList<Pescaria>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> RemoverPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task SalvarAsync(Pescaria pescaria, CancellationToken ct = default);
}

public interface IMidiaStorage
{
    Task<string> SalvarAsync(Stream conteudo, string contentType, CancellationToken ct = default);

    Task ExcluirAsync(string url, CancellationToken ct = default);
}

public interface IGeradorDePdf
{
    byte[] Gerar(Pescaria pescaria, string urlCompartilhavel);
}

public sealed record ArquivoPdf(byte[] Conteudo, string NomeDoArquivo);

public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

public interface IDomainEventDispatcher
{
    Task PublicarAsync(IReadOnlyCollection<Domain.Common.IDomainEvent> eventos, CancellationToken ct = default);
}
