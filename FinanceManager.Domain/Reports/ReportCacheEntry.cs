namespace FinanceManager.Domain.Reports;

/// <summary>
/// Stores cached report data for a specific user and time range.
/// </summary>
public sealed class ReportCacheEntry : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor required for persistence.
    /// </summary>
    private ReportCacheEntry() { }

    /// <summary>
    /// Creates a new cache entry.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="cacheKey">Cache key for the report.</param>
    /// <param name="cacheValue">Serialized JSON cache value.</param>
    /// <param name="parameter">Additional parameter stored with the cache.</param>
    /// <param name="needsRefresh">Whether the cache entry needs to be recalculated.</param>
    public ReportCacheEntry(Guid ownerUserId, string cacheKey, string cacheValue, string? parameter, bool needsRefresh)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        CacheKey = Guards.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));
        CacheValue = Guards.NotNullOrWhiteSpace(cacheValue, nameof(cacheValue));
        Parameter = parameter ?? string.Empty;
        NeedsRefresh = needsRefresh;
    }

    /// <summary>
    /// Identifier of the user who owns the cached report.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Cache key identifying the report range and parameters.
    /// </summary>
    public string CacheKey { get; private set; } = string.Empty;

    /// <summary>
    /// Serialized JSON cache value.
    /// </summary>
    public string CacheValue { get; private set; } = string.Empty;

    /// <summary>
    /// Indicates whether the cache entry needs recalculation.
    /// </summary>
    public bool NeedsRefresh { get; private set; }

    /// <summary>
    /// Additional parameter stored alongside the cache entry.
    /// </summary>
    public string Parameter { get; private set; } = string.Empty;

    /// <summary>
    /// Updates the cached value and metadata.
    /// </summary>
    /// <param name="cacheValue">Serialized JSON cache value.</param>
    /// <param name="parameter">Additional parameter stored with the cache.</param>
    /// <param name="needsRefresh">Whether the cache entry needs recalculation.</param>
    public void Update(string cacheValue, string? parameter, bool needsRefresh)
    {
        CacheValue = Guards.NotNullOrWhiteSpace(cacheValue, nameof(cacheValue));
        Parameter = parameter ?? string.Empty;
        NeedsRefresh = needsRefresh;
        Touch();
    }

    /// <summary>
    /// Marks the cache entry for recalculation.
    /// </summary>
    public void MarkForRefresh()
    {
        NeedsRefresh = true;
        Touch();
    }
}
