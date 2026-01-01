namespace FinanceManager.Domain;

/// <summary>
/// Base class for value objects. Value objects are compared by their set of equality components
/// rather than by identity. Derive from this class and implement <see cref="GetEqualityComponents"/>
/// to provide the sequence of components that determine equality.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Returns an ordered sequence of components that participate in equality comparisons for the value object.
    /// Implementations should yield components in a stable order and include nulls when appropriate.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{object}"/> of components used for equality and hash code generation.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>
    /// Determines whether the specified object is equal to the current value object.
    /// Two value objects are equal when they are of the same runtime type and their equality components
    /// are equal in sequence order.
    /// </summary>
    /// <param name="obj">The object to compare with the current value object.</param>
    /// <returns><c>true</c> if the specified object is equal to the current value object; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Returns a hash code based on the equality components returned by <see cref="GetEqualityComponents"/>.
    /// The implementation combines component hash codes to produce a stable hash for the value object.
    /// </summary>
    /// <returns>An integer hash code for the current value object.</returns>
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj?.GetHashCode() ?? 0));
    }
}
