using FinanceManager.Application.Reports;
using FinanceManager.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Service managing user report favorites (CRUD and listing).
/// Translates persisted <see cref="ReportFavorite"/> entities to DTOs and applies validation/ownership checks.
/// </summary>
public sealed class ReportFavoriteService : IReportFavoriteService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportFavoriteService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to persist and query favorites.</param>
    public ReportFavoriteService(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns the effective posting kinds expressed by the provided <see cref="ReportFavorite"/> entity.
    /// </summary>
    /// <param name="entity">The favorite entity to inspect.</param>
    /// <returns>A collection of <see cref="PostingKind"/> values that the favorite represents.</returns>
    private static IReadOnlyCollection<PostingKind> EffectiveKinds(ReportFavorite entity)
        => entity.GetPostingKinds();

    /// <summary>
    /// Parses a CSV string of integer posting-kind values into a distinct collection of <see cref="PostingKind"/>.
    /// Falls back to the supplied <paramref name="fallback"/> when the input is null/empty or parsing yields no values.
    /// </summary>
    /// <param name="csv">Comma-separated integers representing <see cref="PostingKind"/> members.</param>
    /// <param name="fallback">Fallback PostingKind used when parsing yields no results.</param>
    /// <returns>A distinct array of parsed <see cref="PostingKind"/> values or an array containing the fallback.</returns>
    private static IReadOnlyCollection<PostingKind> ParseKinds(string? csv, PostingKind fallback)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return new PostingKind[] { fallback };
        }
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<PostingKind>(parts.Length);
        foreach (var p in parts)
        {
            if (int.TryParse(p, out var v))
            {
                list.Add((PostingKind)v);
            }
        }
        return list.Count == 0 ? new[] { fallback } : list.Distinct().ToArray();
    }

    /// <summary>
    /// Converts stored entity filter values into a DTO representation used by API consumers.
    /// </summary>
    /// <param name="e">The persistent <see cref="ReportFavorite"/> entity.</param>
    /// <returns>A <see cref="ReportFavoriteFiltersDto"/> instance or <c>null</c> if no filters are set.</returns>
    private static ReportFavoriteFiltersDto? ToDtoFilters(ReportFavorite e)
    {
        var (acc, con, sp, sec, ccat, scat, secat, secTypes, includeDiv) = e.GetFilters();
        if (acc == null && con == null && sp == null && sec == null && ccat == null && scat == null && secat == null && secTypes == null && includeDiv != true)
        {
            return null;
        }
        return new ReportFavoriteFiltersDto(acc, con, sp, sec, ccat, scat, secat, secTypes, includeDiv);
    }

    /// <summary>
    /// Applies DTO filters onto the persistent <see cref="ReportFavorite"/> entity by calling its SetFilters method.
    /// Passing <c>null</c> clears any existing filters on the entity.
    /// </summary>
    /// <param name="e">The entity to modify.</param>
    /// <param name="f">The DTO filters to apply, or <c>null</c> to clear filters.</param>
    private static void ApplyFilters(ReportFavorite e, ReportFavoriteFiltersDto? f)
    {
        if (f == null)
        {
            e.SetFilters(null, null, null, null, null, null, null, null, null);
            return;
        }
        e.SetFilters(f.AccountIds, f.ContactIds, f.SavingsPlanIds, f.SecurityIds, f.ContactCategoryIds, f.SavingsPlanCategoryIds, f.SecurityCategoryIds, f.SecuritySubTypes, f.IncludeDividendRelated);
    }

    /// <summary>
    /// Lists report favorites for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier to filter favorites.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="ReportFavoriteDto"/> instances for the owner (may be empty).</returns>
    public async Task<IReadOnlyList<ReportFavoriteDto>> ListAsync(Guid ownerUserId, CancellationToken ct)
    {
        var raw = await _db.ReportFavorites.AsNoTracking()
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.PostingKind,
                r.IncludeCategory,
                r.Interval,
                r.Take,
                r.ComparePrevious,
                r.CompareYear,
                r.ShowChart,
                r.Expandable,
                r.CreatedUtc,
                r.ModifiedUtc,
                r.PostingKindsCsv,
                r.AccountIdsCsv,
                r.ContactIdsCsv,
                r.SavingsPlanIdsCsv,
                r.SecurityIdsCsv,
                r.ContactCategoryIdsCsv,
                r.SavingsPlanCategoryIdsCsv,
                r.SecurityCategoryIdsCsv,
                r.SecuritySubTypesCsv,
                r.IncludeDividendRelated,
                r.UseValutaDate
            })
            .ToListAsync(ct);

        return raw.Select(r =>
        {
            var entity = new ReportFavorite(ownerUserId, r.Name, r.PostingKind, r.IncludeCategory, r.Interval, r.ComparePrevious, r.CompareYear, r.ShowChart, r.Expandable, r.Take);
            if (!string.IsNullOrWhiteSpace(r.PostingKindsCsv)) { entity.SetPostingKinds(ParseKinds(r.PostingKindsCsv, r.PostingKind)); }
            entity.SetFilters(ParseCsv(r.AccountIdsCsv), ParseCsv(r.ContactIdsCsv), ParseCsv(r.SavingsPlanIdsCsv), ParseCsv(r.SecurityIdsCsv), ParseCsv(r.ContactCategoryIdsCsv), ParseCsv(r.SavingsPlanCategoryIdsCsv), ParseCsv(r.SecurityCategoryIdsCsv), ParseCsvInt(r.SecuritySubTypesCsv), r.IncludeDividendRelated);
            // apply persisted UseValutaDate onto entity state for DTO creation
            if (r.UseValutaDate) { entity.Update(entity.PostingKind, entity.IncludeCategory, entity.Interval, entity.ComparePrevious, entity.CompareYear, entity.ShowChart, entity.Expandable, entity.Take, r.UseValutaDate); }
            return new ReportFavoriteDto(
                r.Id,
                r.Name,
                r.PostingKind,
                r.IncludeCategory,
                r.Interval,
                entity.Take,
                r.ComparePrevious,
                r.CompareYear,
                r.ShowChart,
                r.Expandable,
                r.CreatedUtc,
                r.ModifiedUtc,
                ParseKinds(r.PostingKindsCsv, r.PostingKind),
                ToDtoFilters(entity)
                , r.UseValutaDate
            );
        }).ToList();
    }

    /// <summary>
    /// Gets a single report favorite by id for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the favorite.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="ReportFavoriteDto"/> when found; otherwise <c>null</c>.</returns>
    public async Task<ReportFavoriteDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var r = await _db.ReportFavorites.AsNoTracking()
            .Where(r => r.Id == id && r.OwnerUserId == ownerUserId)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.PostingKind,
                r.IncludeCategory,
                r.Interval,
                r.Take,
                r.ComparePrevious,
                r.CompareYear,
                r.ShowChart,
                r.Expandable,
                r.CreatedUtc,
                r.ModifiedUtc,
                r.PostingKindsCsv,
                r.AccountIdsCsv,
                r.ContactIdsCsv,
                r.SavingsPlanIdsCsv,
                r.SecurityIdsCsv,
                r.ContactCategoryIdsCsv,
                r.SavingsPlanCategoryIdsCsv,
                r.SecurityCategoryIdsCsv,
                r.SecuritySubTypesCsv,
                r.IncludeDividendRelated,
                r.UseValutaDate
            })
            .FirstOrDefaultAsync(ct);
        if (r == null) { return null; }
        var entity = new ReportFavorite(ownerUserId, r.Name, r.PostingKind, r.IncludeCategory, r.Interval, r.ComparePrevious, r.CompareYear, r.ShowChart, r.Expandable, r.Take);
        if (!string.IsNullOrWhiteSpace(r.PostingKindsCsv)) { entity.SetPostingKinds(ParseKinds(r.PostingKindsCsv, r.PostingKind)); }
        entity.SetFilters(ParseCsv(r.AccountIdsCsv), ParseCsv(r.ContactIdsCsv), ParseCsv(r.SavingsPlanIdsCsv), ParseCsv(r.SecurityIdsCsv), ParseCsv(r.ContactCategoryIdsCsv), ParseCsv(r.SavingsPlanCategoryIdsCsv), ParseCsv(r.SecurityCategoryIdsCsv), ParseCsvInt(r.SecuritySubTypesCsv), r.IncludeDividendRelated);
        if (r.UseValutaDate) { entity.Update(entity.PostingKind, entity.IncludeCategory, entity.Interval, entity.ComparePrevious, entity.CompareYear, entity.ShowChart, entity.Expandable, entity.Take, r.UseValutaDate); }
        return new ReportFavoriteDto(
            r.Id,
            r.Name,
            r.PostingKind,
            r.IncludeCategory,
            r.Interval,
            entity.Take,
            r.ComparePrevious,
            r.CompareYear,
            r.ShowChart,
            r.Expandable,
            r.CreatedUtc,
            r.ModifiedUtc,
            ParseKinds(r.PostingKindsCsv, r.PostingKind),
            ToDtoFilters(entity)
            , r.UseValutaDate
        );
    }

    /// <summary>
    /// Creates a new report favorite for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="request">Creation request containing favorite settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="ReportFavoriteDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are invalid (e.g. empty name).</exception>
    /// <exception cref="InvalidOperationException">Thrown when a favorite with the same name already exists for the owner.</exception>
    public async Task<ReportFavoriteDto> CreateAsync(Guid ownerUserId, ReportFavoriteCreateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name required", nameof(request.Name));
        }
        var name = request.Name.Trim();

        var exists = await _db.ReportFavorites.AsNoTracking().AnyAsync(r => r.OwnerUserId == ownerUserId && r.Name == name, ct);
        if (exists)
        {
            throw new InvalidOperationException("Duplicate favorite name");
        }

        var entity = new ReportFavorite(ownerUserId, name, request.PostingKind, request.IncludeCategory, request.Interval, request.ComparePrevious, request.CompareYear, request.ShowChart, request.Expandable, request.Take);
        if (request.PostingKinds is { Count: > 0 })
        {
            entity.SetPostingKinds(request.PostingKinds);
        }
        ApplyFilters(entity, request.Filters);
        // persist UseValutaDate on entity state and touch
        entity.Update(entity.PostingKind, entity.IncludeCategory, entity.Interval, entity.ComparePrevious, entity.CompareYear, entity.ShowChart, entity.Expandable, entity.Take, request.UseValutaDate);
        _db.ReportFavorites.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new ReportFavoriteDto(entity.Id, entity.Name, entity.PostingKind, entity.IncludeCategory, entity.Interval, entity.Take, entity.ComparePrevious, entity.CompareYear, entity.ShowChart, entity.Expandable, entity.CreatedUtc, entity.ModifiedUtc, EffectiveKinds(entity), ToDtoFilters(entity), entity.UseValutaDate);
    }

    /// <summary>
    /// Updates an existing report favorite. Validates ownership and duplicate names.
    /// </summary>
    /// <param name="id">Identifier of the favorite to update.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="request">Update request containing new settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="ReportFavoriteDto"/>, or <c>null</c> when the entity was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are invalid (e.g. empty name).</exception>
    /// <exception cref="InvalidOperationException">Thrown when a favorite with the same name already exists for the owner.</exception>
    public async Task<ReportFavoriteDto?> UpdateAsync(Guid id, Guid ownerUserId, ReportFavoriteUpdateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name required", nameof(request.Name));
        }
        var entity = await _db.ReportFavorites.FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return null;
        }

        var name = request.Name.Trim();
        var duplicate = await _db.ReportFavorites.AsNoTracking().AnyAsync(r => r.OwnerUserId == ownerUserId && r.Name == name && r.Id != id, ct);
        if (duplicate)
        {
            throw new InvalidOperationException("Duplicate favorite name");
        }

        entity.Rename(name);
        entity.Update(request.PostingKind, request.IncludeCategory, request.Interval, request.ComparePrevious, request.CompareYear, request.ShowChart, request.Expandable, request.Take, request.UseValutaDate);
        if (request.PostingKinds is { Count: > 0 })
        {
            entity.SetPostingKinds(request.PostingKinds);
        }
        else
        {
            entity.SetPostingKinds(new[] { request.PostingKind });
        }
        ApplyFilters(entity, request.Filters);
        await _db.SaveChangesAsync(ct);
        return new ReportFavoriteDto(entity.Id, entity.Name, entity.PostingKind, entity.IncludeCategory, entity.Interval, entity.Take, entity.ComparePrevious, entity.CompareYear, entity.ShowChart, entity.Expandable, entity.CreatedUtc, entity.ModifiedUtc, EffectiveKinds(entity), ToDtoFilters(entity), entity.UseValutaDate);
    }

    /// <summary>
    /// Deletes a report favorite if it exists and belongs to the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the favorite to delete.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the favorite existed and was removed; otherwise <c>false</c>.</returns>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.ReportFavorites.FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return false;
        }
        _db.ReportFavorites.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Parses a CSV string of GUIDs into a collection of <see cref="Guid"/> values or returns <c>null</c> when input is empty.
    /// </summary>
    /// <param name="csv">Comma-separated GUIDs.</param>
    /// <returns>A collection of parsed GUIDs or <c>null</c> when input is null/empty.</returns>
    private static IReadOnlyCollection<Guid>? ParseCsv(string? csv)
        => string.IsNullOrWhiteSpace(csv) ? null : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Guid.Parse).ToArray();

    /// <summary>
    /// Parses a CSV string of integers into a collection of <see cref="int"/> values or returns <c>null</c> when input is empty.
    /// </summary>
    /// <param name="csv">Comma-separated integers.</param>
    /// <returns>A collection of parsed integers or <c>null</c> when input is null/empty.</returns>
    private static IReadOnlyCollection<int>? ParseCsvInt(string? csv)
        => string.IsNullOrWhiteSpace(csv) ? null : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
}
