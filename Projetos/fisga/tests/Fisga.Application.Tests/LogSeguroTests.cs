using Fisga.Application.Common;

namespace Fisga.Application.Tests;

public sealed class LogSeguroTests
{
    [Fact]
    public void ParaLog_RemoveQuebraDeLinha()
    {
        Assert.Equal("a b", LogSeguro.ParaLog("a\nb"));
        Assert.Equal("a b", LogSeguro.ParaLog("a\r\nb"));
    }

    [Fact]
    public void ParaLog_RemoveCaracteresDeControle()
    {
        Assert.Equal("ab", LogSeguro.ParaLog("a\u0000b"));
    }

    [Fact]
    public void ParaLog_TruncaNoLimite()
    {
        Assert.Equal("abc", LogSeguro.ParaLog("abcdef", 3));
    }

    [Fact]
    public void ParaLog_VazioViraHifen()
    {
        Assert.Equal("-", LogSeguro.ParaLog(null));
        Assert.Equal("-", LogSeguro.ParaLog("   "));
    }
}
