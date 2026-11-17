namespace Fisga.Domain.Common;

/// <summary>
/// Raiz de agregado: única porta de entrada para mutações e fonte dos eventos de domínio.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _eventos = [];

    public IReadOnlyCollection<IDomainEvent> Eventos => _eventos;

    protected void Registrar(IDomainEvent evento) => _eventos.Add(evento);

    public void LimparEventos() => _eventos.Clear();
}
