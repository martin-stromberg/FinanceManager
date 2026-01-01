/// <summary>
/// A no-op authentication token provider that does not supply any access token.
/// This placeholder implementation returns <c>null</c> and can be replaced with a real provider
/// that retrieves tokens from cookies, an identity provider or another token store.
/// </summary>
public sealed class NoOpAuthTokenProvider : IAuthTokenProvider
{
    /// <summary>
    /// Attempts to obtain an access token. This implementation always returns <c>null</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to cancel the operation.</param>
    /// <returns>
    /// A task that resolves to the access token string when available, or <c>null</c> when no token is provided.
    /// </returns>
    /// <exception cref="OperationCanceledException">May be thrown if the operation is cancelled via the provided <paramref name="cancellationToken"/>.</exception>
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Placeholder: returns no token. Replace with actual acquisition (e.g. from JWT issuer) when needed.
        return Task.FromResult<string?>(null);
    }
}