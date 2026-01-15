namespace FinanceManager.Shared.Dtos.Common;

/// <summary>
/// Base record for create requests that support an optional parent context.
/// </summary>
/// <param name="Parent">Optional parent context used for server-side assignment.</param>
public abstract record CreateRequestWithParent(ParentLinkRequest? Parent = null);
