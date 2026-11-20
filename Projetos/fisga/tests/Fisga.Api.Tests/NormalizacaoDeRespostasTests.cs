using Fisga.Api.OpenApi;
using Fisga.Api.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace Fisga.Api.Tests;

// Cobre a normalização que o transformer aplica depois que a metadata Produces<T>
// gerou a operação. A entrada é um OpenApiOperation montado à mão — sem
// ApiDescription e sem host — para o teste falhar por lógica do transformer, não
// por configuração de endpoint. O documento publicado é coberto por
// ContratoTests; aqui o alvo são media types, headers e descrições.
public sealed class NormalizacaoDeRespostasTests
{
    [Fact]
    public void ErroComCorpoGanhaEspelhoEmJsonComADescricaoDoCatalogo()
    {
        var schemaDoErro = new OpenApiSchema();
        var mediaTypeOriginal = new OpenApiMediaType { Schema = schemaDoErro };
        var resposta = new OpenApiResponse
        {
            // A metadata Produces<T> gera a reason phrase como descrição: é o
            // default do framework, então o catálogo pode escrever por cima.
            Description = ReasonPhrases.GetReasonPhrase(400),
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [RespostasPadrao.TipoProblema] = mediaTypeOriginal
            }
        };

        ContratoOperationTransformer.NormalizarRespostas(Operacao("400", resposta));

        Assert.True(resposta.Content!.ContainsKey(RespostasPadrao.TipoProblema));
        Assert.True(resposta.Content.ContainsKey(RespostasPadrao.TipoJson));

        var espelho = resposta.Content[RespostasPadrao.TipoJson];
        Assert.NotSame(mediaTypeOriginal, espelho);
        Assert.Same(schemaDoErro, espelho.Schema);
        Assert.Same(schemaDoErro, resposta.Content[RespostasPadrao.TipoProblema].Schema);
        Assert.Equal(RespostasPadrao.DescricaoComErro(400), resposta.Description);
    }

    [Fact]
    public void CriadoGanhaHeaderLocation()
    {
        var resposta = new OpenApiResponse();

        ContratoOperationTransformer.NormalizarRespostas(Operacao("201", resposta));

        var header = Assert.Contains("Location", resposta.Headers!);
        Assert.Equal(JsonSchemaType.String, header.Schema!.Type);
        Assert.Equal("uri", header.Schema.Format);
        Assert.Equal(RespostasPadrao.DescricaoSemSchema(201), resposta.Description);
    }

    [Fact]
    public void RateLimitGanhaRetryAfterComADescricaoDaRejeicao()
    {
        var resposta = new OpenApiResponse();

        ContratoOperationTransformer.NormalizarRespostas(Operacao("429", resposta));

        var header = Assert.Contains("Retry-After", resposta.Headers!);
        Assert.Equal(RejeicaoDoRateLimiter.DescricaoDoHeader, header.Description);
        Assert.Equal(JsonSchemaType.Integer, header.Schema!.Type);
        Assert.Equal("int32", header.Schema.Format);
        Assert.Equal(RespostasPadrao.DescricaoSemSchema(429), resposta.Description);
        Assert.Null(resposta.Content);
    }

    [Fact]
    public void SemCorpoRecebeDescricaoESemHeaders()
    {
        var resposta = new OpenApiResponse();

        ContratoOperationTransformer.NormalizarRespostas(Operacao("204", resposta));

        Assert.Equal(RespostasPadrao.DescricaoSemSchema(204), resposta.Description);
        Assert.Null(resposta.Headers);
        Assert.Null(resposta.Content);
    }

    // A descrição do catálogo é default, não imposição: um handler que escreve a
    // própria descrição de erro mantém o texto, mas o espelho em JSON continua
    // sendo necessário para o cliente.
    [Fact]
    public void DescricaoEspecificaNaoEhSobrescritaPeloCatalogo()
    {
        var resposta = new OpenApiResponse
        {
            Description = "detalhe de negócio",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [RespostasPadrao.TipoProblema] = new() { Schema = new OpenApiSchema() }
            }
        };

        ContratoOperationTransformer.NormalizarRespostas(Operacao("400", resposta));

        Assert.Equal("detalhe de negócio", resposta.Description);
        Assert.True(resposta.Content!.ContainsKey(RespostasPadrao.TipoJson));
    }

    [Fact]
    public void DescricaoEspecificaDeStatusSemSchemaEhPreservada()
    {
        var resposta = new OpenApiResponse { Description = "sem corpo por decisão" };

        ContratoOperationTransformer.NormalizarRespostas(Operacao("204", resposta));

        Assert.Equal("sem corpo por decisão", resposta.Description);
        Assert.Null(resposta.Headers);
        Assert.Null(resposta.Content);
    }

    // 200 não é status do catálogo de erro nem de corpo vazio: nada muda, e a
    // resposta não ganha um objeto "headers" vazio no documento publicado.
    [Fact]
    public void SucessoComCorpoFicaIntacto()
    {
        var resposta = new OpenApiResponse
        {
            Description = "descrição gerada pelo framework",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [RespostasPadrao.TipoJson] = new() { Schema = new OpenApiSchema() }
            }
        };

        ContratoOperationTransformer.NormalizarRespostas(Operacao("200", resposta));

        Assert.Equal("descrição gerada pelo framework", resposta.Description);
        Assert.Null(resposta.Headers);
        Assert.Single(resposta.Content!);
    }

    // `byte[]` na metadata vira `format: byte` (base64), mas o handler escreve os
    // bytes crus: publicar `byte` faria o cliente gerado decodificar e corromper
    // o PDF. O transformer corrige o formato do media type de PDF.
    [Fact]
    public void PdfTrocaFormatBytePorBinary()
    {
        var resposta = new OpenApiResponse
        {
            Description = "OK",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [RespostasPadrao.TipoPdf] = new()
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" }
                }
            }
        };

        ContratoOperationTransformer.NormalizarRespostas(Operacao("200", resposta));

        var pdf = Assert.Contains(RespostasPadrao.TipoPdf, resposta.Content!);
        Assert.Equal(JsonSchemaType.String, pdf.Schema!.Type);
        Assert.Equal("binary", pdf.Schema.Format);
        Assert.Equal("OK", resposta.Description);
    }

    private static OpenApiOperation Operacao(string status, OpenApiResponse resposta) =>
        new()
        {
            Responses = new OpenApiResponses { [status] = resposta }
        };
}
