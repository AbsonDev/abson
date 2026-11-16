using System.Globalization;
using CardapioNoLucro.Application.Abstractions;
using CardapioNoLucro.Application.Catalogo;
using CardapioNoLucro.Domain.Common;
using CardapioNoLucro.Domain.Fichas;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CardapioNoLucro.Infrastructure.Pdf;

public sealed class QuestPdfGerador(ICatalogoDeCanais catalogo) : IGeradorDePdf
{
    private static readonly Color Tinta = Color.FromHex("#20241F");
    private static readonly Color Suave = Color.FromHex("#6C7168");
    private static readonly Color Verde = Color.FromHex("#2F5D46");
    private static readonly Color Açafrao = Color.FromHex("#C77A24");
    private static readonly Color Creme = Color.FromHex("#FBF5EA");
    private static readonly Color Areia = Color.FromHex("#F3EBDD");
    private static readonly Color Linha = Color.FromHex("#E8DFD2");
    private static readonly Lock TravaFontes = new();
    private static bool _fontesRegistradas;

    static QuestPdfGerador()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Gerar(FichaTecnica ficha, ResultadoDaFicha resultado, string urlCompartilhavel)
    {
        GarantirFontes();

        var qrCode = GerarQrCode(urlCompartilhavel);
        var canalReferencia = resultado.Canais.FirstOrDefault();

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(36);
                pagina.DefaultTextStyle(estilo => estilo
                    .FontFamily("Lato")
                    .FontSize(9.2f)
                    .FontColor(Tinta));

                pagina.Header().Element(container => Cabecalho(container, ficha));
                pagina.Content().PaddingVertical(14).Column(coluna =>
                {
                    coluna.Item().Text(ficha.NomeDoPrato)
                        .FontSize(24)
                        .Bold()
                        .FontColor(Tinta);

                    coluna.Item().PaddingTop(3).Text(
                            $"{ficha.Categoria ?? "Ficha técnica"} · {ficha.RendimentoEmPorcoes} porções por receita · " +
                            $"{FormatarHoras(ficha.MinutosDeProducao)} de produção")
                        .FontSize(8.7f)
                        .FontColor(Suave);

                    coluna.Item().PaddingTop(14).Row(linha =>
                    {
                        CartaoResumo(
                            linha,
                            "CUSTO POR PORÇÃO",
                            Reais(resultado.CustoTotalPorcao),
                            $"{ficha.RendimentoEmPorcoes} porções na receita",
                            1);

                        CartaoResumo(
                            linha,
                            canalReferencia is null ? "PREÇO SUGERIDO" : $"PREÇO SUGERIDO · {catalogo.NomeDe(canalReferencia.Tipo).ToUpperInvariant()}",
                            canalReferencia is null ? "—" : Reais(canalReferencia.PrecoSugerido),
                            canalReferencia is null ? "sem canal ativo" : $"CMV real {Percentual(canalReferencia.CmvRealizado)}",
                            1,
                            Açafrao);
                    });

                    coluna.Item().PaddingTop(18).Text("Preço por canal de venda")
                        .FontSize(13)
                        .Bold();

                    coluna.Item().PaddingTop(6).Element(container => TabelaCanais(container, resultado));

                    var canaisAjustados = resultado.Canais
                        .Where(canal => canal.AjustadoPeloCustoTotal)
                        .Select(canal => catalogo.NomeDe(canal.Tipo))
                        .ToList();

                    if (canaisAjustados.Count > 0)
                    {
                        coluna.Item().PaddingTop(8).Background("#FFF4E2").CornerRadius(6).Padding(9).Text(
                                $"Atenção: em {string.Join(", ", canaisAjustados)} o preço pelo CMV alvo não cobria " +
                                "todos os custos. O preço foi elevado ao mínimo para não vender no prejuízo.")
                            .FontSize(7.8f)
                            .FontColor(Color.FromHex("#7A5219"));
                    }

                    coluna.Item().PaddingTop(18).Text("Como o custo foi formado")
                        .FontSize(13)
                        .Bold();

                    coluna.Item().PaddingTop(6).Element(container => TabelaCusto(container, resultado));

                    coluna.Item().PaddingTop(16).Text("Ingredientes da receita")
                        .FontSize(13)
                        .Bold();

                    coluna.Item().PaddingTop(5).Element(container => TabelaIngredientes(container, ficha));

                    if (ficha.Embalagens.Count > 0)
                    {
                        coluna.Item().PaddingTop(10).Text("Embalagens e descartáveis por porção")
                            .FontSize(10)
                            .Bold()
                            .FontColor(Verde);

                        foreach (var embalagem in ficha.Embalagens.OrderBy(item => item.Descricao, StringComparer.CurrentCultureIgnoreCase))
                        {
                            coluna.Item().PaddingTop(3).Row(linha =>
                            {
                                linha.RelativeItem().Text(embalagem.Descricao).FontSize(8.3f).FontColor(Suave);
                                linha.ConstantItem(90).AlignRight().Text(Reais(embalagem.CustoPorPorcao))
                                    .FontSize(8.3f);
                            });
                        }
                    }

                    coluna.Item().PaddingTop(14).Background(Areia).CornerRadius(7).Padding(11).Column(caixa =>
                    {
                        caixa.Item().Text("Leitura rápida")
                            .FontSize(9)
                            .Bold()
                            .FontColor(Verde);
                        caixa.Item().PaddingTop(2).Text(
                                "O CMV é calculado só com o custo dos ingredientes dividido pelo preço. " +
                                "O preço mínimo inclui embalagem, mão de obra, custo fixo, impostos e taxas do canal. " +
                                "O preço sugerido é o maior entre os dois.")
                            .FontSize(7.9f)
                            .FontColor(Suave);
                    });
                });

                pagina.Footer().Element(container => Rodape(container, urlCompartilhavel, qrCode, ficha));
            });
        }).GeneratePdf();
    }

    private static void Cabecalho(IContainer container, FichaTecnica ficha)
    {
        container.Column(cabecalho =>
        {
            cabecalho.Item().Row(linha =>
            {
                linha.RelativeItem().Column(coluna =>
                {
                    coluna.Item().Text("cardápio no lucro")
                        .FontSize(19)
                        .Bold()
                        .FontColor(Verde);
                    coluna.Item().Text("ficha técnica que mostra o lucro de cada canal")
                        .FontSize(7.3f)
                        .FontColor(Suave);
                });

                linha.ConstantItem(210).AlignRight().Column(coluna =>
                {
                    coluna.Item().Text(ficha.Estabelecimento ?? "Seu estabelecimento")
                        .FontSize(9)
                        .Bold();
                    coluna.Item().Text(ficha.CriadoEm.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy"))
                        .FontSize(8)
                        .FontColor(Suave);
                    coluna.Item().PaddingTop(2).Text("Ficha técnica · versão gratuita")
                        .FontSize(7)
                        .FontColor(Açafrao);
                });
            });

            cabecalho.Item().PaddingTop(8).LineHorizontal(1).LineColor(Linha);
        });
    }

    private static void CartaoResumo(
        RowDescriptor linha,
        string rotulo,
        string valor,
        string detalhe,
        int peso,
        Color? cor = null)
    {
        linha.RelativeItem(peso).PaddingRight(6).Background(Creme).CornerRadius(8).Padding(12).Column(caixa =>
        {
            caixa.Item().Text(rotulo).FontSize(7).Bold().FontColor(Suave);
            caixa.Item().PaddingTop(3).Text(valor).FontSize(21).Bold().FontColor(cor ?? Verde);
            caixa.Item().Text(detalhe).FontSize(7.2f).FontColor(Suave);
        });
    }

    private void TabelaCanais(IContainer container, ResultadoDaFicha resultado)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn(1.5f);
                colunas.RelativeColumn(0.8f);
                colunas.RelativeColumn(1f);
                colunas.RelativeColumn(0.9f);
                colunas.RelativeColumn(0.8f);
                colunas.RelativeColumn(0.9f);
            });

            tabela.Header(cabecalho =>
            {
                CabecalhoTabela(cabecalho, "CANAL");
                CabecalhoTabela(cabecalho, "COMISSÃO");
                CabecalhoTabela(cabecalho, "PREÇO SUGERIDO");
                CabecalhoTabela(cabecalho, "MÍNIMO");
                CabecalhoTabela(cabecalho, "CMV REAL");
                CabecalhoTabela(cabecalho, "MARGEM CONTRIB.");
            });

            foreach (var canal in resultado.Canais)
            {
                tabela.Cell().Element(EstiloCelula).Text(texto =>
                    texto.Span(catalogo.NomeDe(canal.Tipo)).FontSize(8.5f).Bold());
                tabela.Cell().Element(EstiloCelula).Text(Percentual(canal.Comissao)).FontSize(8.2f);
                tabela.Cell().Element(EstiloCelula).Text(texto =>
                    texto.Span(Reais(canal.PrecoSugerido)).FontSize(8.7f).Bold().FontColor(Verde));
                tabela.Cell().Element(EstiloCelula).Text(Reais(canal.PrecoMinimo)).FontSize(8.2f);
                tabela.Cell().Element(EstiloCelula).Text(Percentual(canal.CmvRealizado)).FontSize(8.2f);
                tabela.Cell().Element(EstiloCelula).Text(Reais(canal.MargemContribuicao)).FontSize(8.2f);
            }
        });
    }

    private static void TabelaCusto(IContainer container, ResultadoDaFicha resultado)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn();
                colunas.ConstantColumn(115);
            });

            tabela.Header(cabecalho =>
            {
                CabecalhoTabela(cabecalho, "ITEM");
                CabecalhoTabela(cabecalho, "POR PORÇÃO");
            });

            LinhaCusto(tabela, $"Ingredientes ({Reais(resultado.CustoIngredientesLote)} na receita)", Reais(resultado.CustoIngredientesPorcao));
            LinhaCusto(tabela, "Embalagens e descartáveis", Reais(resultado.CustoEmbalagemPorcao));
            LinhaCusto(tabela, "Mão de obra", Reais(resultado.CustoMaoDeObraPorcao));
            LinhaCusto(tabela, "Custo fixo rateado", Reais(resultado.CustoFixoPorcao));
            LinhaCusto(tabela, "Custo total", Reais(resultado.CustoTotalPorcao), destaque: true);
        });
    }

    private static void TabelaIngredientes(IContainer container, FichaTecnica ficha)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn(2.3f);
                colunas.RelativeColumn(0.9f);
                colunas.RelativeColumn(0.9f);
                colunas.RelativeColumn(0.9f);
                colunas.RelativeColumn(1f);
            });

            tabela.Header(cabecalho =>
            {
                CabecalhoTabela(cabecalho, "INGREDIENTE");
                CabecalhoTabela(cabecalho, "QUANTIDADE");
                CabecalhoTabela(cabecalho, "PREÇO UNIT.");
                CabecalhoTabela(cabecalho, "RENDIMENTO");
                CabecalhoTabela(cabecalho, "CUSTO REAL");
            });

            foreach (var ingrediente in ficha.Ingredientes.OrderBy(item => item.Descricao, StringComparer.CurrentCultureIgnoreCase))
            {
                tabela.Cell().Element(EstiloCelula).Text(ingrediente.Descricao).FontSize(8.3f);
                tabela.Cell().Element(EstiloCelula).Text($"{Quantidade(ingrediente.Quantidade)} {ingrediente.Unidade}").FontSize(8.1f);
                tabela.Cell().Element(EstiloCelula).Text(Reais(ingrediente.PrecoUnitario)).FontSize(8.1f);
                tabela.Cell().Element(EstiloCelula).Text(Percentual(ingrediente.Rendimento)).FontSize(8.1f);
                tabela.Cell().Element(EstiloCelula).Text(Reais(ingrediente.CustoComRendimento)).FontSize(8.1f);
            }
        });
    }

    private static void CabecalhoTabela(TableCellDescriptor cabecalho, string rotulo) =>
        cabecalho.Cell().Element(EstiloCabecalho).Text(rotulo).FontSize(6.6f).Bold().FontColor(Suave);

    private static void LinhaCusto(
        TableDescriptor tabela,
        string rotulo,
        string valor,
        bool destaque = false)
    {
        tabela.Cell().Element(EstiloCelula).Text(texto =>
        {
            if (destaque)
            {
                texto.Span(rotulo).FontSize(9).Bold();
            }
            else
            {
                texto.Span(rotulo).FontSize(8.4f);
            }
        });

        tabela.Cell().Element(EstiloCelula).AlignRight().Text(texto =>
        {
            if (destaque)
            {
                texto.Span(valor).FontSize(9).Bold().FontColor(Verde);
            }
            else
            {
                texto.Span(valor).FontSize(8.4f);
            }
        });
    }

    private static IContainer EstiloCabecalho(IContainer container) =>
        container.PaddingBottom(5).BorderBottom(1).BorderColor(Linha);

    private static IContainer EstiloCelula(IContainer container) =>
        container.PaddingVertical(5).BorderBottom(1).BorderColor(Color.FromHex("#F1E8DB"));

    private static void Rodape(
        IContainer container,
        string urlCompartilhavel,
        byte[]? qrCode,
        FichaTecnica ficha)
    {
        container.PaddingTop(8).Column(coluna =>
        {
            coluna.Item().LineHorizontal(1).LineColor(Linha);
            coluna.Item().PaddingTop(7).Row(linha =>
            {
                linha.RelativeItem().Column(texto =>
                {
                    texto.Item().Text($"Compartilhe esta ficha: {urlCompartilhavel}")
                        .FontSize(7.3f)
                        .FontColor(Suave);
                    texto.Item().PaddingTop(2).Text(
                            $"Cardápio no Lucro · {ficha.NomeDoPrato} · gerado gratuitamente")
                        .FontSize(6.9f)
                        .FontColor(Açafrao);
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
            throw new InvalidOperationException($"Fonte {nomeArquivo} não encontrada.");
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
