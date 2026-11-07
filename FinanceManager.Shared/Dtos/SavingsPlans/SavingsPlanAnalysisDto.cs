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
/// <param name="ExpectedToDate">Expected total of regular contributions since the start of the current financing cycle (dominant class amount times fully elapsed deposit-cadence periods, the current month not due yet); <see langword="null"/> when no rhythm is detectable.</param>
/// <param name="ReceivedToDate">With a detectable rhythm: sum of the dominant amount class's postings within the current financing cycle — micro postings such as interest credits (below ~50 % of the dominant class amount) never enter; without a rhythm: factual sum of positive, non-reversed and non-reversal postings since cycle start (the last due disbursement ≈ target amount, else the last reached target date, else the first deposit); <see langword="null"/> when the plan has no postings.</param>
/// <param name="ObservedMonthlyRate">Amount of the dominant amount class of the cycle's positive postings — the most frequent recurring deposit (ties by larger amount; a most-frequent micro class below ~50 % of the largest class amount loses to a comparably frequent larger class) — normalized to a monthly rate via the observed deposit cadence (median month gap of the dominant class's booking months; the plan interval — the expense's due rhythm — only serves as fallback when fewer than two booking months leave no cadence to derive); <see langword="null"/> when no rhythm is detectable.</param>
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
