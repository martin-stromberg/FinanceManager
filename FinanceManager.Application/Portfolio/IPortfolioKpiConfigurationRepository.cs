using FinanceManager.Domain.Portfolio;

namespace FinanceManager.Application.Portfolio;

/// <summary>
/// Repository abstraction for loading and persisting a user's <see cref="PortfolioKpiConfiguration"/>.
/// </summary>
public interface IPortfolioKpiConfigurationRepository
{
    /// <summary>
    /// Loads the configuration for the given owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The existing configuration, or <c>null</c> when none has been saved yet.</returns>
    Task<PortfolioKpiConfiguration?> GetAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates or updates the configuration for the given owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="activeTileIds">JSON-serialized array of active tile ids.</param>
    /// <param name="tileOrder">JSON-serialized array describing tile order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted <see cref="PortfolioKpiConfiguration"/>.</returns>
    Task<PortfolioKpiConfiguration> UpsertAsync(Guid ownerUserId, string activeTileIds, string tileOrder, CancellationToken ct);

    /// <summary>
    /// Deletes the configuration for the given owner, if any.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid ownerUserId, CancellationToken ct);
}
