using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fisga.Infrastructure.Identity;

/// <summary>
/// Opções do provedor de token dedicado à redefinição de senha: validade curta (15 min)
/// e nome próprio, para não compartilhar o lifespan com a confirmação de e-mail (que
/// permanece no provedor "Default", de 1 dia).
/// </summary>
public sealed class ResetPasswordTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public ResetPasswordTokenProviderOptions()
    {
        Name = "ResetPassword";
        TokenLifespan = TimeSpan.FromMinutes(15);
    }
}

/// <summary>
/// Provedor de token usado apenas para redefinição de senha, com <see cref="ResetPasswordTokenProviderOptions"/>
/// (15 min). A confirmação de e-mail continua usando o provedor "Default" (DataProtector) de 1 dia,
/// para que o link de confirmação do cadastro não expire prematuramente.
/// </summary>
public sealed class ResetPasswordTokenProvider : DataProtectorTokenProvider<ApplicationUser>
{
    public ResetPasswordTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ResetPasswordTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
