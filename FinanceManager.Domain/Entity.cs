namespace FinanceManager.Domain;

/// <summary>
/// Base class for domain entities providing a common Id property and timestamps.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Numeric or GUID identifier for entities. Derived classes may override or use their own id strategy.
    /// </summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when the entity was created.
    /// </summary>
    public DateTime CreatedUtc { get; protected set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the entity was last modified, or null when never modified after creation.
    /// </summary>
    public DateTime? ModifiedUtc { get; protected set; }

    /// <summary>
    /// Updates the ModifiedUtc timestamp to the current UTC time.
    /// Intended for internal use by aggregate roots when changes are applied.
    /// </summary>
    protected void Touch() => ModifiedUtc = DateTime.UtcNow;
}

/// <summary>
/// Marker interface to denote aggregate root entities in the domain model.
/// </summary>
public interface IAggregateRoot { }
