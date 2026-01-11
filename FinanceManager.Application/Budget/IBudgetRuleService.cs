using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Service for managing budget rules.
/// </summary>
public interface IBudgetRuleService
{
    /// <summary>
    /// Creates a new rule for a purpose.
    /// </summary>
    Task<BudgetRuleDto> CreateAsync(Guid ownerUserId, Guid budgetPurposeId, decimal amount, BudgetIntervalType interval, int? customIntervalMonths, DateOnly startDate, DateOnly? endDate, CancellationToken ct);

    /// <summary>
    /// Updates an existing rule.
    /// </summary>
    Task<BudgetRuleDto?> UpdateAsync(Guid id, Guid ownerUserId, decimal amount, BudgetIntervalType interval, int? customIntervalMonths, DateOnly startDate, DateOnly? endDate, CancellationToken ct);

    /// <summary>
    /// Deletes an existing rule.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Gets a rule by id.
    /// </summary>
    Task<BudgetRuleDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Lists rules for a purpose.
    /// </summary>
    Task<IReadOnlyList<BudgetRuleDto>> ListByPurposeAsync(Guid ownerUserId, Guid budgetPurposeId, CancellationToken ct);
}
