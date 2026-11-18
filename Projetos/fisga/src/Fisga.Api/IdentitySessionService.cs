using System.Security.Claims;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Application.Seguranca;
using Fisga.Domain.Identidade;
using Fisga.Domain.Identidade.Eventos;
using Fisga.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Fisga.Api;

/// <summary>
/// Ponte entre o cookie da Identity (<c>fisga_auth</c>) e a linha de
/// <see cref="Sessao"/> que representa o dispositivo. A sessão é gravada antes de
/// emitir o cookie: o claim <c>sessao_id</c> aponta para a linha e o TokenHash é o
/// hash desse id. O cookie é a única autenticação; a linha é apenas o registro
/// de dispositivo/revogação.
/// </summary>
public sealed class IdentitySessionService(
    SignInManager<ApplicationUser> signIn,
    ISessaoRepository sessoes,
    IUnitOfWork unidade,
    IRelogio relogio,
    IDomainEventDispatcher eventos,
    AutenticacaoOptions opcoes)
{
    private const string ClaimSessaoId = "sessao_id";

    public async Task AssinarAsync(
        ApplicationUser conta,
        Usuario usuario,
        HttpContext http,
        CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var agora = relogio.Agora;
        var userAgent = http.Request.Headers.UserAgent.ToString();
        var ip = http.Connection.RemoteIpAddress?.ToString();

        var sessao = Sessao.Criar(
            id,
            usuario.Id,
            TokensSeguros.Hash(id.ToString()),
            userAgent,
            ip is null ? null : TokensSeguros.Hash(ip),
            agora,
            TimeSpan.FromDays(opcoes.DuracaoDaSessaoEmDias));

        sessoes.Adicionar(sessao);
        await unidade.SalvarAsync(ct);

        // CheckPasswordSignInAsync não assina: a emissão do cookie acontece aqui,
        // uma única vez, já com o claim que liga o cookie ao dispositivo.
        await signIn.SignInWithClaimsAsync(
            conta,
            new AuthenticationProperties { IsPersistent = true },
            [new Claim(ClaimSessaoId, id.ToString())]);

        var pendentes = usuario.Eventos
            .Concat([new SessaoIniciada(usuario.Id, sessao.Id, agora)])
            .ToList();
        await eventos.PublicarAsync(pendentes, ct);
        usuario.LimparEventos();

        http.DefinirSessao(sessao, usuario);
    }
}
