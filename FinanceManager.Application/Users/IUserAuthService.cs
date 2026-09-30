using FinanceManager.Domain;

namespace FinanceManager.Application.Users;

/// <summary>
/// Service handling user authentication and registration use-cases.
/// Implementations are responsible for validating input, creating users and issuing authentication tokens.
/// </summary>
public interface IUserAuthService
{
    /// <summary>
    /// Registers a new user according to the provided command and returns an authentication result on success.
    /// </summary>
    /// <param name="command">Registration command containing username, password and optional preferences.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{AuthResult}"/> indicating success and containing <see cref="AuthResult"/> on success,
    /// or a failed <see cref="Result{AuthResult}"/> with an error message.
    /// </returns>
    Task<Result<AuthResult>> RegisterAsync(RegisterUserCommand command, CancellationToken ct);

    /// <summary>
    /// Attempts to authenticate a user with the supplied login command and returns an authentication result on success.
    /// </summary>
    /// <param name="command">Login command containing credentials and optional client context (IP, language, timezone).</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{AuthResult}"/> indicating success and containing <see cref="AuthResult"/> on success,
    /// or a failed <see cref="Result{AuthResult}"/> with an error message when authentication fails.
    /// </returns>
    Task<Result<AuthResult>> LoginAsync(LoginCommand command, CancellationToken ct);

    /// <summary>
    /// Changes the password of the user identified by <paramref name="userId"/> after verifying the current password.
    /// </summary>
    /// <param name="userId">Identifier of the user whose password should be changed.</param>
    /// <param name="currentPassword">The current password used for verification.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result"/> indicating success, or a failed <see cref="Result"/> carrying a stable error code
    /// (<c>Err_InvalidCurrentPassword</c> when the current password is wrong,
    /// <c>Err_PasswordPolicyViolation</c> when the new password violates the password policy,
    /// <c>Err_UserNotFound</c> when the user does not exist).
    /// </returns>
    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
}
