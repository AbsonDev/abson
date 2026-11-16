using CardapioNoLucro.Application.Fichas;

namespace CardapioNoLucro.Api.Contracts;

public sealed record FichaSalvaResponse(
    Guid Id,
    string ShareToken,
    string EditToken,
    string Url);

public sealed record ErroResponse(string Codigo, string Detalhe);

public sealed record CanalDto(
    string Tipo,
    string Nome,
    string Descricao,
    decimal ComissaoPercentual,
    decimal TaxaFixaPorPedido,
    bool Ativo);
