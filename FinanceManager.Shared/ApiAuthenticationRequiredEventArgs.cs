using System.Net;

namespace FinanceManager.Shared;

/// <summary>
/// Describes an API response that requires the UI to re-authenticate the current user.
/// </summary>
public sealed class ApiAuthenticationRequiredEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new authentication-required event payload.
    /// </summary>
    /// <param name="statusCode">HTTP status code that triggered the authentication signal.</param>
    /// <param name="errorCode">Optional machine-readable error code from the API response.</param>
    /// <param name="errorMessage">Optional human-readable error message from the API response.</param>
    public ApiAuthenticationRequiredEventArgs(HttpStatusCode statusCode, string? errorCode, string? errorMessage)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// HTTP status code that triggered the authentication signal.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Optional machine-readable error code from the API response.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Optional human-readable error message from the API response.
    /// </summary>
    public string? ErrorMessage { get; }
}
