using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities;

/// <summary>
/// Request for a provider-based price refresh pass for a single security.
/// </summary>
/// <param name="OwnerUserId">Owner of the security.</param>
/// <param name="SecurityId">Target security identifier.</param>
/// <param name="AlphaVantageCode">Provider symbol to query.</param>
/// <param name="SecurityName">Display name of the security (used for user-facing messages).</param>
/// <param name="Identifier">Display identifier of the security (used for user-facing messages).</param>
/// <param name="StartExclusive">Only dates strictly after this date are fetched (date portion considered).</param>
/// <param name="EndInclusive">Only dates up to and including this date are fetched (date portion considered).</param>
public sealed record SecurityPriceRefreshRequest(
    Guid OwnerUserId,
    Guid SecurityId,
    string AlphaVantageCode,
    string SecurityName,
    string? Identifier,
    DateTime StartExclusive,
    DateTime EndInclusive);

/// <summary>
/// Outcome of a provider-based price refresh pass.
/// </summary>
public enum SecurityPriceRefreshStatus
{
    /// <summary>Provider returned data; prices were stored and signal evaluations executed.</summary>
    Stored,

    /// <summary>Provider returned no prices for the requested window.</summary>
    NoData,

    /// <summary>
    /// Provider reported a domain error (for example an invalid symbol); the security was marked
    /// with the price error and the owner was notified.
    /// </summary>
    ProviderError
}

/// <summary>
/// Result of a provider-based price refresh pass.
/// </summary>
/// <param name="Status">Overall outcome of the pass.</param>
/// <param name="StoreResult">Counters from the upsert step when <paramref name="Status"/> is <see cref="SecurityPriceRefreshStatus.Stored"/>.</param>
/// <param name="ErrorMessage">Provider error message when <paramref name="Status"/> is <see cref="SecurityPriceRefreshStatus.ProviderError"/>.</param>
public sealed record SecurityPriceRefreshResult(
    SecurityPriceRefreshStatus Status,
    SecurityPriceImportResultDto? StoreResult = null,
    string? ErrorMessage = null);

/// <summary>
/// Single entry point for the security price maintenance process.
/// Encapsulates the sequence fetch → error state → persist → signal evaluation → notification,
/// so callers no longer compose the individual steps themselves.
/// </summary>
public interface ISecurityPriceUpdateService
{
    /// <summary>
    /// Executes the full provider-based refresh pass for one security: fetches daily prices from the
    /// configured price provider, clears the price error state, persists weekday prices via upsert
    /// and runs the signal evaluations (golden cross, trend reversal). On a provider domain error
    /// (<see cref="InvalidOperationException"/>) the security is marked with the error and the owner
    /// is notified once per distinct message.
    /// </summary>
    /// <param name="request">Pass parameters including ownership, target window and display names.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refresh result describing stored data or the provider error.</returns>
    /// <exception cref="RequestLimitExceededException">Propagated so the caller can stop or abort its run.</exception>
    Task<SecurityPriceRefreshResult> RefreshFromProviderAsync(SecurityPriceRefreshRequest request, CancellationToken ct);

    /// <summary>
    /// Executes the store-and-evaluate part of the pass for already provided price items
    /// (for example from a file import): persists them via upsert and runs the signal evaluations.
    /// </summary>
    /// <param name="ownerUserId">Owner of the security.</param>
    /// <param name="securityId">Target security identifier.</param>
    /// <param name="items">Price items to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Counters for inserted, updated and unchanged records.</returns>
    Task<SecurityPriceImportResultDto> StoreAndEvaluateAsync(Guid ownerUserId, Guid securityId, IReadOnlyList<SecurityPriceImportItem> items, CancellationToken ct);
}
