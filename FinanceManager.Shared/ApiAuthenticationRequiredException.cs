using System.Net;

namespace FinanceManager.Shared;

/// <summary>
/// Raised by <see cref="ApiClient"/> when an API call fails because the current session is not
/// authenticated (expired or missing token). The exception derives from
/// <see cref="HttpRequestException"/> so existing callers that catch HTTP failures keep working;
/// server-side middleware can catch this specific type to redirect the user to the login page.
/// </summary>
public sealed class ApiAuthenticationRequiredException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of <see cref="ApiAuthenticationRequiredException"/>.
    /// </summary>
    /// <param name="statusCode">HTTP status code of the rejected API response.</param>
    /// <param name="errorCode">Optional machine-readable error code extracted from the response.</param>
    /// <param name="errorMessage">Optional human-readable error message extracted from the response.</param>
    public ApiAuthenticationRequiredException(HttpStatusCode statusCode, string? errorCode, string? errorMessage)
        : base(errorMessage ?? $"API request requires authentication (HTTP {(int)statusCode}).", null, statusCode)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Optional machine-readable error code extracted from the API response.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Optional human-readable error message extracted from the API response.
    /// </summary>
    public string? ErrorMessage { get; }
}
