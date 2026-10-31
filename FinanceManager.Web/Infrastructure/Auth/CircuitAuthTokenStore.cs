using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Web.Infrastructure.Auth;

/// <summary>
/// Process-wide store for authentication tokens shared between HTTP requests and Blazor circuits.
/// Provides a single-use handoff for the prerender-to-circuit transition and a per-user registry
/// of the most recently issued token, so a circuit can adopt browser-side token renewals without
/// the token ever leaving the server.
/// </summary>
public sealed class CircuitAuthTokenStore
{
    private const string HandoffKeyPrefix = "CircuitAuth.Handoff.";
    private const string CurrentKeyPrefix = "CircuitAuth.Current.";
    private static readonly TimeSpan s_handoffLifetime = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    // Serializes lookup+removal inside TryTakeHandoff: IMemoryCache has no atomic
    // GetAndRemove, so without synchronization two consumers could take the same
    // handoff key and break the single-use guarantee.
    private readonly object _handoffLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="CircuitAuthTokenStore"/>.
    /// </summary>
    /// <param name="cache">Memory cache used to hold handoff and registry entries.</param>
    public CircuitAuthTokenStore(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Stores a token for a single-use handoff to a circuit and returns the random key
    /// that can be handed to the client instead of the token itself.
    /// </summary>
    /// <param name="token">The JWT to hand off.</param>
    /// <param name="expiry">The token expiry timestamp.</param>
    /// <returns>An opaque, single-use handoff key.</returns>
    public string StoreForHandoff(string token, DateTimeOffset expiry)
    {
        var key = Guid.NewGuid().ToString("N");
        // The shared IMemoryCache is configured with a SizeLimit, so every entry must
        // declare a Size - one unit per stored token keeps the accounting simple.
        _cache.Set(HandoffKeyPrefix + key, new StoredToken(token, expiry), new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = s_handoffLifetime
        });
        return key;
    }

    /// <summary>
    /// Retrieves and removes the token stored under the given handoff key.
    /// </summary>
    /// <param name="key">The handoff key previously returned by <see cref="StoreForHandoff"/>.</param>
    /// <param name="token">The stored token, or <c>null</c> when the key is unknown or expired.</param>
    /// <param name="expiry">The stored token expiry.</param>
    /// <returns><c>true</c> when a token was found and consumed.</returns>
    public bool TryTakeHandoff(string key, out string? token, out DateTimeOffset expiry)
    {
        token = null;
        expiry = default;
        var cacheKey = HandoffKeyPrefix + key;
        lock (_handoffLock)
        {
            if (_cache.TryGetValue(cacheKey, out StoredToken? stored) && stored is not null)
            {
                _cache.Remove(cacheKey);
                token = stored.Token;
                expiry = stored.Expiry;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Publishes the most recently issued token for a user so active circuits of the same
    /// user can adopt it.
    /// </summary>
    /// <param name="userId">The token subject (user id).</param>
    /// <param name="token">The latest issued JWT for the user.</param>
    /// <param name="expiry">The token expiry timestamp.</param>
    public void SetCurrent(Guid userId, string token, DateTimeOffset expiry)
    {
        _cache.Set(CurrentKeyPrefix + userId.ToString("N"), new StoredToken(token, expiry), new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpiration = expiry
        });
    }

    /// <summary>
    /// Retrieves the most recently published token for a user.
    /// </summary>
    /// <param name="userId">The user id (token subject).</param>
    /// <param name="token">The stored token, or <c>null</c> when none is known.</param>
    /// <param name="expiry">The stored token expiry.</param>
    /// <returns><c>true</c> when a token is registered for the user.</returns>
    public bool TryGetCurrent(Guid userId, out string? token, out DateTimeOffset expiry)
    {
        token = null;
        expiry = default;
        if (_cache.TryGetValue(CurrentKeyPrefix + userId.ToString("N"), out StoredToken? stored) && stored is not null)
        {
            token = stored.Token;
            expiry = stored.Expiry;
            return true;
        }

        return false;
    }

    private sealed record StoredToken(string Token, DateTimeOffset Expiry);
}
