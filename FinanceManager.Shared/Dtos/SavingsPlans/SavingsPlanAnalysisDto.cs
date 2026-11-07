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
/// <param name="ScheduleDeficit">Backlog against the observed contribution rhythm within the current financing cycle only (expected minus received since the last due disbursement/reached target date, floored at zero); <see langword="null"/> when no postings exist, no regular rhythm is detectable, or the plan revolves (in-cycle disbursements balance the deposits — cyclically balanced, not missing).</param>
/// <param name="ExpectedToDate">Expected total of regular contributions since the start of the current financing cycle (observed rate times elapsed contribution periods); <see langword="null"/> when no rhythm is detectable.</param>
/// <param name="ReceivedToDate">Sum of positive, non-reversed and non-reversal postings since the start of the current financing cycle (the last due disbursement ≈ target amount, else the last reached target date, else the first deposit); <see langword="null"/> when the plan has no postings.</param>
/// <param name="ObservedMonthlyRate">Median contribution per period inside the current cycle normalized to a monthly rate via the plan interval; <see langword="null"/> when no rhythm is detectable.</param>
/// <param name="Disbursements">Sum of all negative effective postings (advance payments/withdrawals) across the whole history — including the disbursement that opened the current cycle; reported separately and never counted into the schedule deficit; <see langword="null"/> when the plan has no postings.</param>
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
