using FinanceManager.Application.Reports;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Service responsible for managing Home KPI records for users.
/// Provides listing, creation, update and deletion operations and maps domain entities to DTOs.
/// </summary>
public sealed class HomeKpiService : IHomeKpiService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeKpiService"/> class.
    /// </summary>
    /// <param name="db">Database context used to persist and query HomeKpi entities.</param>
    public HomeKpiService(AppDbContext db) => _db = db;

    /// <summary>
    /// Maps a domain <see cref="FinanceManager.Domain.Reports.HomeKpi"/> entity to a <see cref="HomeKpiDto"/>.
    /// </summary>
    /// <param name="e">The domain entity to map.</param>
    /// <param name="favName">Optional name of the referenced favorite report (may be null).</param>
    /// <returns>A <see cref="HomeKpiDto"/> representing the entity.</returns>
    private static HomeKpiDto Map(FinanceManager.Domain.Reports.HomeKpi e, string? favName)
        => new(
            e.Id,
            e.Kind,
            e.ReportFavoriteId,
            favName,
            e.Title,
            e.PredefinedType,
            e.DisplayMode,
            e.SortOrder,
            e.CreatedUtc,
            e.ModifiedUtc);

    /// <summary>
    /// Returns all Home KPI entries for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="HomeKpiDto"/> instances owned by the user.</returns>
    public async Task<IReadOnlyList<HomeKpiDto>> ListAsync(Guid ownerUserId, CancellationToken ct)
    {
        var data = await _db.HomeKpis.AsNoTracking()
            .Where(k => k.OwnerUserId == ownerUserId)
            .OrderBy(k => k.SortOrder).ThenBy(k => k.CreatedUtc)
            .Select(k => new { Kpi = k, FavName = k.ReportFavoriteId == null ? null : _db.ReportFavorites.Where(f => f.Id == k.ReportFavoriteId).Select(f => f.Name).FirstOrDefault() })
            .ToListAsync(ct);
        return data.Select(x => Map(x.Kpi, x.FavName)).ToList();
    }

    /// <summary>
    /// Creates a new Home KPI for the specified owner with the provided request data.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="request">Creation request containing KPI details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="HomeKpiDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when required fields for the selected kind are missing (e.g. ReportFavoriteId for ReportFavorite kind).</exception>
    /// <exception cref="InvalidOperationException">Thrown when a referenced ReportFavorite does not exist or is not owned by the user.</exception>
    public async Task<HomeKpiDto> CreateAsync(Guid ownerUserId, HomeKpiCreateRequest request, CancellationToken ct)
    {
        if (request.Kind == HomeKpiKind.ReportFavorite && request.ReportFavoriteId == null)
        {
            throw new ArgumentException("ReportFavoriteId required for kind ReportFavorite", nameof(request.ReportFavoriteId));
        }
        if (request.ReportFavoriteId.HasValue)
        {
            var owned = await _db.ReportFavorites.AsNoTracking().AnyAsync(f => f.Id == request.ReportFavoriteId.Value && f.OwnerUserId == ownerUserId, ct);
            if (!owned) { throw new InvalidOperationException("Favorite not found or not owned"); }
        }
        var entity = new FinanceManager.Domain.Reports.HomeKpi(ownerUserId, request.Kind, request.DisplayMode, request.SortOrder, request.ReportFavoriteId);
        // For predefined KPIs, ensure a default when omitted (back-compat): cycle by sort index
        if (request.Kind == HomeKpiKind.Predefined && request.PredefinedType == null)
        {
            var fallback = (HomeKpiPredefined)(request.SortOrder % Enum.GetValues<HomeKpiPredefined>().Length);
            entity.SetPredefined(fallback);
        }
        else
        {
            entity.SetPredefined(request.PredefinedType);
        }
        entity.SetTitle(request.Title);
        _db.HomeKpis.Add(entity);
        await _db.SaveChangesAsync(ct);
        var favName = request.ReportFavoriteId == null ? null : await _db.ReportFavorites.AsNoTracking().Where(f => f.Id == request.ReportFavoriteId).Select(f => f.Name).FirstOrDefaultAsync(ct);
        return Map(entity, favName);
    }

    /// <summary>
    /// Updates an existing Home KPI identified by <paramref name="id"/> for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the Home KPI to update.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="request">Update request containing new KPI values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="HomeKpiDto"/>, or <c>null</c> when the KPI was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when required fields for the selected kind are missing (e.g. ReportFavoriteId for ReportFavorite kind).</exception>
    /// <exception cref="InvalidOperationException">Thrown when a referenced ReportFavorite does not exist or is not owned by the user.</exception>
    public async Task<HomeKpiDto?> UpdateAsync(Guid id, Guid ownerUserId, HomeKpiUpdateRequest request, CancellationToken ct)
    {
        var entity = await _db.HomeKpis.FirstOrDefaultAsync(k => k.Id == id && k.OwnerUserId == ownerUserId, ct);
        if (entity == null) { return null; }
        if (request.Kind == HomeKpiKind.ReportFavorite && request.ReportFavoriteId == null)
        {
            throw new ArgumentException("ReportFavoriteId required for kind ReportFavorite", nameof(request.ReportFavoriteId));
        }
        if (request.ReportFavoriteId.HasValue)
        {
            var owned = await _db.ReportFavorites.AsNoTracking().AnyAsync(f => f.Id == request.ReportFavoriteId.Value && f.OwnerUserId == ownerUserId, ct);
            if (!owned) { throw new InvalidOperationException("Favorite not found or not owned"); }
        }
        entity.SetFavorite(request.ReportFavoriteId);
        entity.SetTitle(request.Title ?? entity.Title);
        entity.SetPredefined(request.PredefinedType ?? entity.PredefinedType);
        entity.SetDisplayMode(request.DisplayMode);
        entity.SetSortOrder(request.SortOrder);
        await _db.SaveChangesAsync(ct);
        var favName = request.ReportFavoriteId == null ? null : await _db.ReportFavorites.AsNoTracking().Where(f => f.Id == request.ReportFavoriteId).Select(f => f.Name).FirstOrDefaultAsync(ct);
        return Map(entity, favName);
    }

    /// <summary>
    /// Deletes the Home KPI with the specified <paramref name="id"/> for the owner.
    /// </summary>
    /// <param name="id">Identifier of the Home KPI to delete.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when deletion succeeded; otherwise <c>false</c> when not found.</returns>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.HomeKpis.FirstOrDefaultAsync(k => k.Id == id && k.OwnerUserId == ownerUserId, ct);
        if (entity == null) { return false; }
        _db.HomeKpis.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
