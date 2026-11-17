using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fisga.Api.Tests;

[Collection("api")]
public sealed class IdentityOptionsTests(ApiTestFixture fixture)
{
    private readonly ApiTestFixture _fixture = fixture;

    [Fact]
    public void Identity_usa_cookie_e_politicas_do_spec()
    {
        var identity = _fixture.Factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var cookie = _fixture.Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("fisga_auth", cookie.Cookie.Name);
        Assert.True(identity.SignIn.RequireConfirmedEmail);
        Assert.Equal(10, identity.Password.RequiredLength);
        Assert.False(identity.Password.RequireNonAlphanumeric);
        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), identity.Lockout.DefaultLockoutTimeSpan);

        // Host de teste roda em Development, onde Auth:CookieSecure é false.
        Assert.Equal(CookieSecurePolicy.SameAsRequest, cookie.Cookie.SecurePolicy);
    }
}
