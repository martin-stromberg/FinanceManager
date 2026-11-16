using FinanceManager.Application.Reports;
using FinanceManager.Domain.Reports;
using FinanceManager.Shared.Dtos.Reports;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Service managing per-favorite suppressed projection expectations. All operations are
/// scoped to the owning user and verify that the referenced report favorite belongs to the
/// same owner.
/// </summary>
public sealed class ReportSuppressedExpectationService : IReportSuppressedExpectationService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportSuppressedExpectationService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to persist and query suppressions.</param>
    public ReportSuppressedExpectationService(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportSuppressedExpectationDto>> ListAsync(Guid ownerUserId, Guid reportFavoriteId, CancellationToken ct)
    {
        if (!await OwnsFavoriteAsync(ownerUserId, reportFavoriteId, ct))
        {
            return Array.Empty<ReportSuppressedExpectationDto>();
        }
        var rows = await _db.ReportSuppressedExpectations.AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId && s.ReportFavoriteId == reportFavoriteId)
            .OrderBy(s => s.PriorYearDate)
            .ToListAsync(ct);
        var names = await ResolveEntityNamesAsync(ownerUserId, rows.Select(r => (r.Kind, r.EntityId)), ct);
        return rows
            .Select(r => ToDto(r, ResolveName(names, r.Kind, r.EntityId)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ReportSuppressedExpectationDto?> SuppressAsync(Guid ownerUserId, Guid reportFavoriteId, ReportSuppressedExpectationRequest request, CancellationToken ct)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!await OwnsFavoriteAsync(ownerUserId, reportFavoriteId, ct))
        {
            return null;
        }
        var priorYearDate = request.PriorYearDate.Date;
        var existing = await _db.ReportSuppressedExpectations
            .FirstOrDefaultAsync(s =>
                s.OwnerUserId == ownerUserId
                && s.ReportFavoriteId == reportFavoriteId
                && s.Kind == request.Kind
                && s.EntityId == request.EntityId
                && s.PriorYearDate == priorYearDate, ct);
        var entity = existing ?? new ReportSuppressedExpectation(ownerUserId, reportFavoriteId, request.Kind, request.EntityId, priorYearDate);
        if (existing is null)
        {
            try
            {
                _db.ReportSuppressedExpectations.Add(entity);
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Unique-index race: another request inserted the same suppression first.
                _db.Entry(entity).State = EntityState.Detached;
                entity = await _db.ReportSuppressedExpectations
                    .FirstAsync(s =>
                        s.OwnerUserId == ownerUserId
                        && s.ReportFavoriteId == reportFavoriteId
                        && s.Kind == request.Kind
                        && s.EntityId == request.EntityId
                        && s.PriorYearDate == priorYearDate, ct);
            }
        }
        var names = await ResolveEntityNamesAsync(ownerUserId, new[] { (entity.Kind, entity.EntityId) }, ct);
        return ToDto(entity, ResolveName(names, entity.Kind, entity.EntityId));
    }

    /// <inheritdoc />
    public async Task<bool> UnsuppressAsync(Guid ownerUserId, Guid reportFavoriteId, Guid suppressionId, CancellationToken ct)
    {
        if (reportFavoriteId == Guid.Empty) throw new ArgumentException("Value cannot be empty GUID.", nameof(reportFavoriteId));
        if (suppressionId == Guid.Empty) throw new ArgumentException("Value cannot be empty GUID.", nameof(suppressionId));
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Value cannot be empty GUID.", nameof(ownerUserId));
        var removed = await _db.ReportSuppressedExpectations
            .Where(s => s.Id == suppressionId && s.ReportFavoriteId == reportFavoriteId && s.OwnerUserId == ownerUserId)
            .ExecuteDeleteAsync(ct);
        return removed > 0;
    }

    private async Task<bool> OwnsFavoriteAsync(Guid ownerUserId, Guid reportFavoriteId, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("Value cannot be empty GUID.", nameof(ownerUserId));
        if (reportFavoriteId == Guid.Empty) throw new ArgumentException("Value cannot be empty GUID.", nameof(reportFavoriteId));
        return await _db.ReportFavorites.AsNoTracking()
            .AnyAsync(f => f.Id == reportFavoriteId && f.OwnerUserId == ownerUserId, ct);
    }

    private async Task<Dictionary<(PostingKind, Guid), string>> ResolveEntityNamesAsync(Guid ownerUserId, IEnumerable<(PostingKind Kind, Guid EntityId)> items, CancellationToken ct)
    {
        var result = new Dictionary<(PostingKind, Guid), string>();
        var byKind = items.GroupBy(i => i.Kind);
        foreach (var group in byKind)
        {
            var ids = group.Select(i => i.EntityId).Distinct().ToList();
            Dictionary<Guid, string> names = group.Key switch
            {
                PostingKind.Bank => await _db.Accounts.AsNoTracking()
                    .Where(a => a.OwnerUserId == ownerUserId && ids.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, a => a.Name, ct),
                PostingKind.Contact => await _db.Contacts.AsNoTracking()
                    .Where(c => c.OwnerUserId == ownerUserId && ids.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name, ct),
                PostingKind.SavingsPlan => await _db.SavingsPlans.AsNoTracking()
                    .Where(s => s.OwnerUserId == ownerUserId && ids.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name, ct),
                PostingKind.Security => await _db.Securities.AsNoTracking()
                    .Where(s => s.OwnerUserId == ownerUserId && ids.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name, ct),
                _ => new Dictionary<Guid, string>()
            };
            foreach (var kv in names)
            {
                result[(group.Key, kv.Key)] = kv.Value;
            }
        }
        return result;
    }

    private static string ResolveName(IReadOnlyDictionary<(PostingKind, Guid), string> names, PostingKind kind, Guid entityId)
        => names.TryGetValue((kind, entityId), out var name) ? name : entityId.ToString("N")[..6];

    private static ReportSuppressedExpectationDto ToDto(ReportSuppressedExpectation entity, string entityName)
        => new(entity.Id, entity.Kind, entity.EntityId, entityName, entity.PriorYearDate.AddYears(1), entity.PriorYearDate, entity.SuppressedAtUtc);
}
