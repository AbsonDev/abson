namespace Fisga.Application.Condicoes;

public static class EstadosBrasileiros
{
    private static readonly Dictionary<string, string> PorNome = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acre"] = "AC",
        ["Alagoas"] = "AL",
        ["Amapá"] = "AP",
        ["Amazonas"] = "AM",
        ["Bahia"] = "BA",
        ["Ceará"] = "CE",
        ["Distrito Federal"] = "DF",
        ["Espírito Santo"] = "ES",
        ["Goiás"] = "GO",
        ["Maranhão"] = "MA",
        ["Mato Grosso"] = "MT",
        ["Mato Grosso do Sul"] = "MS",
        ["Minas Gerais"] = "MG",
        ["Pará"] = "PA",
        ["Paraíba"] = "PB",
        ["Paraná"] = "PR",
        ["Pernambuco"] = "PE",
        ["Piauí"] = "PI",
        ["Rio de Janeiro"] = "RJ",
        ["Rio Grande do Norte"] = "RN",
        ["Rio Grande do Sul"] = "RS",
        ["Rondônia"] = "RO",
        ["Roraima"] = "RR",
        ["Santa Catarina"] = "SC",
        ["São Paulo"] = "SP",
        ["Sergipe"] = "SE",
        ["Tocantins"] = "TO"
    };

    public static string? ParaUf(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var limpo = valor.Trim();
        if (limpo.Length == 2)
        {
            return limpo.ToUpperInvariant();
        }

        return PorNome.TryGetValue(limpo, out var uf) ? uf : null;
    }
}
