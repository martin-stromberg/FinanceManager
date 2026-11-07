namespace FinanceManager.Shared.Dtos.SavingsPlans;

/// <summary>
/// DTO summarizing analysis values for a savings plan.
/// </summary>
/// <param name="PlanId">The plan id.</param>
/// <param name="TargetReachable">The target reachable.</param>
/// <param name="TargetAmount">The target amount.</param>
/// <param name="TargetDate">The target date.</param>
/// <param name="AccumulatedAmount">The accumulated amount.</param>
/// <param name="RequiredMonthly">The required monthly.</param>
/// <param name="MonthsRemaining">The months remaining.</param>
/// <param name="ScheduleDeficit">Backlog against the observed contribution rhythm (expected minus received, floored at zero); <see langword="null"/> when no postings exist or no regular rhythm is detectable.</param>
/// <param name="ExpectedToDate">Expected total of regular contributions up to today (observed rate times elapsed contribution periods); <see langword="null"/> when no rhythm is detectable.</param>
/// <param name="ReceivedToDate">Sum of positive, non-reversed and non-reversal postings of the plan; <see langword="null"/> when the plan has no postings.</param>
/// <param name="ObservedMonthlyRate">Median contribution per period normalized to a monthly rate via the plan interval; <see langword="null"/> when no rhythm is detectable.</param>
/// <param name="Disbursements">Sum of extraordinary negative postings (advance payments/withdrawals), reported separately and never counted into the schedule deficit; <see langword="null"/> when the plan has no postings.</param>
/// <returns>The result.</returns>
public sealed record SavingsPlanAnalysisDto(
    Guid PlanId,
    bool TargetReachable,
    decimal? TargetAmount,
    DateTime? TargetDate,
    decimal AccumulatedAmount,
    decimal RequiredMonthly,
    int MonthsRemaining,
    decimal? ScheduleDeficit = null,
    decimal? ExpectedToDate = null,
    decimal? ReceivedToDate = null,
    decimal? ObservedMonthlyRate = null,
    decimal? Disbursements = null
);
