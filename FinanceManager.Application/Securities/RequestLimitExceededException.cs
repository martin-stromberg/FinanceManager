namespace FinanceManager.Application.Securities;

/// <summary>
/// Exception thrown when a price provider's request limits were exceeded and the client is blocked from issuing further requests for a short period.
/// </summary>
public sealed class RequestLimitExceededException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="RequestLimitExceededException"/> with the specified message.
    /// </summary>
    /// <param name="message">Human readable description of the limit condition.</param>
    public RequestLimitExceededException(string message) : base(message) { }
}
