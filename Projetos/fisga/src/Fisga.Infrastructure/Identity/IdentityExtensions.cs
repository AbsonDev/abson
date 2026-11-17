using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Fisga.Infrastructure.Persistence;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Registra a ASP.NET Core Identity completa: usuário de autenticação
/// (<see cref="ApplicationUser"/>), cookie <c>fisga_auth</c>, políticas de
/// senha/lockout/e-mail e, quando configurado, o login externo do Google.
/// As chaves do Data Protection são persistidas em disco para o cookie
/// sobreviver a restart/deploy.
/// </summary>
public static class IdentityExtensions
{
    public static IServiceCollection AdicionarIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(ObterCaminhoDasChaves(configuration)));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(opcoes =>
        {
            // Senha: mínimo 10, máximo 128 (limite do Identity), sem exigir
            // maiúscula, número ou símbolo.
            opcoes.Password.RequiredLength = 10;
            opcoes.Password.RequireDigit = false;
            opcoes.Password.RequireLowercase = false;
            opcoes.Password.RequireUppercase = false;
            opcoes.Password.RequireNonAlphanumeric = false;
            opcoes.Password.RequiredUniqueChars = 0;

            // Lockout após 5 tentativas por 15 minutos.
            opcoes.Lockout.AllowedForNewUsers = true;
            opcoes.Lockout.MaxFailedAccessAttempts = 5;
            opcoes.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

            // Login por senha exige e-mail confirmado.
            opcoes.SignIn.RequireConfirmedEmail = true;

            // E-mail único e normalizado.
            opcoes.User.RequireUniqueEmail = true;
        })
            .AddEntityFrameworkStores<FisgaDbContext>()
            .AddDefaultTokenProviders();

        // Auth:CookieSecure é a fonte única da política Secure do cookie da Identity:
        // true (produção) → Secure=Always; false (dev/teste) → Secure apenas sob HTTPS.
        var cookieSecure = configuration.GetValue<bool?>("Auth:CookieSecure") ?? true;

        services.ConfigureApplicationCookie(opcoes =>
        {
            opcoes.Cookie.Name = "fisga_auth";
            opcoes.Cookie.HttpOnly = true;
            opcoes.Cookie.SameSite = SameSiteMode.Lax;
            opcoes.Cookie.SecurePolicy = cookieSecure
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            opcoes.ExpireTimeSpan = TimeSpan.FromDays(30);
            opcoes.SlidingExpiration = true;
        });

        var clientId = configuration["Google:ClientId"];
        var clientSecret = configuration["Google:ClientSecret"];

        // Google só é registrado quando ClientId e ClientSecret estão preenchidos;
        // sem isso o app sobe normalmente e o login Google permanece indisponível.
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            services.AddAuthentication()
                .AddGoogle(google =>
                {
                    google.ClientId = clientId;
                    google.ClientSecret = clientSecret;
                    google.SignInScheme = IdentityConstants.ExternalScheme;
                    google.CallbackPath = "/signin-google";
                    google.SaveTokens = true;
                });
        }

        return services;
    }

    private static string ObterCaminhoDasChaves(IConfiguration configuration)
    {
        var caminho = configuration["DataProtection:KeysPath"];
        return string.IsNullOrWhiteSpace(caminho) ? "data/keys" : caminho;
    }
}
