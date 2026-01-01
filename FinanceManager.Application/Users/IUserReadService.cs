namespace FinanceManager.Application.Users;

/// <summary>
/// Read-only user-related queries used by UI and services to determine existence or retrieve lightweight information.
/// </summary>
public interface IUserReadService
{
    /// <summary>
    /// Determines whether any user accounts exist in the system.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to <c>true</c> when at least one user exists; otherwise <c>false</c>.</returns>
    Task<bool> HasAnyUsersAsync(CancellationToken ct);
}
