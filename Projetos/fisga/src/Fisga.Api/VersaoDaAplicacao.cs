namespace Fisga.Api;

public static class VersaoDaAplicacao
{
    public static string Resolver(string? gitSha, string? sourceCommit)
    {
        if (!string.IsNullOrWhiteSpace(gitSha))
        {
            return gitSha;
        }

        if (!string.IsNullOrWhiteSpace(sourceCommit))
        {
            return sourceCommit;
        }

        return "dev";
    }
}
