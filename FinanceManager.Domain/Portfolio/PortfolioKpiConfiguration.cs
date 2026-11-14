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

    // Backup DTO
    /// <summary>
    /// DTO carrying the serializable state of a <see cref="PortfolioKpiConfiguration"/> for backup purposes.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="OwnerUserId">Identifier of the user who owns this configuration.</param>
    /// <param name="ActiveTileIds">JSON-serialized array of active tile ids.</param>
    /// <param name="TileOrder">JSON-serialized array describing tile order.</param>
    /// <param name="UpdatedUtc">UTC timestamp of the last update to this configuration.</param>
    /// <param name="CreatedUtc">Entity creation timestamp UTC.</param>
    /// <param name="ModifiedUtc">Entity last modified timestamp UTC, if any.</param>
    /// <returns>The result.</returns>
    public sealed record PortfolioKpiConfigurationBackupDto(Guid Id, Guid OwnerUserId, string ActiveTileIds, string TileOrder, DateTime UpdatedUtc, DateTime CreatedUtc, DateTime? ModifiedUtc);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this configuration.
    /// </summary>
    /// <returns>A <see cref="PortfolioKpiConfigurationBackupDto"/> containing values required for backup/restore.</returns>
    public PortfolioKpiConfigurationBackupDto ToBackupDto()
        => new PortfolioKpiConfigurationBackupDto(Id, OwnerUserId, ActiveTileIds, TileOrder, UpdatedUtc, CreatedUtc, ModifiedUtc);

    /// <summary>
    /// Assigns values from a backup DTO to this entity instance.
    /// </summary>
    /// <param name="dto">The <see cref="PortfolioKpiConfigurationBackupDto"/> containing values to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the tile payloads are null or whitespace.</exception>
    public void AssignBackupDto(PortfolioKpiConfigurationBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        Update(dto.ActiveTileIds, dto.TileOrder);
        // Restore the original update timestamp carried by the backup (Update stamps UtcNow).
        UpdatedUtc = dto.UpdatedUtc;
        SetDates(dto.CreatedUtc, dto.ModifiedUtc);
    }
}
