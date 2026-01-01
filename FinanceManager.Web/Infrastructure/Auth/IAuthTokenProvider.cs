/// <summary>
/// Provides access tokens for authenticated HTTP requests. Implementations encapsulate token retrieval/refresh logic
/// (for example using client credentials, hosted user tokens or cached/rotated tokens).
/// </summary>
public interface IAuthTokenProvider
{
    /// <summary>
    /// Asynchronously obtains an access token suitable for authorizing outgoing HTTP requests.
    /// Implementations should respect the provided <paramref name="cancellationToken"/> and may cache or refresh tokens as needed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to cancel the token retrieval operation.</param>
    /// <returns>
    /// A task that resolves to the access token string when available, or <c>null</c> when no token could be obtained.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the provided <paramref name="cancellationToken"/>.</exception>
    /// <exception cref="InvalidOperationException">May be thrown when the provider is not properly configured or cannot obtain a token due to configuration errors.</exception>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}