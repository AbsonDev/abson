using System.Globalization;
using Fisga.Application.Abstractions;
using Fisga.Application.Identidade;
using Fisga.Application.Pescarias;
using Fisga.Domain.Identidade;

namespace Fisga.Application.Newsletter;

public sealed class ServicoDeNewsletter(
    IUsuarioRepository usuarios,
    ServicoDoDiario diario,
    IEmailSender email,
    IUnitOfWork unidade,
    IRelogio relogio,
    AutenticacaoOptions auth,
    NewsletterOptions opcoes)
{
    public async Task<ResultadoNewsletter> EnviarAsync(
        string? mes,
        bool simulacao = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Secret))
        {
            throw new InvalidOperationException(
                "Configure Newsletter:Secret para assinar os links de descadastro.");
        }

        var (mesNormalizado, mesNome, dataMes) = ResolverMes(mes);
        var leituras = ConteudoSazonal.ParaMes(dataMes.Month);

        var lista = (await usuarios.ListarAceitaNovidadesAsync(ct))
            .Take(Math.Max(1, opcoes.MaximoPorEnvio))
            .ToList();

        var enviados = 0;
        var falhas = 0;
        var erros = new List<string>();

        foreach (var usuario in lista)
        {
            var resumo = await diario.ObterAsync(usuario.Id, ct);
            var token = AssinaturaDeDescadastro.Gerar(usuario.Id, opcoes.Secret);
            var urlDescadastro =
                $"{auth.LinkBaseUrl.TrimEnd('/')}/api/newsletter/descadastrar?token={Uri.EscapeDataString(token)}";
            var mensagem = ModeloDeNewsletter.Criar(
                usuario,
                resumo,
                mesNome,
                leituras,
                urlDescadastro,
                auth.WebUrl);

            if (simulacao)
            {
                continue;
            }

            try
            {
                await email.EnviarAsync(
                    usuario.Email.Valor,
                    mensagem.Assunto,
                    mensagem.Html,
                    mensagem.Texto,
                    urlDescadastro,
                    ct);

                enviados += 1;
            }
            catch (Exception excecao)
            {
                falhas += 1;

                if (erros.Count < 5)
                {
                    erros.Add($"{usuario.Email.Valor}: {excecao.Message}");
                }
            }
        }

        return new ResultadoNewsletter(
            mesNormalizado,
            lista.Count,
            enviados,
            falhas,
            simulacao,
            erros);
    }

    public async Task<MensagemDeNewsletter?> PrevisualizarAsync(
        string? email,
        string? mes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(opcoes.Secret))
        {
            return null;
        }

        Email emailNormalizado;
        try
        {
            emailNormalizado = Email.Criar(email);
        }
        catch
        {
            return null;
        }

        var usuario = await usuarios.ObterPorEmailAsync(emailNormalizado, ct);
        if (usuario is null)
        {
            return null;
        }

        var (mesNormalizado, mesNome, dataMes) = ResolverMes(mes);
        var leituras = ConteudoSazonal.ParaMes(dataMes.Month);
        var resumo = await diario.ObterAsync(usuario.Id, ct);
        var token = AssinaturaDeDescadastro.Gerar(usuario.Id, opcoes.Secret);
        var urlDescadastro =
            $"{auth.LinkBaseUrl.TrimEnd('/')}/api/newsletter/descadastrar?token={Uri.EscapeDataString(token)}";

        _ = mesNormalizado;
        return ModeloDeNewsletter.Criar(usuario, resumo, mesNome, leituras, urlDescadastro, auth.WebUrl);
    }

    public async Task<bool> DescadastrarAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Secret))
        {
            return false;
        }

        var usuarioId = AssinaturaDeDescadastro.Validar(token, opcoes.Secret);
        if (usuarioId is null)
        {
            return false;
        }

        var usuario = await usuarios.ObterPorIdAsync(usuarioId.Value, ct);
        if (usuario is null)
        {
            return false;
        }

        usuario.DescadastrarNovidades(relogio.Agora);
        await unidade.SalvarAsync(ct);
        return true;
    }

    private (string MesNormalizado, string MesNome, DateTime DataMes) ResolverMes(string? mes)
    {
        var agora = relogio.Agora;
        var mesNormalizado = string.IsNullOrWhiteSpace(mes)
            ? agora.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : mes.Trim();

        if (!DateTime.TryParseExact(
                $"{mesNormalizado}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dataMes))
        {
            dataMes = agora.DateTime;
            mesNormalizado = agora.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        var mesNome = Capitalizar(dataMes.ToString("MMMM", CultureInfo.GetCultureInfo("pt-BR")));
        return (mesNormalizado, mesNome, dataMes);
    }

    private static string Capitalizar(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
