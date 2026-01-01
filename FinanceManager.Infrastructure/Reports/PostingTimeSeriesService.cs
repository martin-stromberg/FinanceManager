using FinanceManager.Application.Reports;
using FinanceManager.Domain.Postings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Reports;

/// <summary>
/// Service that provides time series of posting aggregates for reporting purposes.
/// Supports fetching series for a single entity (account/contact/savings plan/security) or aggregated across all entities of a kind.
/// </summary>
public sealed class PostingTimeSeriesService : IPostingTimeSeriesService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostingTimeSeriesService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query posting aggregates and ownership information.</param>
    public PostingTimeSeriesService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Helper to clamp the requested number of points to a sensible default and range based on the period.
    /// </summary>
    /// <param name="period">The aggregate period requested.</param>
    /// <param name="take">Requested number of points; when &lt;= 0 a default value based on the period is used.</param>
    /// <returns>A clamped value between 1 and 200.</returns>
    private static int ClampTake(AggregatePeriod period, int take)
        => Math.Clamp(take <= 0 ? (period == AggregatePeriod.Month ? 36 : period == AggregatePeriod.Quarter ? 16 : period == AggregatePeriod.HalfYear ? 12 : 10) : take, 1, 200);

    /// <summary>
    /// Computes the minimum period start date when restricting series by a maximum number of years back.
    /// </summary>
    /// <param name="maxYearsBack">Optional maximum years back to include in the series. When null no minimum is applied.</param>
    /// <returns>The month-aligned minimum <see cref="DateTime"/>, or <c>null</c> when <paramref name="maxYearsBack"/> is null.</returns>
    private static DateTime? ComputeMinDate(int? maxYearsBack)
    {
        if (!maxYearsBack.HasValue) { return null; }
        var v = Math.Clamp(maxYearsBack.Value, 1, 10);
        var today = DateTime.UtcNow.Date;
        return new DateTime(today.Year - v, today.Month, 1); // month aligned
    }

    /// <summary>
    /// Gets a time series of aggregate points for a single entity (account/contact/savings plan/security).
    /// The service validates ownership and returns <c>null</c> when the entity is not owned by the specified user.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier used to validate access to the requested entity.</param>
    /// <param name="kind">Kind of posting (Bank / Contact / SavingsPlan / Security).</param>
    /// <param name="entityId">Identifier of the entity for which to return the series.</param>
    /// <param name="period">Aggregate period (Month/Quarter/HalfYear/Year) to use for the series.</param>
    /// <param name="take">Number of points to return; when &lt;= 0 a period-specific default is used. The value is clamped to [1,200].</param>
    /// <param name="maxYearsBack">Optional maximum number of years to include; when provided the series will be truncated to start no earlier than this many years ago.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An ordered list of <see cref="AggregatePointDto"/> instances (ascending by period start) or <c>null</c> when the specified entity is not owned by the user.
    /// </returns>
    public async Task<IReadOnlyList<AggregatePointDto>?> GetAsync(
        Guid ownerUserId,
        PostingKind kind,
        Guid entityId,
        AggregatePeriod period,
        int take,
        int? maxYearsBack,
        CancellationToken ct)
    {
        // Validate ownership depending on kind
        bool owned = kind switch
        {
            PostingKind.Bank => await _db.Accounts.AsNoTracking().AnyAsync(a => a.Id == entityId && a.OwnerUserId == ownerUserId, ct),
            PostingKind.Contact => await _db.Contacts.AsNoTracking().AnyAsync(c => c.Id == entityId && c.OwnerUserId == ownerUserId, ct),
            PostingKind.SavingsPlan => await _db.SavingsPlans.AsNoTracking().AnyAsync(p => p.Id == entityId && p.OwnerUserId == ownerUserId, ct),
            PostingKind.Security => await _db.Securities.AsNoTracking().AnyAsync(s => s.Id == entityId && s.OwnerUserId == ownerUserId, ct),
            _ => false
        };
        if (!owned) { return null; }

        take = ClampTake(period, take);
        var minDate = ComputeMinDate(maxYearsBack);

        var q = _db.PostingAggregates.AsNoTracking().Where(pa => pa.Kind == kind && pa.Period == period);
        if (minDate.HasValue)
        {
            q = q.Where(a => a.PeriodStart >= minDate.Value);
        }
        q = kind switch
        {
            PostingKind.Bank => q.Where(a => a.AccountId == entityId),
            PostingKind.Contact => q.Where(a => a.ContactId == entityId),
            PostingKind.SavingsPlan => q.Where(a => a.SavingsPlanId == entityId),
            PostingKind.Security => q.Where(a => a.SecurityId == entityId),
            _ => q.Where(_ => false)
        };

        var latest = await q.OrderByDescending(a => a.PeriodStart).Take(take).ToListAsync(ct);
        return latest.OrderBy(a => a.PeriodStart).Select(a => new AggregatePointDto(a.PeriodStart, a.Amount)).ToList();
    }

    /// <summary>
    /// Gets an aggregated time series across all entities of the given kind that are owned by the user.
    /// The method groups aggregated rows by period start and sums amounts across entities.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier to scope which entities are considered.</param>
    /// <param name="kind">Kind of postings to aggregate across.</param>
    /// <param name="period">Aggregate period to use for the series.</param>
    /// <param name="take">Number of points to return; when &lt;= 0 a period-specific default is used. The value is clamped to [1,200].</param>
    /// <param name="maxYearsBack">Optional maximum number of years to include; when provided the series will be truncated to start no earlier than this many years ago.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An ordered list of <see cref="AggregatePointDto"/> representing the summed series across all owned entities of the requested kind.</returns>
    public async Task<IReadOnlyList<AggregatePointDto>> GetAllAsync(
        Guid ownerUserId,
        PostingKind kind,
        AggregatePeriod period,
        int take,
        int? maxYearsBack,
        CancellationToken ct)
    {
        take = ClampTake(period, take);
        var minDate = ComputeMinDate(maxYearsBack);

        // Filter aggregates for owned entities of the given kind
        var aggregates = _db.PostingAggregates.AsNoTracking().Where(a => a.Kind == kind && a.Period == period);
        if (minDate.HasValue)
        {
            aggregates = aggregates.Where(a => a.PeriodStart >= minDate.Value);
        }
        aggregates = kind switch
        {
            PostingKind.Bank => aggregates.Where(a => _db.Accounts.AsNoTracking().Any(ac => ac.Id == a.AccountId && ac.OwnerUserId == ownerUserId)),
            PostingKind.Contact => aggregates.Where(a => _db.Contacts.AsNoTracking().Any(c => c.Id == a.ContactId && c.OwnerUserId == ownerUserId)),
            PostingKind.SavingsPlan => aggregates.Where(a => _db.SavingsPlans.AsNoTracking().Any(s => s.Id == a.SavingsPlanId && s.OwnerUserId == ownerUserId)),
            PostingKind.Security => aggregates.Where(a => _db.Securities.AsNoTracking().Any(s => s.Id == a.SecurityId && s.OwnerUserId == ownerUserId)),
            _ => aggregates.Where(_ => false)
        };

        // Aggregate sums across all entities per period
        var latestDesc = await aggregates
            .GroupBy(a => a.PeriodStart)
            .Select(g => new { PeriodStart = g.Key, Amount = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.PeriodStart)
            .Take(take)
            .ToListAsync(ct);

        return latestDesc
            .OrderBy(x => x.PeriodStart)
            .Select(x => new AggregatePointDto(x.PeriodStart, x.Amount))
            .ToList();
    }
}
