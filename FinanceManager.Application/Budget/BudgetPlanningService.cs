using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Calculates planned values based on persisted rules and overrides.
/// </summary>
public sealed class BudgetPlanningService : IBudgetPlanningService
{
    private readonly ILogger<BudgetPlanningService> _logger;
    private readonly IBudgetPlanningRepository _repo;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="repo">Repository used to load rules and overrides.</param>
    public BudgetPlanningService(ILogger<BudgetPlanningService> logger, IBudgetPlanningRepository repo)
    {
        _logger = logger;
        _repo = repo;
    }

    /// <inheritdoc />
    public async Task<BudgetPlannedValuesResult> CalculatePlannedValuesAsync(Guid ownerUserId, IReadOnlyCollection<Guid>? purposeIds, BudgetPeriodKey from, BudgetPeriodKey to, CancellationToken ct)
    {
        _logger.LogInformation("Calculating planned values for {OwnerUserId} from {From} to {To}", ownerUserId, from, to);

        from.Validate();
        to.Validate();

        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("OwnerUserId must not be empty", nameof(ownerUserId));
        }

        var purposeIdList = await _repo.GetPurposeIdsAsync(ownerUserId, purposeIds, ct);
        if (purposeIdList.Count == 0)
        {
            return new BudgetPlannedValuesResult(from, to, Array.Empty<BudgetPlannedValue>());
        }

        var (rules, overrides) = await _repo.GetRulesAndOverridesAsync(ownerUserId, purposeIdList, from, to, ct);

        var planned = new Dictionary<(Guid PurposeId, BudgetPeriodKey Period), decimal>(capacity: purposeIdList.Count * 8);

        foreach (var rule in rules)
        {
            foreach (var period in BudgetRuleScheduler.GetDuePeriods(rule, from, to))
            {
                var key = (PurposeId: rule.BudgetPurposeId, Period: period);
                planned.TryGetValue(key, out var cur);
                planned[key] = cur + rule.Amount;
            }
        }

        foreach (var ov in overrides)
        {
            var key = (PurposeId: ov.BudgetPurposeId, Period: ov.Period);
            planned[key] = ov.Amount;
        }

        var values = new List<BudgetPlannedValue>(purposeIdList.Count * 12);
        foreach (var pid in purposeIdList)
        {
            foreach (var period in BudgetPeriodRange.Enumerate(from, to))
            {
                planned.TryGetValue((PurposeId: pid, Period: period), out var amount);
                values.Add(new BudgetPlannedValue(pid, period, amount));
            }
        }

        return new BudgetPlannedValuesResult(from, to, values);
    }
}
