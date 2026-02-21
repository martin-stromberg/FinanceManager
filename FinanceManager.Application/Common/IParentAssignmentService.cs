using FinanceManager.Shared.Dtos.Common;

namespace FinanceManager.Application.Common;

/// <summary>
/// Applies an optional parent assignment for a newly created entity.
/// Implementations must validate ownership/permissions.
/// </summary>
public interface IParentAssignmentService
{
    /// <summary>
    /// Assigns the created entity to the specified parent context.
    /// Returns <c>true</c> when the assignment was performed.
    /// </summary>
    Task<bool> TryAssignAsync(Guid ownerUserId, ParentLinkRequest? parent, string createdKind, Guid createdId, CancellationToken ct);
}
