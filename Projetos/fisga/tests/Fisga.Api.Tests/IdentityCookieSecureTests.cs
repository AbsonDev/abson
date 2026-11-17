using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Fisga.Infrastructure.Identity;

namespace Fisga.Api.Tests;

/// <summary>
/// Teste focado (sem host) da política <c>Secure</c> do cookie da Identity.
/// Não cria um segundo host: exercita <see cref="IdentityExtensions.AdicionarIdentity"/>
/// diretamente sobre um <see cref="ServiceCollection"/> com configuração em memória.
/// </summary>
public sealed class IdentityCookieSecureTests
{
    [Theory]
    [InlineData("true", CookieSecurePolicy.Always)]
    [InlineData("false", CookieSecurePolicy.SameAsRequest)]
    public void Cookie_secure_policy_segue_auth_cookie_secure(string valor, CookieSecurePolicy esperado)
    {
        var pastaChaves = Path.Combine(Path.GetTempPath(), $"fisga-keys-{Guid.NewGuid():N}");

        try
        {
            var configuracoes = new Dictionary<string, string?>
            {
                ["Auth:CookieSecure"] = valor,
                ["DataProtection:KeysPath"] = pastaChaves
            };

            var services = new ServiceCollection();
            services.AddLogging();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configuracoes)
                .Build();

            services.AdicionarIdentity(configuration);

            using var provider = services.BuildServiceProvider();
            var cookie = provider
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);

            Assert.Equal(esperado, cookie.Cookie.SecurePolicy);
        }
        finally
        {
            if (Directory.Exists(pastaChaves))
            {
                Directory.Delete(pastaChaves, recursive: true);
            }
        }
    }
}
