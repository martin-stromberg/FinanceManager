namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Stable error codes returned by authentication services in <see cref="FinanceManager.Domain.Result{T}.Error"/>,
/// so callers (e.g. controllers) can map them to HTTP status codes and localized messages without
/// matching on human-readable text.
/// </summary>
public static class AuthErrorCodes
{
    /// <summary>
    /// Registration was rejected because self-registration is disabled and the user is not the first user.
    /// Maps to HTTP 403 Forbidden.
    /// </summary>
    public const string RegistrationDisabled = "Err_RegistrationDisabled";

    /// <summary>
    /// Registration was rejected because a user with the requested username already exists.
    /// Maps to HTTP 409 Conflict.
    /// </summary>
    public const string UserAlreadyExists = "Err_Conflict_UserAlreadyExists";

    /// <summary>
    /// Registration failed for a reason that is neither a disabled self-registration gate nor a
    /// duplicate username - e.g. missing fields or password-policy/identity validation errors.
    /// Maps to HTTP 400 Bad Request with a generic message so no detail usable for user
    /// enumeration is exposed to clients.
    /// </summary>
    public const string InvalidRegistration = "Err_InvalidRegistration";
}
