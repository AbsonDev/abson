using System.Globalization;
using Fisga.Application.Abstractions;
using Fisga.Application.Catalogo;
using Fisga.Domain.Pescarias;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fisga.Infrastructure.Pdf;

public sealed class QuestPdfGerador(ICatalogoDePesca catalogo) : IGeradorDePdf
{
    private static readonly Color Tinta = Color.FromHex("#172A2D");
    private static readonly Color Suave = Color.FromHex("#657277");
    private static readonly Color Petroleo = Color.FromHex("#174E57");
    private static readonly Color Laranja = Color.FromHex("#D9773F");
    private static readonly Color Areia = Color.FromHex("#F8F2E7");
    private static readonly Color Creme = Color.FromHex("#FFFBF4");
    private static readonly Color Linha = Color.FromHex("#E7DDCF");
    private static readonly Color Verde = Color.FromHex("#56745C");
    private static readonly Lock TravaFontes = new();
    private static bool _fontesRegistradas;

    static QuestPdfGerador()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Gerar(Pescaria pescaria, string urlCompartilhavel)
    {
        GarantirFontes();

        var qrCode = GerarQrCode(urlCompartilhavel);
        var maiorCaptura = pescaria.Capturas
            .OrderByDescending(captura => captura.PesoKg)
            .ThenByDescending(captura => captura.ComprimentoCm)
            .FirstOrDefault();

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

                pagina.Header().Element(container => Cabecalho(container, pescaria));
                pagina.Content().PaddingVertical(14).Column(coluna =>
                {
                    coluna.Item().Text(pescaria.Local)
                        .FontSize(24)
                        .Bold()
                        .FontColor(Tinta);

                    var detalhe = string.Join(" · ", new[]
                    {
                        catalogo.NomeDe(pescaria.Ambiente),
                        catalogo.NomeDe(pescaria.Periodo),
                        Data(pescaria.Data),
                        Cidade(pescaria)
                    }.Where(valor => !string.IsNullOrWhiteSpace(valor)));

                    coluna.Item().PaddingTop(3).Text(detalhe)
                        .FontSize(8.8f)
                        .FontColor(Suave);

                    coluna.Item().PaddingTop(14).Row(linha =>
                    {
                        CartaoResumo(linha, "CAPTURAS", pescaria.Capturas.Count.ToString(), "nesta pescaria", 1);
                        CartaoResumo(
                            linha,
                            "MAIOR PEIXE",
                            maiorCaptura is null
                                ? "—"
                                : maiorCaptura.PesoKg > 0
                                    ? $"{Quantidade(maiorCaptura.PesoKg)} kg"
                                    : $"{Quantidade(maiorCaptura.ComprimentoCm)} cm",
                            maiorCaptura is null ? "sem capturas" : maiorCaptura.Especie,
                            1,
                            Laranja);
                        CartaoResumo(
                            linha,
                            "SOLTOS",
                            pescaria.Capturas.Count(captura => captura.Solto).ToString(),
                            "devolvidos à água",
                            1,
                            Verde);
                    });

                    coluna.Item().PaddingTop(13).Row(linha =>
                    {
                        Condicao(linha, "LUA", catalogo.NomeDe(pescaria.Lua));
                        Condicao(linha, "MARÉ", catalogo.NomeDe(pescaria.Mare));
                        Condicao(linha, "CLIMA", catalogo.NomeDe(pescaria.Clima));
                        Condicao(linha, "ÁGUA", catalogo.NomeDe(pescaria.ClarezaDaAgua));
                    });

                    if (maiorCaptura is not null)
                    {
                        coluna.Item().PaddingTop(15).Background(Areia).CornerRadius(9).Padding(13).Row(linha =>
                        {
                            linha.RelativeItem().Column(destaque =>
                            {
                                destaque.Item().Text("TROFÉU DA PESCARIA")
                                    .FontSize(7)
                                    .Bold()
                                    .FontColor(Laranja);
                                destaque.Item().PaddingTop(3).Text(maiorCaptura.Especie)
                                    .FontSize(18)
                                    .Bold()
                                    .FontColor(Tinta);
                                destaque.Item().Text(
                                        $"{Quantidade(maiorCaptura.PesoKg)} kg · {Quantidade(maiorCaptura.ComprimentoCm)} cm" +
                                        (string.IsNullOrWhiteSpace(maiorCaptura.Isca) ? string.Empty : $" · isca {maiorCaptura.Isca}"))
                                    .FontSize(8.3f)
                                    .FontColor(Suave);
                            });

                            linha.ConstantItem(120).AlignRight().Column(selo =>
                            {
                                selo.Item().Text(maiorCaptura.Solto ? "SOLTO" : "CAPTURADO")
                                    .FontSize(8)
                                    .Bold()
                                    .FontColor(maiorCaptura.Solto ? Verde : Laranja);
                                if (!string.IsNullOrWhiteSpace(maiorCaptura.Observacao))
                                {
                                    selo.Item().PaddingTop(2).Text(maiorCaptura.Observacao)
                                        .FontSize(6.7f)
                                        .FontColor(Suave);
                                }
                            });
                        });
                    }

                    coluna.Item().PaddingTop(17).Text("Capturas do dia")
                        .FontSize(13)
                        .Bold();

                    coluna.Item().PaddingTop(6).Element(container => TabelaCapturas(container, pescaria));

                    if (!string.IsNullOrWhiteSpace(pescaria.Observacoes))
                    {
                        coluna.Item().PaddingTop(14).Background(Creme).Border(1).BorderColor(Linha).CornerRadius(8)
                            .Padding(11).Column(caixa =>
                            {
                                caixa.Item().Text("Anotações da pescaria")
                                    .FontSize(8.5f)
                                    .Bold()
                                    .FontColor(Petroleo);
                                caixa.Item().PaddingTop(2).Text(pescaria.Observacoes)
                                    .FontSize(8)
                                    .FontColor(Suave);
                            });
                    }

                    coluna.Item().PaddingTop(13).Text(
                            "Fisga · cada peixe tem uma história. Este relatório foi gerado gratuitamente.")
                        .FontSize(7.2f)
                        .FontColor(Suave);
                });

                pagina.Footer().Element(container => Rodape(container, urlCompartilhavel, qrCode, pescaria));
            });
        }).GeneratePdf();
    }

    private static void Cabecalho(IContainer container, Pescaria pescaria)
    {
        container.Column(cabecalho =>
        {
            cabecalho.Item().Row(linha =>
            {
                linha.RelativeItem().Column(coluna =>
                {
                    coluna.Item().Text("FISGA")
                        .FontSize(21)
                        .Bold()
                        .FontColor(Petroleo);
                    coluna.Item().Text("diário de pesca")
                        .FontSize(7.4f)
                        .FontColor(Suave);
                });

                linha.ConstantItem(210).AlignRight().Column(coluna =>
                {
                    coluna.Item().Text(pescaria.Pescador ?? "Pescador")
                        .FontSize(9)
                        .Bold();
                    coluna.Item().Text(Data(pescaria.Data))
                        .FontSize(8)
                        .FontColor(Suave);
                    coluna.Item().PaddingTop(2).Text("Relatório gratuito")
                        .FontSize(7)
                        .FontColor(Laranja);
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
        linha.RelativeItem(peso).PaddingRight(6).Background(Areia).CornerRadius(8).Padding(11).Column(caixa =>
        {
            caixa.Item().Text(rotulo).FontSize(7).Bold().FontColor(Suave);
            caixa.Item().PaddingTop(3).Text(valor).FontSize(20).Bold().FontColor(cor ?? Petroleo);
            caixa.Item().Text(detalhe).FontSize(7.1f).FontColor(Suave);
        });
    }

    private static void Condicao(RowDescriptor linha, string rotulo, string valor)
    {
        linha.RelativeItem().PaddingRight(8).Column(caixa =>
        {
            caixa.Item().Text(rotulo).FontSize(6.4f).Bold().FontColor(Suave);
            caixa.Item().PaddingTop(1).Text(valor).FontSize(8.2f).Bold().FontColor(Tinta);
        });
    }

    private void TabelaCapturas(IContainer container, Pescaria pescaria)
    {
        container.Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn(1.8f);
                colunas.RelativeColumn(0.8f);
                colunas.RelativeColumn(0.8f);
                colunas.RelativeColumn(1.3f);
                colunas.RelativeColumn(0.8f);
            });

            tabela.Header(cabecalho =>
            {
                CabecalhoTabela(cabecalho, "ESPÉCIE");
                CabecalhoTabela(cabecalho, "PESO");
                CabecalhoTabela(cabecalho, "COMPR.");
                CabecalhoTabela(cabecalho, "ISCA");
                CabecalhoTabela(cabecalho, "DESTINO");
            });

            if (pescaria.Capturas.Count == 0)
            {
                tabela.Cell().ColumnSpan(5).PaddingVertical(12).Text("Nenhuma captura registrada nesta pescaria.")
                    .FontSize(8.2f)
                    .FontColor(Suave);
            }

            foreach (var captura in pescaria.Capturas.OrderByDescending(captura => captura.PesoKg))
            {
                tabela.Cell().Element(EstiloCelula).Text(captura.Especie).FontSize(8.4f).Bold();
                tabela.Cell().Element(EstiloCelula).Text(captura.PesoKg > 0 ? $"{Quantidade(captura.PesoKg)} kg" : "—").FontSize(8.2f);
                tabela.Cell().Element(EstiloCelula).Text(captura.ComprimentoCm > 0 ? $"{Quantidade(captura.ComprimentoCm)} cm" : "—").FontSize(8.2f);
                tabela.Cell().Element(EstiloCelula).Text(string.IsNullOrWhiteSpace(captura.Isca) ? "—" : captura.Isca).FontSize(8.2f);
                tabela.Cell().Element(EstiloCelula).Text(captura.Solto ? "Solto" : "Capturado").FontSize(8.2f)
                    .FontColor(captura.Solto ? Verde : Laranja);
            }
        });
    }

    private static void CabecalhoTabela(TableCellDescriptor cabecalho, string rotulo) =>
        cabecalho.Cell().Element(EstiloCabecalho).Text(rotulo).FontSize(6.6f).Bold().FontColor(Suave);

    private static IContainer EstiloCabecalho(IContainer container) =>
        container.PaddingBottom(5).BorderBottom(1).BorderColor(Linha);

    private static IContainer EstiloCelula(IContainer container) =>
        container.PaddingVertical(5).BorderBottom(1).BorderColor(Color.FromHex("#F1E8DB"));

    private void Rodape(
        IContainer container,
        string urlCompartilhavel,
        byte[]? qrCode,
        Pescaria pescaria)
    {
        container.PaddingTop(8).Column(coluna =>
        {
            coluna.Item().LineHorizontal(1).LineColor(Linha);
            coluna.Item().PaddingTop(7).Row(linha =>
            {
                linha.RelativeItem().Column(texto =>
                {
                    texto.Item().Text($"Compartilhe esta pescaria: {urlCompartilhavel}")
                        .FontSize(7.3f)
                        .FontColor(Suave);
                    texto.Item().PaddingTop(2).Text(
                            $"FISGA · {catalogo.NomeDe(pescaria.Ambiente)} · {Data(pescaria.Data)} · relatório gratuito")
                        .FontSize(6.9f)
                        .FontColor(Laranja);
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

    private static string Quantidade(decimal valor) =>
        valor.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Cidade(Pescaria pescaria) =>
        string.Join(" · ", new[]
        {
            pescaria.Cidade,
            pescaria.Estado
        }.Where(valor => !string.IsNullOrWhiteSpace(valor)));
}
