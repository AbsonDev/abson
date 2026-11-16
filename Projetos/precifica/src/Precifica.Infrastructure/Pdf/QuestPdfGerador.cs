using System.Globalization;
using Microsoft.Extensions.Options;
using Precifica.Application.Abstractions;
using Precifica.Application.Catalogo;
using Precifica.Domain.Common;
using Precifica.Domain.Precificacao;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Precifica.Infrastructure.Pdf;

public sealed class QuestPdfGerador(ICatalogoDeNichos catalogo) : IGeradorDePdf
{
    private static readonly Color Tinta = Color.FromHex("#231F1C");
    private static readonly Color Suave = Color.FromHex("#6F665E");
    private static readonly Color Terracota = Color.FromHex("#B94B2A");
    private static readonly Color Creme = Color.FromHex("#FBF5EC");
    private static readonly Color Linha = Color.FromHex("#E9DFD3");
    private static readonly Color Verde = Color.FromHex("#5F7357");
    private static readonly Color Areia = Color.FromHex("#F2E9DE");
    private static readonly Lock TravaFontes = new();
    private static bool _fontesRegistradas;

    static QuestPdfGerador()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Gerar(
        Calculo calculo,
        ResultadoDaPrecificacao resultado,
        bool comMarcaDagua,
        string urlCompartilhavel)
    {
        GarantirFontes();

        var modelo = catalogo.Obter(calculo.Nicho);
        var nomeDoNicho = modelo?.Nome ?? calculo.Nicho.ToString();
        var dica = modelo?.DicaDePreco;
        var qrCode = GerarQrCode(urlCompartilhavel);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(36);
                pagina.DefaultTextStyle(estilo => estilo
                    .FontFamily("Lato")
                    .FontSize(9.5f)
                    .FontColor(Tinta));

                if (comMarcaDagua)
                {
                    pagina.Background()
                        .AlignCenter()
                        .AlignMiddle()
                        .Rotate(-30)
                        .Text("precifica")
                        .FontFamily("Lato")
                        .FontSize(110)
                        .Bold()
                        .FontColor(Color.FromHex("#F2E7DA"));
                }

                pagina.Header().Element(container => Cabecalho(
                    container,
                    calculo,
                    nomeDoNicho,
                    comMarcaDagua));

                pagina.Content().PaddingVertical(14).Column(coluna =>
                {
                    coluna.Item().Text(calculo.Titulo)
                        .FontSize(23)
                        .Bold()
                        .FontColor(Tinta);

                    coluna.Item().PaddingTop(3).Text(
                            $"{calculo.QuantidadeProduzida} {(calculo.QuantidadeProduzida == 1 ? "unidade" : "unidades")} · " +
                            $"{FormatarHoras(calculo.MinutosDeTrabalho)} de trabalho · {nomeDoNicho}")
                        .FontSize(9)
                        .FontColor(Suave);

                    coluna.Item().PaddingTop(14).Background(Creme).CornerRadius(10).Padding(18).Row(linha =>
                    {
                        linha.RelativeItem().Column(preco =>
                        {
                            preco.Item().Text("PREÇO SUGERIDO")
                                .FontSize(8)
                                .Bold()
                                .FontColor(Suave);
                            preco.Item().PaddingTop(2).Text(Reais(resultado.PrecoSugerido))
                                .FontSize(34)
                                .Bold()
                                .FontColor(Terracota);

                            if (calculo.QuantidadeProduzida > 1)
                            {
                                preco.Item().Text($"{Reais(resultado.PrecoSugeridoPorUnidade)} por unidade")
                                    .FontSize(9)
                                    .FontColor(Suave);
                            }
                        });

                        linha.ConstantItem(170).AlignRight().Column(detalhes =>
                        {
                            detalhes.Item().Text("LUCRO LÍQUIDO")
                                .FontSize(8)
                                .Bold()
                                .FontColor(Suave);
                            detalhes.Item().Text(Reais(resultado.LucroLiquido))
                                .FontSize(16)
                                .Bold()
                                .FontColor(Verde);
                            detalhes.Item().PaddingTop(5).Text($"Margem real: {Percentual(resultado.MargemEfetiva)}")
                                .FontSize(8.5f)
                                .FontColor(Suave);
                        });
                    });

                    coluna.Item().PaddingTop(18).Text("Como o preço foi formado")
                        .FontSize(13)
                        .Bold();

                    coluna.Item().PaddingTop(6).Element(container => TabelaCustos(container, resultado));
                    coluna.Item().PaddingTop(14).Row(linha =>
                    {
                        linha.RelativeItem().PaddingRight(6).Border(1).BorderColor(Linha).CornerRadius(8)
                            .Padding(11).Column(caixa =>
                            {
                                caixa.Item().Text("PREÇO MÍNIMO")
                                    .FontSize(8)
                                    .Bold()
                                    .FontColor(Suave);
                                caixa.Item().PaddingTop(2).Text(Reais(resultado.PrecoMinimo))
                                    .FontSize(17)
                                    .Bold()
                                    .FontColor(Tinta);
                                caixa.Item().Text("Abaixo disso, você paga para trabalhar.")
                                    .FontSize(7.5f)
                                    .FontColor(Suave);
                            });

                        linha.RelativeItem().PaddingLeft(6).Border(1).BorderColor(Linha).CornerRadius(8)
                            .Padding(11).Column(caixa =>
                            {
                                caixa.Item().Text("CUSTO TOTAL")
                                    .FontSize(8)
                                    .Bold()
                                    .FontColor(Suave);
                                caixa.Item().PaddingTop(2).Text(Reais(resultado.CustoTotal))
                                    .FontSize(17)
                                    .Bold()
                                    .FontColor(Tinta);
                                caixa.Item().Text(
                                        resultado.QuantidadeProduzida > 1
                                            ? $"{Reais(resultado.CustoTotalPorUnidade)} por unidade"
                                            : "Materiais + mão de obra + fixos + extras")
                                    .FontSize(7.5f)
                                    .FontColor(Suave);
                            });
                    });

                    if (calculo.Materiais.Count > 0)
                    {
                        coluna.Item().PaddingTop(16).Text("Materiais considerados")
                            .FontSize(13)
                            .Bold();

                        foreach (var material in calculo.Materiais)
                        {
                            coluna.Item().PaddingTop(4).Row(linha =>
                            {
                                linha.RelativeItem().Text(
                                        $"{material.Descricao} — {Quantidade(material.Quantidade)} {material.Unidade}")
                                    .FontSize(8.5f)
                                    .FontColor(Suave);
                                linha.ConstantItem(80).AlignRight().Text(Reais(material.CustoTotal))
                                    .FontSize(8.5f)
                                    .FontColor(Tinta);
                            });
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(dica))
                    {
                        coluna.Item().PaddingTop(16).Background(Areia).CornerRadius(8).Padding(12).Column(caixa =>
                        {
                            caixa.Item().Text($"Dica de {nomeDoNicho}")
                                .FontSize(9)
                                .Bold()
                                .FontColor(Terracota);
                            caixa.Item().PaddingTop(2).Text(dica)
                                .FontSize(8.5f)
                                .FontColor(Suave);
                        });
                    }
                });

                pagina.Footer().Element(container => Rodape(
                    container,
                    urlCompartilhavel,
                    qrCode,
                    comMarcaDagua));
            });
        }).GeneratePdf();
    }

    private static void Cabecalho(
        IContainer container,
        Calculo calculo,
        string nomeDoNicho,
        bool comMarcaDagua)
    {
        container.Column(cabecalho =>
        {
            cabecalho.Item().Row(linha =>
            {
                linha.RelativeItem().Column(coluna =>
                {
                    coluna.Item().Text("precifica")
                        .FontSize(20)
                        .Bold()
                        .FontColor(Terracota);
                    coluna.Item().Text("precificação que respeita seu trabalho")
                        .FontSize(7.5f)
                        .FontColor(Suave);
                });

                linha.ConstantItem(190).AlignRight().Column(coluna =>
                {
                    coluna.Item().Text(nomeDoNicho)
                        .FontSize(9)
                        .Bold()
                        .FontColor(Tinta);
                    coluna.Item().Text(calculo.CriadoEm.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy"))
                        .FontSize(8)
                        .FontColor(Suave);
                    coluna.Item().PaddingTop(2).Text(comMarcaDagua ? "Versão gratuita" : "PDF sem marca d'água")
                        .FontSize(7)
                        .FontColor(Terracota);
                });
            });

            cabecalho.Item().PaddingTop(8).LineHorizontal(1).LineColor(Linha);
        });
    }

    private static void TabelaCustos(IContainer container, ResultadoDaPrecificacao resultado)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn();
                colunas.ConstantColumn(105);
            });

            tabela.Header(cabecalho =>
            {
                cabecalho.Cell().Element(EstiloCabecalho).Text("ITEM");
                cabecalho.Cell().Element(EstiloCabecalho).AlignRight().Text("VALOR");
            });

            AdicionarLinhaDaTabela(tabela, "Materiais (com perda)", Reais(resultado.CustoDeMateriais));
            AdicionarLinhaDaTabela(tabela, "Mão de obra", Reais(resultado.CustoDeMaoDeObra));
            AdicionarLinhaDaTabela(tabela, "Custos fixos rateados", Reais(resultado.CustoFixoRateado));
            AdicionarLinhaDaTabela(tabela, "Custos extras", Reais(resultado.CustosExtras));
            AdicionarLinhaDaTabela(tabela, "Custo total", Reais(resultado.CustoTotal), destaque: true);
            AdicionarLinhaDaTabela(tabela, "Impostos", Reais(resultado.Impostos));
            AdicionarLinhaDaTabela(tabela, "Taxa do cartão", Reais(resultado.TaxaDoCartao));
            AdicionarLinhaDaTabela(tabela, "Taxa fixa por venda", Reais(resultado.TaxaFixa));
            AdicionarLinhaDaTabela(tabela, "Lucro líquido", Reais(resultado.LucroLiquido), destaque: true, cor: Verde);
        });
    }

    private static void AdicionarLinhaDaTabela(
        TableDescriptor tabela,
        string rotulo,
        string valor,
        bool destaque = false,
        Color? cor = null)
    {
        tabela.Cell().Element(EstiloCelula).Text(texto =>
        {
            if (destaque)
            {
                texto.Span(rotulo).FontSize(9.5f).Bold();
            }
            else
            {
                texto.Span(rotulo).FontSize(9);
            }
        });

        tabela.Cell().Element(EstiloCelula).AlignRight().Text(texto =>
        {
            if (destaque)
            {
                texto.Span(valor).FontSize(9.5f).Bold().FontColor(cor ?? Tinta);
            }
            else
            {
                texto.Span(valor).FontSize(9).FontColor(cor ?? Tinta);
            }
        });
    }

    private static IContainer EstiloCabecalho(IContainer container) =>
        container.PaddingBottom(5).BorderBottom(1).BorderColor(Linha);

    private static IContainer EstiloCelula(IContainer container) =>
        container.PaddingVertical(5).BorderBottom(1).BorderColor(Color.FromHex("#F1EAE1"));

    private static void Rodape(
        IContainer container,
        string urlCompartilhavel,
        byte[]? qrCode,
        bool comMarcaDagua)
    {
        container.PaddingTop(8).Column(coluna =>
        {
            coluna.Item().LineHorizontal(1).LineColor(Linha);
            coluna.Item().PaddingTop(7).Row(linha =>
            {
                linha.RelativeItem().Column(texto =>
                {
                    texto.Item().Text($"Compartilhe este cálculo: {urlCompartilhavel}")
                        .FontSize(7.5f)
                        .FontColor(Suave);
                    texto.Item().PaddingTop(2).Text(
                            comMarcaDagua
                                ? "Feito gratuitamente no Precifica. Remova a marca d'água por R$ 29."
                                : "Precifica · cálculo completo, pronto para compartilhar.")
                        .FontSize(7)
                        .FontColor(comMarcaDagua ? Terracota : Suave);
                });

                if (qrCode is not null)
                {
                    linha.ConstantItem(42).Image(qrCode).FitArea();
                }
            });
        });
    }

    private static byte[]? GerarQrCode(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(dados).GetGraphic(8);
    }

    private static void GarantirFontes()
    {
        lock (TravaFontes)
        {
            if (_fontesRegistradas)
            {
                return;
            }

            using var regular = new MemoryStream(LerFonte("Lato-Regular.ttf"));
            using var negrito = new MemoryStream(LerFonte("Lato-Bold.ttf"));
            FontManager.RegisterFontFromStream(regular);
            FontManager.RegisterFontFromStream(negrito);
            _fontesRegistradas = true;
        }
    }

    private static byte[] LerFonte(string nomeArquivo)
    {
        var assembly = typeof(QuestPdfGerador).Assembly;
        var nomeRecurso = assembly.GetManifestResourceNames()
            .FirstOrDefault(nome => nome.EndsWith(nomeArquivo, StringComparison.OrdinalIgnoreCase));

        if (nomeRecurso is null)
        {
            throw new InvalidOperationException($"Fonte {nomeArquivo} não encontrada nos recursos embarcados.");
        }

        using var recurso = assembly.GetManifestResourceStream(nomeRecurso)
                           ?? throw new InvalidOperationException($"Não foi possível ler a fonte {nomeArquivo}.");
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static string Reais(Dinheiro valor)
    {
        var sinal = valor.Centavos < 0 ? "-" : string.Empty;
        var absoluto = Math.Abs(valor.Centavos);
        var inteiro = (absoluto / 100).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
        return $"R$ {sinal}{inteiro},{absoluto % 100:00}";
    }

    private static string Percentual(Percentual valor) =>
        $"{valor.Valor.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',')}%";

    private static string Quantidade(decimal valor) =>
        valor.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string FormatarHoras(int minutos)
    {
        var horas = minutos / 60;
        var restantes = minutos % 60;

        if (horas == 0)
        {
            return $"{restantes} min";
        }

        return restantes == 0 ? $"{horas} h" : $"{horas} h {restantes} min";
    }
}
