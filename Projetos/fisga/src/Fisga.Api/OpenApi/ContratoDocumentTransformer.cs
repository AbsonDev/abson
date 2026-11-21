using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Fisga.Api.OpenApi;

/// <summary>
/// Declara os esquemas de segurança usados pelo contrato. Cada operação aponta
/// para um deles em <c>security</c>; aqui ficam apenas as definições, sem
/// duplicar a regra por operação, que vive em
/// <see cref="ContratoOperationTransformer"/>.
/// </summary>
public sealed class ContratoDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info ??= new OpenApiInfo();
        document.Info.Title = "API do Fisga";
        document.Info.Description = DescricaoDoDocumento;
        document.Info.Contact = new OpenApiContact
        {
            Name = "Fisga",
            Url = new Uri("https://fisga.abson.tech")
        };
        document.Info.License = new OpenApiLicense { Name = "Proprietária" };

        // Fixo de propósito: o artefato é gerado em Development e comparado byte
        // a byte no CI. Escolher por ambiente faria o JSON mudar entre máquinas
        // e o portal publicar um endereço que não existe.
        document.Servers =
        [
            new OpenApiServer
            {
                Url = "https://api.fisga.abson.tech",
                Description = "Produção"
            },
            new OpenApiServer
            {
                Url = "http://localhost:5080",
                Description = "Local"
            }
        ];

        // document.Tags é ISet: a ordem vem do gerador e não é contrato.
        foreach (var tag in document.Tags ?? new HashSet<OpenApiTag>())
        {
            // Name é anulável na Microsoft.OpenApi 2.13; tag sem nome também
            // não tem descrição possível.
            if (tag.Name is null
                || !Tags.Descricoes.TryGetValue(tag.Name, out var descricao))
            {
                throw new InvalidOperationException(
                    $"Tag sem descrição em Tags.Descricoes: {tag.Name}");
            }

            tag.Description = descricao;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        // Este transformer é o dono dos dois ids em components.securitySchemes;
        // as operações só apontam para eles via Esquemas.
        document.Components.SecuritySchemes[Esquemas.CookieAuth] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "fisga_auth",
            Description = "Sessão do Fisga no cookie `fisga_auth` (`HttpOnly`, `SameSite=Lax`; `Secure` conforme configuração; validade deslizante de 30 dias)."
        };

        document.Components.SecuritySchemes[Esquemas.AdminToken] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Admin-Token",
            Description = "Token administrativo no header `X-Admin-Token`."
        };

        return Task.CompletedTask;
    }

    private const string DescricaoDoDocumento = """
        API do Fisga: o diário de pesca que registra capturas com identidade própria.

        As operações são de leitura pública ou exigem sessão; `security` em cada
        uma diz qual é o caso. A sessão é o cookie `fisga_auth`, com validade
        deslizante de 30 dias.

        **Erros.** Respostas de erro com corpo usam `{codigo, detalhe}`; exceção
        não tratada vira `500 erro_inesperado`. O `429` não tem corpo e traz o
        header `Retry-After` em segundos.

        **Consentimento.** Usuário autenticado com Termos ou Privacidade
        pendentes recebe `403` em qualquer rota, exceto `GET /api/auth/me`,
        `POST /api/auth/sair` e `POST /api/conta/consentimento`:

        ```json
        {"codigo":"consentimento_pendente","detalhe":"Aceite os termos e a privacidade vigentes para continuar."}
        ```

        **Origem.** Mutação (`POST`, `PUT`, `PATCH`, `DELETE`) com `Origin` de
        outro site recebe `403 origem_invalida`:

        ```json
        {"codigo":"origem_invalida","detalhe":"Origem da requisição não permitida."}
        ```

        As duas rotas administrativas respondem `404` sem corpo a token inválido,
        de propósito: não revelam que o token existe.
        """;
}
