namespace FinanceManager.Domain.Reports;

/// <summary>
/// A single KPI configuration entry on the home dashboard for a specific user.
/// Can point to a predefined KPI or a report favorite.
/// </summary>
public sealed class HomeKpi : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor required for persistence/ORM and deserialization.
    /// </summary>
    private HomeKpi() { }

    /// <summary>
    /// Creates a new <see cref="HomeKpi"/> for a given owner user.
    /// </summary>
    /// <param name="ownerUserId">The owner user identifier. Must not be empty.</param>
    /// <param name="kind">The kind of KPI (predefined or report favorite).</param>
    /// <param name="displayMode">How the KPI should be displayed.</param>
    /// <param name="sortOrder">Sort order for rendering on the dashboard.</param>
    /// <param name="reportFavoriteId">Optional id of a report favorite when <paramref name="kind"/> is <see cref="HomeKpiKind.ReportFavorite"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ownerUserId"/> is an empty GUID or when required invariants are not satisfied (see <see cref="Validate"/>).</exception>
    public HomeKpi(Guid ownerUserId, HomeKpiKind kind, HomeKpiDisplayMode displayMode, int sortOrder, Guid? reportFavoriteId = null)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        Kind = kind;
        DisplayMode = displayMode;
        SortOrder = sortOrder;
        ReportFavoriteId = reportFavoriteId;
        Validate();
    }

    /// <summary>
    /// Identifier of the user who owns this KPI configuration.
    /// </summary>
    /// <value>The owner user id.</value>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Kind of the home KPI (e.g. predefined or report favorite).
    /// </summary>
    /// <value>The KPI kind.</value>
    public HomeKpiKind Kind { get; private set; }

    /// <summary>
    /// Optional id of the report favorite this KPI points to.
    /// </summary>
    /// <value>The report favorite id or null.</value>
    public Guid? ReportFavoriteId { get; private set; }

    /// <summary>
    /// Display mode used to render the KPI on the dashboard.
    /// </summary>
    /// <value>The display mode.</value>
    public HomeKpiDisplayMode DisplayMode { get; private set; }

    /// <summary>
    /// Sort order of the KPI on the dashboard.
    /// </summary>
    /// <value>Sort order (integer).</value>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Optional custom title provided by the user.
    /// </summary>
    /// <value>The title or null.</value>
    public string? Title { get; private set; }

    /// <summary>
    /// Optional predefined KPI type when <see cref="Kind"/> is a predefined KPI.
    /// </summary>
    /// <value>The predefined KPI type or null.</value>
    public HomeKpiPredefined? PredefinedType { get; private set; }

    /// <summary>
    /// Updates the display mode for this KPI.
    /// </summary>
    /// <param name="mode">The new display mode.</param>
    /// <remarks>Callers are expected to persist the entity after modification.</remarks>
    public void SetDisplayMode(HomeKpiDisplayMode mode)
    {
        DisplayMode = mode;
        Touch();
    }

    /// <summary>
    /// Sets the sort order for this KPI.
    /// </summary>
    /// <param name="order">The new sort order.</param>
    /// <remarks>Lower values are rendered earlier. Callers should persist the change.</remarks>
    public void SetSortOrder(int order)
    {
        SortOrder = order;
        Touch();
    }

    /// <summary>
    /// Assigns or clears the report favorite referenced by this KPI.
    /// </summary>
    /// <param name="favoriteId">The report favorite id to reference, or null to clear it.</param>
    /// <returns>None (void). The entity is modified in-place.</returns>
    /// <exception cref="ArgumentException">Thrown when the resulting state does not satisfy domain invariants (see <see cref="Validate"/>).</exception>
    public void SetFavorite(Guid? favoriteId)
    {
        ReportFavoriteId = favoriteId;
        Validate();
        Touch();
    }

    /// <summary>
    /// Sets the predefined KPI type for this entry.
    /// </summary>
    /// <param name="predefined">The predefined KPI type to set, or null to clear it.</param>
    /// <exception cref="ArgumentException">Thrown when the resulting state violates domain invariants (see <see cref="Validate"/>).</exception>
    public void SetPredefined(HomeKpiPredefined? predefined)
    {
        PredefinedType = predefined;
        Validate();
        Touch();
    }

    /// <summary>
    /// Sets a custom title for the KPI. Titles are trimmed and limited to 120 characters.
    /// </summary>
    /// <param name="title">The title to set, or null/whitespace to clear it.</param>
    public void SetTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            Title = null;
        }
        else
        {
            var t = title.Trim();
            if (t.Length > 120) { t = t.Substring(0, 120); }
            Title = t;
        }
        Touch();
    }

    /// <summary>
    /// Validates domain invariants for the current state of the entity.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when required fields for the configured <see cref="Kind"/> are missing or invalid.</exception>
    private void Validate()
    {
        if (Kind == HomeKpiKind.ReportFavorite && ReportFavoriteId == null)
        {
            throw new ArgumentException("ReportFavoriteId required for ReportFavorite KPIs", nameof(ReportFavoriteId));
        }
    }
}
