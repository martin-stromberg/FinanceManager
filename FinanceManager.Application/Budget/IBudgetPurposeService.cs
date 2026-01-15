using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Service for managing budget purposes.
/// </summary>
public interface IBudgetPurposeService
{
    /// <summary>
    /// Creates a new budget purpose.
    /// </summary>
    Task<BudgetPurposeDto> CreateAsync(Guid ownerUserId, string name, BudgetSourceType sourceType, Guid sourceId, string? description, Guid? budgetCategoryId, CancellationToken ct);

    /// <summary>
    /// Updates an existing budget purpose.
    /// </summary>
    Task<BudgetPurposeDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, BudgetSourceType sourceType, Guid sourceId, string? description, Guid? budgetCategoryId, CancellationToken ct);

    /// <summary>
    /// Deletes an existing budget purpose.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Gets a budget purpose by id.
    /// </summary>
    Task<BudgetPurposeDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Lists budget purposes.
    /// </summary>
    Task<IReadOnlyList<BudgetPurposeDto>> ListAsync(Guid ownerUserId, int skip, int take, BudgetSourceType? sourceType, string? nameFilter, CancellationToken ct);

    /// <summary>
    /// Returns the total number of purposes for the owner.
    /// </summary>
    Task<int> CountAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Lists budget purposes including rule count and computed budget sum for the provided period.
    /// When <paramref name="from"/> or <paramref name="to"/> are <c>null</c>, the service may apply a default period.
    /// </summary>
    Task<IReadOnlyList<BudgetPurposeOverviewDto>> ListOverviewAsync(
        Guid ownerUserId,
        int skip,
        int take,
        BudgetSourceType? sourceType,
        string? nameFilter,
        DateOnly? from,
        DateOnly? to,
        Guid? budgetCategoryId,
        CancellationToken ct);
}
