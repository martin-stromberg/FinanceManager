namespace FinanceManager.Web.Services;

/// <summary>
/// Stable classification for price-provider failures.
/// </summary>
public enum PriceProviderErrorClass
{
    /// <summary>
    /// Provider reported an invalid symbol/function combination.
    /// </summary>
    InvalidSymbolOrFunction = 1,
    /// <summary>
    /// Provider rate limit was reached.
    /// </summary>
    RateLimit = 2,
    /// <summary>
    /// Network or timeout failures after retry exhaustion.
    /// </summary>
    TransientNetwork = 3,
    /// <summary>
    /// Any provider failure that cannot be mapped to a more specific class.
    /// </summary>
    UnknownProviderError = 4
}

/// <summary>
/// Helpers for converting <see cref="PriceProviderErrorClass"/> values to persisted API-safe codes.
/// </summary>
public static class PriceProviderErrorClassExtensions
{
    /// <summary>
    /// Converts the error class to the required stable code representation.
    /// </summary>
    /// <param name="errorClass">Error class value.</param>
    /// <returns>Stable uppercase code string.</returns>
    public static string ToCode(this PriceProviderErrorClass errorClass)
        => errorClass switch
        {
            PriceProviderErrorClass.InvalidSymbolOrFunction => "INVALID_SYMBOL_OR_FUNCTION",
            PriceProviderErrorClass.RateLimit => "RATE_LIMIT",
            PriceProviderErrorClass.TransientNetwork => "TRANSIENT_NETWORK",
            _ => "UNKNOWN_PROVIDER_ERROR"
        };
}

/// <summary>
/// Exception used by price providers to expose a deterministic error classification and provider raw details.
/// </summary>
public sealed class PriceProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="PriceProviderException"/>.
    /// </summary>
    /// <param name="errorClass">Deterministic provider error class.</param>
    /// <param name="providerRawMessage">Raw provider detail (internal use only).</param>
    /// <param name="message">Safe technical exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public PriceProviderException(PriceProviderErrorClass errorClass, string? providerRawMessage, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorClass = errorClass;
        ProviderRawMessage = providerRawMessage;
    }

    /// <summary>
    /// Classified provider error class.
    /// </summary>
    public PriceProviderErrorClass ErrorClass { get; }

    /// <summary>
    /// Stable uppercase representation of <see cref="ErrorClass"/>.
    /// </summary>
    public string ErrorClassCode => ErrorClass.ToCode();

    /// <summary>
    /// Raw provider text for internal diagnostics; must not be shown directly in end-user notifications.
    /// </summary>
    public string? ProviderRawMessage { get; }
}
