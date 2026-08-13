namespace FinanceManager.Domain.Portfolio;

/// <summary>
/// Persists a user's tile selection and tile order for the portfolio analysis report.
/// One configuration exists per owning user.
/// </summary>
public sealed class PortfolioKpiConfiguration : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor required for persistence.
    /// </summary>
    private PortfolioKpiConfiguration() { }

    /// <summary>
    /// Creates a new portfolio KPI configuration for the given owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="activeTileIds">JSON-serialized array of active tile ids.</param>
    /// <param name="tileOrder">JSON-serialized array describing tile order.</param>
    public PortfolioKpiConfiguration(Guid ownerUserId, string activeTileIds, string tileOrder)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        ActiveTileIds = Guards.NotNullOrWhiteSpace(activeTileIds, nameof(activeTileIds));
        TileOrder = Guards.NotNullOrWhiteSpace(tileOrder, nameof(tileOrder));
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Identifier of the user who owns this configuration.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// JSON-serialized array of active (visible) tile ids.
    /// </summary>
    public string ActiveTileIds { get; private set; } = string.Empty;

    /// <summary>
    /// JSON-serialized array describing the display order of tile ids.
    /// </summary>
    public string TileOrder { get; private set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the last update to this configuration.
    /// </summary>
    public DateTime UpdatedUtc { get; private set; }

    /// <summary>
    /// Updates the tile selection and order.
    /// </summary>
    /// <param name="activeTileIds">JSON-serialized array of active tile ids.</param>
    /// <param name="tileOrder">JSON-serialized array describing tile order.</param>
    public void Update(string activeTileIds, string tileOrder)
    {
        ActiveTileIds = Guards.NotNullOrWhiteSpace(activeTileIds, nameof(activeTileIds));
        TileOrder = Guards.NotNullOrWhiteSpace(tileOrder, nameof(tileOrder));
        UpdatedUtc = DateTime.UtcNow;
        Touch();
    }
}
