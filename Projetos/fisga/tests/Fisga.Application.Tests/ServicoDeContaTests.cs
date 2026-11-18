using Fisga.Application.Identidade;
using Fisga.Domain.Common;
using Fisga.Domain.Identidade;

namespace Fisga.Application.Tests;

public sealed class ServicoDeContaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 6, 30, 0, TimeSpan.FromHours(-3));

    private sealed record Cenario(
        ServicoDeConta Servico,
        UsuarioRepositorioFake Usuarios,
        SessaoRepositorioFake Sessoes,
        PescariaRepositorioFake Pescarias,
        MidiaStorageFake Midia,
        ExclusaoDeIdentidadeFake Identidade,
        UnitOfWorkFake Unidade,
        Usuario Usuario);

    private static Cenario Criar()
    {
        var usuarios = new UsuarioRepositorioFake();
        var sessoes = new SessaoRepositorioFake();
        var pescarias = new PescariaRepositorioFake();
        var midia = new MidiaStorageFake();
        var identidade = new ExclusaoDeIdentidadeFake();
        var unidade = new UnitOfWorkFake();
        var relogio = new RelogioFixo(Agora);

        var usuario = Usuario.Criar(Email.Criar("abson@exemplo.com"), "Abson", Agora);
        usuarios.Adicionar(usuario);
        pescarias.Pescarias.Add(PescariaDeTeste.Criar(usuario.Id));
        sessoes.Sessoes.Add(Sessao.Criar(
            usuario.Id,
            new string('a', 64),
            "Playwright",
            null,
            Agora,
            TimeSpan.FromDays(30)));

        var servico = new ServicoDeConta(usuarios, sessoes, pescarias, midia, identidade, unidade, relogio);
        return new Cenario(servico, usuarios, sessoes, pescarias, midia, identidade, unidade, usuario);
    }

    [Fact]
    public async Task Excluir_conta_remove_diario_sessoes_e_fotos()
    {
        var cenario = Criar();

        await cenario.Servico.ExcluirAsync(cenario.Usuario.Id, "EXCLUIR");

        Assert.Empty(cenario.Usuarios.Usuarios);
        Assert.Contains(cenario.Usuario.Id, cenario.Identidade.Excluidos);
        Assert.Empty(cenario.Pescarias.Pescarias);
        Assert.All(cenario.Sessoes.Sessoes, sessao => Assert.NotNull(sessao.RevogadaEm));
        Assert.Contains("/media/tucunare.jpg", cenario.Midia.Excluidos);
        Assert.True(cenario.Unidade.Salvamentos > 0);
    }

    [Fact]
    public async Task Excluir_exige_confirmacao_digitada()
    {
        var cenario = Criar();

        var excecao = await Assert.ThrowsAsync<DomainException>(() =>
            cenario.Servico.ExcluirAsync(cenario.Usuario.Id, "sim"));

        Assert.Equal("confirmacao_invalida", excecao.Codigo);
        Assert.Single(cenario.Usuarios.Usuarios);
        Assert.Single(cenario.Pescarias.Pescarias);
    }

    [Fact]
    public async Task Exportar_devolve_conta_e_pescarias()
    {
        var cenario = Criar();

        var exportacao = await cenario.Servico.ExportarAsync(cenario.Usuario.Id);

        Assert.Equal("abson@exemplo.com", exportacao.Usuario.Email);
        Assert.Single(exportacao.Pescarias);
        Assert.Equal("Represa do Lago Azul", exportacao.Pescarias[0].Entrada.Local);
        Assert.NotEmpty(ServicoDeConta.Serializar(exportacao));
    }
}
