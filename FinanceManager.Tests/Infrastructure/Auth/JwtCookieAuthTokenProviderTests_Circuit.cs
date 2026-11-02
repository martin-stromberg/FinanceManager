using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceManager.Infrastructure.Auth;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Auth;

/// <summary>Checks long-lived circuit state, identity boundaries and late refresh responses.</summary>
public sealed class JwtCookieAuthTokenProviderTests_Circuit
{
    private static readonly IOptions<JwtOptions> Options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
    {
        Key = "test-signing-key-with-sufficient-length-1234567890",
        Issuer = "financemanager",
        Audience = "financemanager",
        LifetimeMinutes = 30
    });

    /// <summary>A valid circuit token remains usable inside the renewal window.</summary>
    [Fact]
    public async Task GetAccessTokenAsync_CachedTokenInsideRenewalWindow_RemainsUsable()
    {
        var (sut, _) = CreateProvider();
        var userId = Guid.NewGuid();
        var token = CreateToken(userId, DateTime.UtcNow.AddMinutes(30));
        sut.PrimeCache(token, DateTimeOffset.UtcNow.AddMinutes(30));

        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(token, actual);
    }

    /// <summary>The persisted state carries an opaque handoff key, never the token itself.</summary>
    [Fact]
    public void PersistedTokenKey_Getter_ReturnsOpaqueKeyNotToken()
    {
        var (sut, _) = CreateProvider();
        var token = CreateToken(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(30));
        sut.PrimeCache(token, DateTimeOffset.UtcNow.AddMinutes(30));

        var key = sut.PersistedTokenKey;

        Assert.NotNull(key);
        Assert.DoesNotContain(".", key);
        Assert.NotEqual(token, key);
    }

    /// <summary>A circuit provider restores its token from a handoff key issued by the prerender provider.</summary>
    [Fact]
    public async Task PersistedTokenKey_RoundTrip_PrimesCircuit()
    {
        var store = new CircuitAuthTokenStore(new MemoryCache(new MemoryCacheOptions()));
        var (prerenderProvider, _) = CreateProvider(store);
        var userId = Guid.NewGuid();
        var expiry = DateTime.UtcNow.AddMinutes(30);
        var token = CreateToken(userId, expiry);
        prerenderProvider.PrimeCache(token, new DateTimeOffset(expiry));
        var key = prerenderProvider.PersistedTokenKey;

        var (circuitProvider, _) = CreateProvider(store);
        circuitProvider.PersistedTokenKey = key;

        Assert.Equal(token, await circuitProvider.GetAccessTokenAsync(CancellationToken.None));
        Assert.True(store.TryGetCurrent(userId, out var current, out _));
        Assert.Equal(token, current);
    }

    /// <summary>A handoff key can be consumed exactly once.</summary>
    [Fact]
    public async Task PersistedTokenKey_SingleUse_SecondConsumerGetsNoToken()
    {
        var store = new CircuitAuthTokenStore(new MemoryCache(new MemoryCacheOptions()));
        var (prerenderProvider, _) = CreateProvider(store);
        var token = CreateToken(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(30));
        prerenderProvider.PrimeCache(token, DateTimeOffset.UtcNow.AddMinutes(30));
        var key = prerenderProvider.PersistedTokenKey;

        var (first, _) = CreateProvider(store);
        first.PersistedTokenKey = key;
        var (second, _) = CreateProvider(store);
        second.PersistedTokenKey = key;

        Assert.Equal(token, await first.GetAccessTokenAsync(CancellationToken.None));
        Assert.Null(await second.GetAccessTokenAsync(CancellationToken.None));
    }

    /// <summary>An unknown or expired handoff key yields no token.</summary>
    [Fact]
    public async Task PersistedTokenKey_UnknownOrExpiredKey_YieldsNoToken()
    {
        var (sut, _) = CreateProvider();

        sut.PersistedTokenKey = Guid.NewGuid().ToString("N");

        Assert.Null(await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    /// <summary>Persisted state still uses ordinary JWT lifetime validation.</summary>
    [Fact]
    public async Task PersistedTokenKey_ExpiredToken_IsRejected()
    {
        var store = new CircuitAuthTokenStore(new MemoryCache(new MemoryCacheOptions()));
        var userId = Guid.NewGuid();
        var key = store.StoreForHandoff(CreateToken(userId, DateTime.UtcNow.AddMinutes(-1)), DateTimeOffset.UtcNow.AddMinutes(-1));
        var (sut, _) = CreateProvider(store);

        sut.PersistedTokenKey = key;

        Assert.Null(await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    /// <summary>An expiring cached circuit token is replaced by a newer registry token of the same subject.</summary>
    [Fact]
    public async Task GetAccessTokenAsync_ExpiringCache_PullsNewerTokenFromRegistry()
    {
        var (sut, store) = CreateProvider();
        var userId = Guid.NewGuid();
        // Inside the renewal window (< 15 minutes of the 30 minute lifetime).
        var cached = CreateToken(userId, DateTime.UtcNow.AddMinutes(10));
        sut.PrimeCache(cached, DateTimeOffset.UtcNow.AddMinutes(10));
        var renewed = CreateToken(userId, DateTime.UtcNow.AddMinutes(30));
        store.SetCurrent(userId, renewed, DateTimeOffset.UtcNow.AddMinutes(30));

        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(renewed, actual);
    }

    /// <summary>A registry token of a different subject must not overwrite circuit authentication.</summary>
    [Fact]
    public async Task GetAccessTokenAsync_RegistryTokenOfOtherSubject_IsIgnored()
    {
        var (sut, store) = CreateProvider();
        var userId = Guid.NewGuid();
        var cached = CreateToken(userId, DateTime.UtcNow.AddMinutes(10));
        sut.PrimeCache(cached, DateTimeOffset.UtcNow.AddMinutes(10));
        var foreign = CreateToken(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(30));
        store.SetCurrent(Guid.NewGuid(), foreign, DateTimeOffset.UtcNow.AddMinutes(30));

        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(cached, actual);
    }

    /// <summary>An expired cached token is replaced by a valid registry token of the same subject.</summary>
    [Fact]
    public async Task GetAccessTokenAsync_ExpiredCache_AdoptsRegistryToken()
    {
        var (sut, store) = CreateProvider();
        var userId = Guid.NewGuid();
        // A well-formed cached token whose declared cache expiry already elapsed.
        sut.PrimeCache(CreateToken(userId, DateTime.UtcNow.AddMinutes(30)), DateTimeOffset.UtcNow.AddMinutes(-5));
        var renewed = CreateToken(userId, DateTime.UtcNow.AddMinutes(30));
        store.SetCurrent(userId, renewed, DateTimeOffset.UtcNow.AddMinutes(30));

        var actual = await sut.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(renewed, actual);
    }

    /// <summary>An invalidated circuit cannot be resurrected by a registry entry.</summary>
    [Fact]
    public async Task GetAccessTokenAsync_AfterInvalidation_DoesNotRestoreToken()
    {
        var (sut, store) = CreateProvider();
        var userId = Guid.NewGuid();
        sut.PrimeCache(CreateToken(userId, DateTime.UtcNow.AddMinutes(10)), DateTimeOffset.UtcNow.AddMinutes(10));
        sut.InvalidateCache();
        store.SetCurrent(userId, CreateToken(userId, DateTime.UtcNow.AddMinutes(30)), DateTimeOffset.UtcNow.AddMinutes(30));

        Assert.Null(await sut.GetAccessTokenAsync(CancellationToken.None));
    }

    private static (JwtCookieAuthTokenProvider provider, CircuitAuthTokenStore store) CreateProvider(CircuitAuthTokenStore? store = null)
    {
        store ??= new CircuitAuthTokenStore(new MemoryCache(new MemoryCacheOptions()));
        return (new JwtCookieAuthTokenProvider(new HttpContextAccessor(), Options,
            new JwtTokenValidationParametersFactory(Options), Mock.Of<IJwtRefreshService>(), store), store);
    }

    private static string CreateToken(Guid userId, DateTime expires) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        Options.Value.Issuer, Options.Value.Audience, [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
        notBefore: DateTime.UtcNow.AddMinutes(-2), expires: expires,
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Value.Key)), SecurityAlgorithms.HmacSha256)));
}
