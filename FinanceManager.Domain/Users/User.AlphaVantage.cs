namespace FinanceManager.Domain.Users;

/// <summary>
/// Partial <see cref="User"/> type containing settings related to AlphaVantage integration (API key and sharing preference).
/// </summary>
public sealed partial class User
{
    /// <summary>
    /// User-provided AlphaVantage API key used for price lookups. Null when not set.
    /// </summary>
    /// <value>The API key string or <c>null</c> if none is configured.</value>
    public string? AlphaVantageApiKey { get; private set; }

    /// <summary>
    /// Indicates whether the user has chosen to share their AlphaVantage API key with other users (if the application supports sharing).
    /// </summary>
    /// <value><c>true</c> when the API key may be shared; otherwise <c>false</c>.</value>
    public bool ShareAlphaVantageApiKey { get; private set; }

    /// <summary>
    /// Sets or clears the AlphaVantage API key for the user. Passing a null or whitespace value clears the stored key.
    /// </summary>
    /// <param name="apiKey">The API key to store, or <c>null</c>/<see cref="string.Empty"/>/whitespace to clear the key.</param>
    public void SetAlphaVantageKey(string? apiKey)
    {
        // allow clearing with null/empty
        AlphaVantageApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        Touch();
    }

    /// <summary>
    /// Sets the user's preference whether to share their AlphaVantage API key with others.
    /// </summary>
    /// <param name="share">If <c>true</c>, the key may be shared; otherwise it remains private.</param>
    public void SetShareAlphaVantageKey(bool share)
    {
        ShareAlphaVantageApiKey = share;
        Touch();
    }
}