using System.Globalization;
using Fisga.Application.Pescarias;
using Fisga.Domain.Pescarias;
using Sentry;

namespace Fisga.Api.Observabilidade;

/// <summary>
/// Métricas de negócio com tags de baixa cardinalidade. Nunca envie e-mail, nome,
/// coordenadas, espécie livre ou identificadores de usuário como atributos.
/// </summary>
public static class Metricas
{
    public static void PescariaCriada(AmbienteDePesca ambiente, IReadOnlyList<EntradaDeCaptura>? capturas)
    {
        var total = capturas?.Count ?? 0;
        SentrySdk.Metrics.EmitCounter(
            "fisga.pescaria.criada",
            1,
            Atributos(("ambiente", ambiente.ToString()), ("com_capturas", total > 0)));

        if (total == 0)
        {
            return;
        }

        SentrySdk.Metrics.EmitCounter(
            "fisga.captura.registrada",
            total,
            Atributos(("ambiente", ambiente.ToString())));

        var soltas = capturas!.Count(captura => captura.Solto);
        if (soltas > 0)
        {
            SentrySdk.Metrics.EmitCounter(
                "fisga.captura.solta",
                soltas,
                Atributos(("ambiente", ambiente.ToString())));
        }
    }

    public static void PescariaAtualizada(AmbienteDePesca ambiente) =>
        SentrySdk.Metrics.EmitCounter(
            "fisga.pescaria.atualizada",
            1,
            Atributos(("ambiente", ambiente.ToString())));

    public static void LocalSalvo(AmbienteDePesca ambiente) =>
        SentrySdk.Metrics.EmitCounter(
            "fisga.local.salvo",
            1,
            Atributos(("ambiente", ambiente.ToString())));

    public static void CondicoesConsultadas(
        AmbienteDePesca ambiente,
        string tipo,
        double duracaoMs) =>
        SentrySdk.Metrics.EmitDistribution(
            "fisga.condicoes.duracao",
            duracaoMs,
            MeasurementUnit.Duration.Millisecond,
            Atributos(("ambiente", ambiente.ToString()), ("tipo", tipo)));

    public static void LoginSolicitado() =>
        SentrySdk.Metrics.EmitCounter("fisga.login.solicitado", 1, []);

    public static void LoginConfirmado(bool primeiroAcesso) =>
        SentrySdk.Metrics.EmitCounter(
            "fisga.login.confirmado",
            1,
            Atributos(("primeiro_acesso", primeiroAcesso)));

    public static void LoginFalhou(string motivo) =>
        SentrySdk.Metrics.EmitCounter(
            "fisga.login.falhou",
            1,
            Atributos(("motivo", motivo)));

    public static void ResetSolicitado() =>
        SentrySdk.Metrics.EmitCounter("fisga.reset.solicitado", 1, []);

    public static void ResetConcluido() =>
        SentrySdk.Metrics.EmitCounter("fisga.reset.concluido", 1, []);

    public static void MidiaEnviada(string tipo, long bytes)
    {
        SentrySdk.Metrics.EmitCounter(
            "fisga.midia.enviada",
            1,
            Atributos(("tipo", tipo)));

        SentrySdk.Metrics.EmitDistribution(
            "fisga.midia.tamanho",
            bytes,
            MeasurementUnit.Information.Byte,
            Atributos(("tipo", tipo)));
    }

    public static void RequisicaoApi(string rota, string metodo, int status, double duracaoMs) =>
        SentrySdk.Metrics.EmitDistribution(
            "fisga.api.request.duration",
            duracaoMs,
            MeasurementUnit.Duration.Millisecond,
            Atributos(("rota", rota), ("metodo", metodo), ("status", status.ToString(CultureInfo.InvariantCulture))));

    private static KeyValuePair<string, object>[] Atributos(params (string Chave, object Valor)[] itens) =>
        itens.Select(item => new KeyValuePair<string, object>(item.Chave, item.Valor)).ToArray();
}
