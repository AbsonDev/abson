namespace Fisga.Api.Tests;

// Todas as classes de teste de API DEVEM usar a collection "api". O
// WebApplicationFactory cacheia o host estaticamente; este fixture compartilhado
// garante um único host e um único banco SQLite por execução de teste, além de
// restaurar as variáveis de ambiente no dispose.
[CollectionDefinition("api")]
public sealed class ApiTestCollection : ICollectionFixture<ApiTestFixture>
{
}

public sealed class ApiTestFixture : IDisposable
{
    private readonly string? _providerOriginal;
    private readonly string? _connectionStringOriginal;
    private readonly string? _sentryDsnOriginal;
    private readonly string _dbPath;

    public ApiTestFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"fisga-api-tests-{Guid.NewGuid():N}.db");

        _providerOriginal = Environment.GetEnvironmentVariable("Database__Provider");
        _connectionStringOriginal = Environment.GetEnvironmentVariable("Database__ConnectionString");
        _sentryDsnOriginal = Environment.GetEnvironmentVariable("Sentry__Dsn");

        // A API lê Database:* e Sentry durante Program.Main (no CreateBuilder),
        // antes de o ConfigureAppConfiguration/ConfigureServices serem aplicados ao
        // host real. Variáveis de ambiente são lidas pelo próprio CreateBuilder e têm
        // precedência sobre o appsettings; por isso são definidas aqui, uma única vez,
        // antes de o host ser construído, e restauradas no Dispose.
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Database__ConnectionString", $"Data Source={_dbPath}");
        Environment.SetEnvironmentVariable("Sentry__Dsn", "");

        Factory = new TestWebApplicationFactory(_dbPath);
    }

    public TestWebApplicationFactory Factory { get; }

    public string DbPath => _dbPath;

    public void Dispose()
    {
        Factory.Dispose();

        Restaurar("Database__Provider", _providerOriginal);
        Restaurar("Database__ConnectionString", _connectionStringOriginal);
        Restaurar("Sentry__Dsn", _sentryDsnOriginal);

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private static void Restaurar(string nome, string? valor) =>
        Environment.SetEnvironmentVariable(nome, valor);
}
