using Fisga.Application.Catalogo;
using Fisga.Application.Identidade;
using Fisga.Application.Newsletter;
using Fisga.Application.Pescarias;
using Fisga.Domain.Identidade;

namespace Fisga.Application.Tests;

public sealed class ServicoDeNewsletterTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    private sealed record Cenario(
        ServicoDeNewsletter Servico,
        UsuarioRepositorioFake Usuarios,
        EmailSenderFake Email,
        UnitOfWorkFake Unidade,
        Usuario Usuario,
        Usuario Outro);

    private static Cenario Criar()
    {
        var usuarios = new UsuarioRepositorioFake();
        var pescarias = new PescariaRepositorioFake();
        var email = new EmailSenderFake();
        var unidade = new UnitOfWorkFake();
        var relogio = new RelogioFixo(Agora);

        var usuario = Usuario.Criar(Email.Criar("news@exemplo.com"), "Pescador", Agora);
        usuario.AtualizarPerfil("Pescador", true, Agora);
        usuarios.Adicionar(usuario);

        var outro = Usuario.Criar(Email.Criar("sem@exemplo.com"), "Sem News", Agora);
        usuarios.Adicionar(outro);

        pescarias.Pescarias.Add(PescariaDeTeste.Criar(usuario.Id));

        var diario = new ServicoDoDiario(pescarias, new CatalogoDePesca());
        var opcoes = new NewsletterOptions
        {
            Secret = "segredo-de-teste",
            AdminToken = "admin-de-teste",
            MaximoPorEnvio = 10
        };
        var auth = new AutenticacaoOptions
        {
            LinkBaseUrl = "https://api.fisga.test",
            WebUrl = "https://fisga.test"
        };

        var servico = new ServicoDeNewsletter(usuarios, diario, email, unidade, relogio, auth, opcoes);
        return new Cenario(servico, usuarios, email, unidade, usuario, outro);
    }

    [Fact]
    public async Task Simulacao_nao_envia_e_conta_apenas_opt_in()
    {
        var cenario = Criar();

        var resultado = await cenario.Servico.EnviarAsync("2026-11", simulacao: true);

        Assert.Equal("2026-11", resultado.Mes);
        Assert.Equal(1, resultado.Elegiveis);
        Assert.Equal(0, resultado.Enviados);
        Assert.True(resultado.Simulacao);
        Assert.Empty(cenario.Email.Mensagens);
    }

    [Fact]
    public async Task Envia_digest_personalizado_com_link_de_descadastro()
    {
        var cenario = Criar();

        var resultado = await cenario.Servico.EnviarAsync("2026-11");

        Assert.Equal(1, resultado.Elegiveis);
        Assert.Equal(1, resultado.Enviados);
        Assert.Equal(0, resultado.Falhas);

        var mensagem = Assert.Single(cenario.Email.Mensagens);
        Assert.Equal("news@exemplo.com", mensagem.Destino);
        Assert.Contains("Novembro", mensagem.Assunto);
        Assert.Contains("Seu mês de pesca", mensagem.Html);
        Assert.Contains("Tucunaré", mensagem.Html);
        Assert.Contains("newsletter/descadastrar", mensagem.Descadastro);
    }

    [Fact]
    public async Task Descadastro_valido_desativa_preferencia()
    {
        var cenario = Criar();
        var token = AssinaturaDeDescadastro.Gerar(cenario.Usuario.Id, "segredo-de-teste");

        var ok = await cenario.Servico.DescadastrarAsync(token);

        Assert.True(ok);
        Assert.False(cenario.Usuario.AceitaNovidades);
        Assert.True(cenario.Unidade.Salvamentos > 0);
    }

    [Fact]
    public async Task Descadastro_com_token_invalido_nao_alterar()
    {
        var cenario = Criar();

        var ok = await cenario.Servico.DescadastrarAsync("token-invalido");

        Assert.False(ok);
        Assert.True(cenario.Usuario.AceitaNovidades);
    }

    [Fact]
    public void Assinatura_detecta_adulteracao()
    {
        var id = Guid.NewGuid();
        var token = AssinaturaDeDescadastro.Gerar(id, "segredo");

        Assert.Equal(id, AssinaturaDeDescadastro.Validar(token, "segredo"));
        Assert.Null(AssinaturaDeDescadastro.Validar(token + "x", "segredo"));
        Assert.Null(AssinaturaDeDescadastro.Validar(token, "outro-segredo"));
    }
}
