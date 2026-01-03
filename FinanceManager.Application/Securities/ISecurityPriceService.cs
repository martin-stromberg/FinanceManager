using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities;

/// <summary>
/// Service responsible for managing security price records (historical close prices).
/// Implementations persist and query <see cref="SecurityPriceDto"/> records for a given security and owner user.
/// </summary>
public interface ISecurityPriceService
{
    /// <summary>
    /// Creates a security price record for the specified security on the given date.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns the security.</param>
    /// <param name="securityId">The identifier of the security to which the price belongs.</param>
    /// <param name="date">The date (UTC date) the price applies to.</param>
    /// <param name="close">The closing price value for the date.</param>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the price record has been created.</returns>
    /// <exception cref="ArgumentException">Thrown when the specified security does not exist or is not owned by <paramref name="ownerUserId"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="close"/> is not a valid price (for example negative or NaN).</exception>
    /// <remarks>
    /// Implementations should validate ownership and the provided values before persisting.
    /// The method is asynchronous and should honour the provided <paramref name="ct"/>.
    /// </remarks>
    Task CreateAsync(Guid ownerUserId, Guid securityId, DateTime date, decimal close, CancellationToken ct);

    /// <summary>
    /// Lists historical security prices for the specified security in descending date order.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns the security.</param>
    /// <param name="securityId">The identifier of the security whose prices to list.</param>
    /// <param name="skip">Number of records to skip (for paging).</param>
    /// <param name="take">Maximum number of records to return (for paging).</param>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>
    /// A task that returns a read-only list of <see cref="SecurityPriceDto"/> ordered by date descending.
    /// If the security is not owned by <paramref name="ownerUserId"/> an empty list may be returned or
    /// the implementation may throw an <see cref="ArgumentException"/> to indicate invalid ownership.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the specified security does not exist or is not owned by <paramref name="ownerUserId"/>.</exception>
    Task<IReadOnlyList<SecurityPriceDto>> ListAsync(Guid ownerUserId, Guid securityId, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Returns the most recent stored price date for the specified security, or <c>null</c> if no price exists.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns the security.</param>
    /// <param name="securityId">The identifier of the security.</param>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that returns the date of the latest stored price, or <c>null</c> when no price exists.</returns>
    Task<DateTime?> GetLatestDateAsync(Guid ownerUserId, Guid securityId, CancellationToken ct);

    /// <summary>
    /// Sets a price error message on the specified security. This is used to mark a security
    /// as having an error when price lookups fail (for example invalid external symbol).
    /// </summary>
    /// <param name="ownerUserId">The owner of the security.</param>
    /// <param name="securityId">The security identifier.</param>
    /// <param name="message">A short error message to record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the operation has been applied.</returns>
    Task SetPriceErrorAsync(Guid ownerUserId, Guid securityId, string message, CancellationToken ct);
}
