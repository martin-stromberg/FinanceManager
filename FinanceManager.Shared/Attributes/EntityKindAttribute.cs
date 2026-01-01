using System;
using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Shared.Attributes;


/// <summary>
/// Associates a domain/entity type with a specific <see cref="PostingKind"/>.
/// Apply this attribute to domain classes or structs to declare the posting kind
/// that should be used when mapping or handling the entity in posting-related logic.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class EntityKindAttribute : Attribute
{
    /// <summary>
    /// The posting kind associated with the decorated type.
    /// </summary>
    public PostingKind Kind { get; }

    /// <summary>
    /// Creates a new <see cref="EntityKindAttribute"/> that associates a domain/entity type with a specific <see cref="PostingKind"/>.
    /// </summary>
    /// <param name="kind">The posting kind to associate with the decorated type.</param>
    public EntityKindAttribute(PostingKind kind)
    {
        Kind = kind;
    }
}
