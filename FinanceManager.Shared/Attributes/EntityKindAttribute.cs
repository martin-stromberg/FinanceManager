using System;
using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Shared.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class EntityKindAttribute : Attribute
{
    public PostingKind Kind { get; }

    public EntityKindAttribute(PostingKind kind)
    {
        Kind = kind;
    }
}
