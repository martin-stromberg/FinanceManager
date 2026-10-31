using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinanceManager.Infrastructure.Auth;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Auth;

/// <summary>Guards default and shortened renewal timing in both token refresh paths.</summary>
public sealed class JwtRenewalTimingTests
{
    /// <summary>The deployment defaults remain thirty-minute tokens and a five-minute minimum.</summary>
    [Fact]
    public void Defaults_PreserveProductionTiming()
    {
        var options = new JwtOptions();
        Assert.Equal(30, options.LifetimeMinutes);
        Assert.Equal(5, options.MinimumRenewalWindowMinutes);
        Assert.Equal(TimeSpan.FromMinutes(15), options.RenewalWindow);
    }

    /// <summary>Both refresh paths make the same decision, including fractional halves of odd lifetimes.</summary>
    /// <param name="lifetimeMinutes">The configured token lifetime.</param>
    /// <param name="minimumMinutes">The configured minimum window.</param>
    /// <param name="remainingSeconds">The token's remaining validity.</param>
    /// <param name="shouldRefresh">Whether the token is inside the renewal window.</param>
    [Theory]
    [InlineData(30, 5, 960, false)]
    [InlineData(30, 5, 840, true)]
    [InlineData(3, 1, 120, false)]
    [InlineData(3, 1, 75, true)]
    [InlineData(31, 5, 915, true)]
    [InlineData(3, 5, 120, true)]
    public async Task RefreshPaths_UseTheSameConfiguredWindow(int lifetimeMinutes, int minimumMinutes, int remainingSeconds, bool shouldRefresh)
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "test-signing-key-with-sufficient-length-1234567890",
            LifetimeMinutes = lifetimeMinutes,
            MinimumRenewalWindowMinutes = minimumMinutes
        });
        var validation = new JwtTokenValidationParametersFactory(options);
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            validation.Create().IssuerSigningKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            options.Value.Issuer, options.Value.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, "timing-user")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddSeconds(remainingSeconds), credentials));
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"FinanceManager.Auth={token}";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "timing-user")], "test"));
        var refresh = new Mock<IJwtRefreshService>();
        refresh.Setup(r => r.RefreshAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JwtRefreshResult.Success("renewed-token", DateTime.UtcNow.AddMinutes(lifetimeMinutes)));
        var provider = new JwtCookieAuthTokenProvider(new HttpContextAccessor { HttpContext = context }, options, validation, refresh.Object,
            new CircuitAuthTokenStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));

        var result = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(shouldRefresh ? "renewed-token" : token, result);
        refresh.Verify(r => r.RefreshAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), shouldRefresh ? Times.Once() : Times.Never());
        refresh.Invocations.Clear();

        using var services = new ServiceCollection().AddSingleton(validation).AddSingleton(provider)
            .AddSingleton(refresh.Object).BuildServiceProvider();
        context.RequestServices = services;
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/auth/keepalive";
        await new JwtRefreshMiddleware(_ => Task.CompletedTask, options).InvokeAsync(context);

        refresh.Verify(r => r.RefreshAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), shouldRefresh ? Times.Once() : Times.Never());
    }

    /// <summary>Passive non-keepalive traffic never renews the token, even inside the renewal window.</summary>
    [Fact]
    public async Task Middleware_DoesNotRenewOnNonKeepaliveRequests()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "test-signing-key-with-sufficient-length-1234567890",
            LifetimeMinutes = 3,
            MinimumRenewalWindowMinutes = 1
        });
        var validation = new JwtTokenValidationParametersFactory(options);
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            validation.Create().IssuerSigningKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            options.Value.Issuer, options.Value.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, "timing-user")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddSeconds(60), credentials));
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"FinanceManager.Auth={token}";
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/background-tasks/active";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "timing-user")], "test"));
        var refresh = new Mock<IJwtRefreshService>();
        var provider = new JwtCookieAuthTokenProvider(new HttpContextAccessor { HttpContext = context }, options, validation, refresh.Object,
            new CircuitAuthTokenStore(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));
        using var services = new ServiceCollection().AddSingleton(validation).AddSingleton(provider)
            .AddSingleton(refresh.Object).BuildServiceProvider();
        context.RequestServices = services;

        await new JwtRefreshMiddleware(_ => Task.CompletedTask, options).InvokeAsync(context);

        refresh.Verify(r => r.RefreshAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
