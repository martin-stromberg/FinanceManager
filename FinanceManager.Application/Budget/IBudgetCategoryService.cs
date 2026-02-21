using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Service for managing budget categories.
/// </summary>
public interface IBudgetCategoryService
{
    /// <summary>
    /// Lists budget categories for the owner.
    /// </summary>
    Task<IReadOnlyList<BudgetCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Gets a budget category by id for the owner.
    /// </summary>
    Task<BudgetCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new budget category.
    /// </summary>
    Task<BudgetCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Updates an existing budget category.
    /// </summary>
    Task<BudgetCategoryDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct);

    /// <summary>
    /// Deletes a budget category.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Lists budget categories for the owner including calculated overview fields.
    /// When <paramref name="from"/> and <paramref name="to"/> are provided, budget and actual values are calculated for that range.
    /// </summary>
    Task<IReadOnlyList<BudgetCategoryOverviewDto>> ListOverviewAsync(Guid ownerUserId, DateOnly? from, DateOnly? to, CancellationToken ct);
}
