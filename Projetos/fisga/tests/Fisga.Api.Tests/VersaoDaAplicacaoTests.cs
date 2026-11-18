using Fisga.Api;

namespace Fisga.Api.Tests;

public class VersaoDaAplicacaoTests
{
    [Fact]
    public void Usa_git_sha_quando_presente()
    {
        Assert.Equal("abc123", VersaoDaAplicacao.Resolver("abc123", "def456"));
    }

    [Fact]
    public void Usa_source_commit_quando_git_sha_ausente()
    {
        Assert.Equal("def456", VersaoDaAplicacao.Resolver(null, "def456"));
    }

    [Fact]
    public void Usa_dev_quando_ambos_ausentes()
    {
        Assert.Equal("dev", VersaoDaAplicacao.Resolver(null, null));
    }

    [Theory]
    [InlineData("", "def456", "def456")]
    [InlineData("   ", "def456", "def456")]
    [InlineData("abc123", "", "abc123")]
    [InlineData("", "", "dev")]
    public void Ignora_valores_em_branco(string? gitSha, string? sourceCommit, string esperado)
    {
        Assert.Equal(esperado, VersaoDaAplicacao.Resolver(gitSha, sourceCommit));
    }
}
