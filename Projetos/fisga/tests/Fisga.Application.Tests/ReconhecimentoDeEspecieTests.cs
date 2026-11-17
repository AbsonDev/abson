using Fisga.Application.Vision;

namespace Fisga.Application.Tests;

public sealed class ReconhecimentoDeEspecieTests
{
    [Fact]
    public async Task Sem_provedor_configurado_nao_inventa_especie()
    {
        var reconhecedor = new ManualReconhecedor();

        using var imagem = new MemoryStream([1, 2, 3]);
        var resultado = await reconhecedor.SugerirAsync(imagem, "image/jpeg");

        Assert.False(resultado.Disponivel);
        Assert.Empty(resultado.Sugestoes);
        Assert.Contains("manualmente", resultado.Mensagem);
    }
}
