using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// EF Core implementation of <see cref="IBudgetPlanningRepository"/>.
/// </summary>
public sealed class BudgetPlanningRepository : IBudgetPlanningRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">DbContext instance.</param>
    public BudgetPlanningRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetPurposeIdsAsync(Guid ownerUserId, IReadOnlyCollection<Guid>? purposeIds, CancellationToken ct)
    {
        var purposeQuery = _db.BudgetPurposes.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId);
        if (purposeIds != null && purposeIds.Count > 0)
        {
            purposeQuery = purposeQuery.Where(p => purposeIds.Contains(p.Id));
        }

        return await purposeQuery.Select(p => p.Id).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<BudgetRule> Rules, IReadOnlyList<BudgetOverride> Overrides)> GetRulesAndOverridesAsync(Guid ownerUserId, IReadOnlyList<Guid> purposeIds, BudgetPeriodKey @from, BudgetPeriodKey to, CancellationToken ct)
    {
        @from.Validate();
        to.Validate();

        var fromDate = @from.StartDate;
        var toDate = to.EndDate;

        var rules = await _db.BudgetRules.AsNoTracking()
            .Where(r => r.OwnerUserId == ownerUserId)
            .Where(r => purposeIds.Contains(r.BudgetPurposeId))
            .Where(r => r.StartDate <= toDate)
            .Where(r => r.EndDate == null || r.EndDate.Value >= fromDate)
            .ToListAsync(ct);

        var overrides = await _db.BudgetOverrides.AsNoTracking()
            .Where(o => o.OwnerUserId == ownerUserId)
            .Where(o => purposeIds.Contains(o.BudgetPurposeId))
            .Where(o => (o.PeriodYear > @from.Year || (o.PeriodYear == @from.Year && o.PeriodMonth >= @from.Month)))
            .Where(o => (o.PeriodYear < to.Year || (o.PeriodYear == to.Year && o.PeriodMonth <= to.Month)))
            .ToListAsync(ct);

        return (rules, overrides);
    }
}
