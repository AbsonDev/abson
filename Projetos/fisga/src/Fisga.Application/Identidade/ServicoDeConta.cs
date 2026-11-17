using System.Text.Json;
using Fisga.Application.Abstractions;
using Fisga.Application.Pescarias;
using Fisga.Domain.Common;

namespace Fisga.Application.Identidade;

public sealed class ServicoDeConta(
    IUsuarioRepository usuarios,
    ISessaoRepository sessoes,
    IPescariaRepository pescarias,
    IMidiaStorage midia,
    IUnitOfWork unidade,
    IRelogio relogio)
{
    public async Task ExcluirAsync(
        Guid usuarioId,
        string? confirmacao,
        CancellationToken ct = default)
    {
        if (!string.Equals(confirmacao?.Trim(), "EXCLUIR", StringComparison.Ordinal))
        {
            throw new DomainException("confirmacao_invalida", "Digite EXCLUIR para confirmar a exclusão da conta.");
        }

        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct)
                       ?? throw new RecursoNaoEncontradoException("usuario_nao_encontrado", "Conta não encontrada.");

        var fotos = await pescarias.RemoverPorUsuarioAsync(usuarioId, ct);
        await sessoes.RevogarTodasAsync(usuarioId, relogio.Agora, exceto: null, ct);
        usuarios.Remover(usuario);
        await unidade.SalvarAsync(ct);

        foreach (var foto in fotos)
        {
            await midia.ExcluirAsync(foto, ct);
        }
    }

    public async Task<ExportacaoDaConta> ExportarAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct)
                       ?? throw new RecursoNaoEncontradoException("usuario_nao_encontrado", "Conta não encontrada.");

        var registros = await pescarias.ListarPorUsuarioAsync(usuarioId, ct);

        return new ExportacaoDaConta(
            ExportadoEm: relogio.Agora,
            Usuario: new ExportacaoDoUsuario(
                usuario.Id,
                usuario.Email.Valor,
                usuario.Nome,
                usuario.AceitaNovidades,
                usuario.CriadoEm,
                usuario.UltimoAcessoEm),
            Pescarias: registros.Select(pescaria => PescariaMapper.ParaDetalhada(pescaria)).ToList());
    }

    public static byte[] Serializar(ExportacaoDaConta exportacao) =>
        JsonSerializer.SerializeToUtf8Bytes(
            exportacao,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
}

public sealed record ExportacaoDoUsuario(
    Guid Id,
    string Email,
    string? Nome,
    bool AceitaNovidades,
    DateTimeOffset CriadoEm,
    DateTimeOffset? UltimoAcessoEm);

public sealed record ExportacaoDaConta(
    DateTimeOffset ExportadoEm,
    ExportacaoDoUsuario Usuario,
    IReadOnlyList<Pescarias.PescariaDetalhadaDto> Pescarias);
