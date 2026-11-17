using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fisga.Application.Catalogo;
using Fisga.Application.Vision;
using Microsoft.Extensions.Logging;

namespace Fisga.Infrastructure.Vision;

public sealed class GeminiReconhecedor(
    HttpClient http,
    VisionOptions opcoes,
    ICatalogoDePesca catalogo,
    ILogger<GeminiReconhecedor> logger) : IReconhecedorDeEspecie
{
    public async Task<SugestaoDeEspecieResultado> SugerirAsync(
        Stream imagem,
        string contentType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(opcoes.ApiKey))
        {
            return new SugestaoDeEspecieResultado(
                false,
                "A sugestão automática ainda não está configurada. Confirme a espécie manualmente.",
                []);
        }

        try
        {
            using var memoria = new MemoryStream();
            await imagem.CopyToAsync(memoria, ct);
            var base64 = Convert.ToBase64String(memoria.ToArray());

            var especies = string.Join(", ", catalogo.Especies);
            var prompt = $$"""
                Você é um assistente de pesca esportiva brasileira. Identifique a espécie de peixe na foto.
                Responda SOMENTE com JSON válido no formato:
                {"sugestoes":[{"especie":"Tucunaré","confianca":0.82}]}
                Use no máximo 3 sugestões, da mais provável para a menos provável.
                A confiança deve estar entre 0 e 1.
                Se não conseguir identificar, devolva {"sugestoes":[]}.
                Prefira estas espécies quando fizer sentido: {{especies}}.
                """;

            var corpo = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType,
                                    data = base64
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(opcoes.Model)}:generateContent?key={Uri.EscapeDataString(opcoes.ApiKey)}";
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json")
            };
            requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resposta = await http.SendAsync(requisicao, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                var erro = await resposta.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Gemini recusou a sugestão: {Status} {Erro}", resposta.StatusCode, erro[..Math.Min(erro.Length, 300)]);
                return new SugestaoDeEspecieResultado(
                    true,
                    "Não foi possível analisar a foto agora. Confirme a espécie manualmente.",
                    []);
            }

            await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
            var texto = json.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(texto))
            {
                return new SugestaoDeEspecieResultado(true, "Não conseguimos ler a sugestão.", []);
            }

            return Interpretar(texto);
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(excecao, "Falha ao sugerir espécie");
            return new SugestaoDeEspecieResultado(
                true,
                "Não foi possível analisar a foto agora. Confirme a espécie manualmente.",
                []);
        }
    }

    private SugestaoDeEspecieResultado Interpretar(string texto)
    {
        var limpo = texto.Trim();
        if (limpo.StartsWith("```", StringComparison.Ordinal))
        {
            var inicio = limpo.IndexOf('\n');
            var fim = limpo.LastIndexOf("```", StringComparison.Ordinal);
            if (inicio >= 0 && fim > inicio)
            {
                limpo = limpo[(inicio + 1)..fim].Trim();
            }
        }

        using var json = JsonDocument.Parse(limpo);
        if (!json.RootElement.TryGetProperty("sugestoes", out var sugestoes)
            || sugestoes.ValueKind != JsonValueKind.Array)
        {
            return new SugestaoDeEspecieResultado(true, "Nenhuma espécie identificada.", []);
        }

        var lista = new List<SugestaoDeEspecie>();
        foreach (var item in sugestoes.EnumerateArray())
        {
            if (!item.TryGetProperty("especie", out var especieJson)
                || especieJson.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var nome = especieJson.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(nome))
            {
                continue;
            }

            var canonica = catalogo.Especies
                .FirstOrDefault(conhecida => string.Equals(conhecida, nome, StringComparison.CurrentCultureIgnoreCase));
            if (canonica is null)
            {
                continue;
            }

            var confianca = item.TryGetProperty("confianca", out var valor)
                            && valor.ValueKind == JsonValueKind.Number
                ? Math.Clamp(valor.GetDouble(), 0, 1)
                : 0.5;

            lista.Add(new SugestaoDeEspecie(canonica, Math.Round(confianca, 2)));
        }

        return new SugestaoDeEspecieResultado(
            true,
            lista.Count == 0 ? "Nenhuma espécie identificada com segurança." : "Sugestões para confirmar.",
            lista
                .DistinctBy(item => item.Especie)
                .OrderByDescending(item => item.Confianca)
                .Take(3)
                .ToList());
    }
}
