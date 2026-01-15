using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Domain.Budget;

/// <summary>
/// A budget category groups multiple budget purposes.
/// </summary>
public sealed class BudgetCategory : Entity, IAggregateRoot
{
    private BudgetCategory() { }

    /// <summary>
    /// Creates a new budget category owned by a user.
    /// </summary>
    /// <param name="ownerUserId">Owner user id. Must not be empty.</param>
    /// <param name="name">Name of the category. Must not be null or whitespace.</param>
    public BudgetCategory(Guid ownerUserId, string name)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        Rename(name);
    }

    /// <summary>
    /// Owner user identifier.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Display name of the category.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Renames the category.
    /// </summary>
    public void Rename(string name)
    {
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Touch();
    }

    /// <summary>
    /// DTO carrying the serializable state of a <see cref="BudgetCategory"/> for backup purposes.
    /// </summary>
    public sealed record BudgetCategoryBackupDto(Guid Id, Guid OwnerUserId, string Name);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this budget category.
    /// </summary>
    public BudgetCategoryBackupDto ToBackupDto() => new(Id, OwnerUserId, Name);

    /// <summary>
    /// Applies values from the provided backup DTO to this entity.
    /// </summary>
    public void AssignBackupDto(BudgetCategoryBackupDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        OwnerUserId = dto.OwnerUserId;
        Name = dto.Name;
    }
}
