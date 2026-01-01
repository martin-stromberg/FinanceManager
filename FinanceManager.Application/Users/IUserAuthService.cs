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
}
