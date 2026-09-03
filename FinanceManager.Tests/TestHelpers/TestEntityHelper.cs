using System.Reflection;

namespace FinanceManager.Tests.TestHelpers;

/// <summary>
/// Reflection-based backdoor for assigning entity primary keys in tests. Domain entities typically expose
/// <c>Id</c> with a private setter (it is normally assigned by EF Core or a factory method), so tests that
/// need a deterministic, known id to assert against — e.g. to build cross-references between in-memory
/// entities before they are persisted — cannot set it directly and use this helper instead.
/// </summary>
internal static class TestEntityHelper
{
    /// <summary>
    /// Sets the <c>Id</c> property of <paramref name="entity"/> to <paramref name="id"/> via reflection,
    /// bypassing any private setter. Throws <see cref="InvalidOperationException"/> if the entity type has
    /// no <c>Id</c> property, so a typo or wrong entity type fails fast rather than silently doing nothing.
    /// </summary>
    /// <param name="entity">The entity instance whose <c>Id</c> should be overwritten.</param>
    /// <param name="id">The identifier value to assign.</param>
    public static void SetEntityId(object entity, Guid id)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        var prop = entity.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop == null) throw new InvalidOperationException($"Type {entity.GetType().FullName} does not have an 'Id' property.");
        prop.SetValue(entity, id);
    }
}
