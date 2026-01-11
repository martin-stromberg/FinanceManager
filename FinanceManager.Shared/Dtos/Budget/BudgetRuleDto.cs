namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Data transfer object for budget rules.
/// </summary>
/// <param name="Id">Rule id.</param>
/// <param name="OwnerUserId">Owner user id.</param>
/// <param name="BudgetPurposeId">Purpose id.</param>
/// <param name="Amount">Expected amount.</param>
/// <param name="Interval">Interval type.</param>
/// <param name="CustomIntervalMonths">Custom interval months when interval is CustomMonths.</param>
/// <param name="StartDate">Start date (inclusive).</param>
/// <param name="EndDate">Optional end date (inclusive).</param>
public sealed record BudgetRuleDto(Guid Id, Guid OwnerUserId, Guid BudgetPurposeId, decimal Amount, BudgetIntervalType Interval, int? CustomIntervalMonths, DateOnly StartDate, DateOnly? EndDate);
