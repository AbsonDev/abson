using Fisga.Domain.Common;
using Fisga.Domain.Identidade;

namespace Fisga.Domain.Tests;

public sealed class ConsentimentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 8, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Criar_rejeita_usuario_vazio()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Consentimento.Criar(Guid.Empty, TipoDeConsentimento.Termos, "2026.1", Agora));

        Assert.Equal("consentimento_sem_usuario", excecao.Codigo);
    }

    [Fact]
    public void Criar_rejeita_tipo_indefinido()
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Consentimento.Criar(Guid.NewGuid(), (TipoDeConsentimento)999, "2026.1", Agora));

        Assert.Equal("consentimento_tipo_invalido", excecao.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_rejeita_versao_vazia(string versao)
    {
        var excecao = Assert.Throws<DomainException>(() =>
            Consentimento.Criar(Guid.NewGuid(), TipoDeConsentimento.Termos, versao, Agora));

        Assert.Equal("consentimento_versao_invalida", excecao.Codigo);
    }

    [Fact]
    public void Criar_rejeita_versao_com_mais_de_64_caracteres()
    {
        var versao = new string('a', 65);

        var excecao = Assert.Throws<DomainException>(() =>
            Consentimento.Criar(Guid.NewGuid(), TipoDeConsentimento.Termos, versao, Agora));

        Assert.Equal("consentimento_versao_invalida", excecao.Codigo);
    }

    [Fact]
    public void Criar_aceita_registro_valido()
    {
        var usuarioId = Guid.NewGuid();

        var consentimento = Consentimento.Criar(
            usuarioId,
            TipoDeConsentimento.Privacidade,
            "  2026.2  ",
            Agora);

        Assert.NotEqual(Guid.Empty, consentimento.Id);
        Assert.Equal(usuarioId, consentimento.UsuarioId);
        Assert.Equal(TipoDeConsentimento.Privacidade, consentimento.Tipo);
        Assert.Equal("2026.2", consentimento.Versao);
        Assert.Equal(Agora, consentimento.AceitoEm);
    }
}
