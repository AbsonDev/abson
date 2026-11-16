using Precifica.Application.Seguranca;

namespace Precifica.Application.Tests;

public sealed class TokensSegurosTests
{
    [Fact]
    public void Hash_confere_com_token_correto()
    {
        var token = TokensSeguros.Novo(24);
        var hash = TokensSeguros.Hash(token);

        Assert.Equal(64, hash.Length);
        Assert.True(TokensSeguros.Correspondem(token, hash));
        Assert.False(TokensSeguros.Correspondem("outro-token", hash));
    }

    [Fact]
    public void Hash_malformado_nao_quebra_a_verificacao()
    {
        Assert.False(TokensSeguros.Correspondem("token", "zzzz"));
        Assert.False(TokensSeguros.Correspondem("", ""));
    }
}
