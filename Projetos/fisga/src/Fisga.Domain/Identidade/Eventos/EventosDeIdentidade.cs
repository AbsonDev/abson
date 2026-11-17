using Fisga.Domain.Common;

namespace Fisga.Domain.Identidade.Eventos;

public sealed record UsuarioCriado(
    Guid UsuarioId,
    string Email,
    DateTimeOffset OcorridoEm) : IDomainEvent;

public sealed record LoginSolicitado(
    string Email,
    DateTimeOffset ExpiraEm,
    DateTimeOffset OcorridoEm) : IDomainEvent;

public sealed record SessaoIniciada(
    Guid UsuarioId,
    Guid SessaoId,
    DateTimeOffset OcorridoEm) : IDomainEvent;
