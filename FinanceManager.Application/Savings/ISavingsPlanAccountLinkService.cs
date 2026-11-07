namespace FinanceManager.Application.Savings;

/// <summary>
/// Service managing the explicit many-to-many links between savings plans and bank accounts.
/// Links are owner-scoped: only a plan and an account belonging to the same user can be linked.
/// A plan can be linked to several accounts and an account can serve several plans; duplicate
/// links are rejected.
/// </summary>
public interface ISavingsPlanAccountLinkService
{
    /// <summary>
    /// Creates a link between a savings plan and an account of the same owner.
    /// </summary>
    /// <param name="savingsPlanId">Identifier of the savings plan to link.</param>
    /// <param name="accountId">Identifier of the account to link.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the operation.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the link has been persisted.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the savings plan or the account does not exist or does not belong to
    /// <paramref name="ownerUserId"/> (links between different owners are rejected the same way).
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the link already exists.</exception>
    Task AddLinkAsync(Guid savingsPlanId, Guid accountId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Removes the link between a savings plan and an account.
    /// </summary>
    /// <param name="savingsPlanId">Identifier of the savings plan.</param>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the operation.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the link existed and was removed; otherwise <c>false</c> — including
    /// the case that the savings plan does not exist or does not belong to the owner.</returns>
    Task<bool> RemoveLinkAsync(Guid savingsPlanId, Guid accountId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Lists the identifiers of all accounts linked to the specified savings plan.
    /// </summary>
    /// <param name="savingsPlanId">Identifier of the savings plan.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the query.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The linked account identifiers (possibly empty), or <c>null</c> when the savings
    /// plan does not exist or does not belong to <paramref name="ownerUserId"/>.</returns>
    Task<IReadOnlyList<Guid>?> GetLinkedAccountIdsAsync(Guid savingsPlanId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Lists the identifiers of all savings plans linked to the specified account.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the query.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The linked savings plan identifiers (possibly empty), or <c>null</c> when the
    /// account does not exist or does not belong to <paramref name="ownerUserId"/>.</returns>
    Task<IReadOnlyList<Guid>?> GetLinkedSavingsPlanIdsAsync(Guid accountId, Guid ownerUserId, CancellationToken ct);
}
