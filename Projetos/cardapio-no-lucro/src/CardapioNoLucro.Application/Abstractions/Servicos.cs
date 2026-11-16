using CardapioNoLucro.Domain.Common;
using CardapioNoLucro.Domain.Fichas;

namespace CardapioNoLucro.Application.Abstractions;

public interface IGeradorDePdf
{
    byte[] Gerar(FichaTecnica ficha, ResultadoDaFicha resultado, string urlCompartilhavel);
}

public sealed record ArquivoPdf(byte[] Conteudo, string NomeDoArquivo);

public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

public interface IDomainEventDispatcher
{
    Task PublicarAsync(IReadOnlyCollection<IDomainEvent> eventos, CancellationToken ct = default);
}
