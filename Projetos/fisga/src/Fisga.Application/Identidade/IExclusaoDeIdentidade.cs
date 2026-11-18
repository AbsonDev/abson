using Fisga.Domain.Identidade;

namespace Fisga.Application.Identidade;

/// <summary>
/// Remove a credencial de autenticação da Identity e os tokens sem FK de uma
/// conta excluída. Deve rodar ANTES da remoção do usuário de domínio: se a
/// limpeza seguinte falhar, a conta fica inutilizável e o suporte pode repetir.
/// </summary>
public interface IExclusaoDeIdentidade
{
    Task ExcluirAsync(Usuario usuario, CancellationToken ct = default);
}
