namespace FinanceManager.Shared.Dtos.Common;

/// <summary>
/// DTO wrapping an API error response for standardized client consumption.
/// </summary>
/// <param name="error">Machine-readable error code (e.g. Err_Invalid_name).</param>
/// <param name="message">Human-readable error message intended for display.</param>
public sealed record ApiErrorDto(string? error, string? message)
{
    /// <summary>
    /// Creates an error response with only a human-readable message.
    /// </summary>
    /// <param name="message">Human-readable error message intended for display.</param>
    public ApiErrorDto(string message) : this(null, message)
    {
    }

    /// <summary>
    /// Creates an error response with only a human-readable message.
    /// </summary>
    /// <param name="message">Human-readable error message intended for display.</param>
    public static ApiErrorDto FromMessage(string message) => new(null, message);
}
