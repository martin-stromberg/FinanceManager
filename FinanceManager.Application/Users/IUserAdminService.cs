namespace FinanceManager.Application.Users;

/// <summary>
/// Administrative user management service used by UI/administration components to list, create, update
/// and remove users as well as manage passwords and locks.
/// </summary>
public interface IUserAdminService
{
    /// <summary>
    /// Returns a list of all users suitable for administrative overview.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to a read-only list of <see cref="UserAdminDto"/> instances.</returns>
    Task<IReadOnlyList<UserAdminDto>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Retrieves a single user by id for administrative purposes.
    /// </summary>
    /// <param name="id">Identifier of the user to retrieve.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to the <see cref="UserAdminDto"/> for the user, or null when the user was not found.</returns>
    Task<UserAdminDto?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Creates a new user with the specified username and password and returns the created user representation.
    /// </summary>
    /// <param name="username">Desired username for the new user.</param>
    /// <param name="password">Plain-text password for the new user. The implementation is responsible for hashing and validation.</param>
    /// <param name="isAdmin">Whether the new user should be created with administrative privileges.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to the created <see cref="UserAdminDto"/>.</returns>
    Task<UserAdminDto> CreateAsync(string username, string password, bool isAdmin, CancellationToken ct);

    /// <summary>
    /// Updates an existing user's mutable fields.
    /// </summary>
    /// <param name="id">Identifier of the user to update.</param>
    /// <param name="username">New username or null to leave unchanged.</param>
    /// <param name="isAdmin">New admin flag or null to leave unchanged.</param>
    /// <param name="active">New active flag or null to leave unchanged.</param>
    /// <param name="preferredLanguage">New preferred language or null to leave unchanged.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to the updated <see cref="UserAdminDto"/>, or null if the user was not found.</returns>
    Task<UserAdminDto?> UpdateAsync(Guid id, string? username, bool? isAdmin, bool? active, string? preferredLanguage, CancellationToken ct);

    /// <summary>
    /// Resets the password for a user to a new password.
    /// </summary>
    /// <param name="id">Identifier of the user whose password will be reset.</param>
    /// <param name="newPassword">New plain-text password. The implementation must validate and hash this password.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to <c>true</c> when the password was successfully reset; otherwise <c>false</c> (for example when the user was not found or validation failed).</returns>
    Task<bool> ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct);

    /// <summary>
    /// Unlocks a user by clearing any lockout state.
    /// </summary>
    /// <param name="id">Identifier of the user to unlock.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to <c>true</c> when the user was unlocked; otherwise <c>false</c> (for example when the user was not found or not locked).</returns>
    Task<bool> UnlockAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Deletes a user permanently.
    /// </summary>
    /// <param name="id">Identifier of the user to delete.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to <c>true</c> when the user was successfully deleted; otherwise <c>false</c>.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}
