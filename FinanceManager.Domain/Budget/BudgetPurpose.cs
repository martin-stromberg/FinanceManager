using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Domain.Budget;

/// <summary>
/// A budget purpose defines what expected (planned) amounts should be tracked against.
/// A purpose aggregates planned and actual values for a specific source (contact, contact group, savings plan).
/// </summary>
public sealed class BudgetPurpose : Entity, IAggregateRoot
{
    private BudgetPurpose() { }

    /// <summary>
    /// Creates a new budget purpose owned by a user.
    /// </summary>
    /// <param name="ownerUserId">Owner user id. Must not be empty.</param>
    /// <param name="name">Name of the purpose. Must not be null or whitespace.</param>
    /// <param name="sourceType">Source type used to map actual values.</param>
    /// <param name="sourceId">Identifier of the source entity (depends on <paramref name="sourceType"/>).</param>
    /// <param name="description">Optional description.</param>
    public BudgetPurpose(Guid ownerUserId, string name, BudgetSourceType sourceType, Guid sourceId, string? description = null)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        Rename(name);
        SetSource(sourceType, sourceId);
        SetDescription(description);
    }

    /// <summary>
    /// Owner user identifier.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Display name of the purpose.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Optional description for the purpose.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// The type of source used to resolve actual postings.
    /// </summary>
    public BudgetSourceType SourceType { get; private set; }

    /// <summary>
    /// The identifier of the source entity.
    /// </summary>
    public Guid SourceId { get; private set; }

    /// <summary>
    /// Renames the purpose.
    /// </summary>
    /// <param name="name">New name.</param>
    public void Rename(string name)
    {
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Touch();
    }

    /// <summary>
    /// Sets a description for the purpose.
    /// </summary>
    /// <param name="description">Description to set, or null/whitespace to clear.</param>
    public void SetDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Touch();
    }

    /// <summary>
    /// Updates the source mapping.
    /// </summary>
    /// <param name="sourceType">New source type.</param>
    /// <param name="sourceId">New source id. Must not be empty.</param>
    public void SetSource(BudgetSourceType sourceType, Guid sourceId)
    {
        SourceType = sourceType;
        SourceId = Guards.NotEmpty(sourceId, nameof(sourceId));
        Touch();
    }
}
