using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace FinanceManager.Web.Services;

/// <summary>
/// Protects AlphaVantage API keys before persistence and restores them only for immediate runtime use.
/// </summary>
public interface IAlphaVantageSecretProtector
{
    /// <summary>
    /// Protects a plaintext AlphaVantage API key for storage.
    /// </summary>
    /// <param name="plaintext">The plaintext API key entered by a user.</param>
    /// <returns>The protected storage value including the format prefix, or <c>null</c> for empty input.</returns>
    string? Protect(string? plaintext);

    /// <summary>
    /// Restores a stored AlphaVantage API key.
    /// </summary>
    /// <param name="storedValue">The protected storage value.</param>
    /// <returns>The plaintext key for immediate use, or <c>null</c> for empty input.</returns>
    /// <exception cref="AlphaVantageSecretProtectionException">Thrown when a protected value cannot be restored.</exception>
    string? Unprotect(string? storedValue);

    /// <summary>
    /// Determines whether the stored value uses the protected AlphaVantage format.
    /// </summary>
    /// <param name="storedValue">The stored value to inspect.</param>
    /// <returns><c>true</c> when the value is protected; otherwise <c>false</c>.</returns>
    bool IsProtected(string? storedValue);
}

/// <summary>
/// Raised when an AlphaVantage API key cannot be protected or restored.
/// </summary>
public sealed class AlphaVantageSecretProtectionException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaVantageSecretProtectionException"/> class.
    /// </summary>
    public AlphaVantageSecretProtectionException()
        : base("Stored AlphaVantage API key cannot be read.")
    {
    }
}

/// <summary>
/// ASP.NET Core Data Protection based implementation for AlphaVantage API keys.
/// </summary>
public sealed class DataProtectionAlphaVantageSecretProtector : IAlphaVantageSecretProtector
{
    /// <summary>
    /// Prefix used to identify protected AlphaVantage API key values in persistence.
    /// </summary>
    public const string ProtectedPrefix = "dp:v1:";

    private const string Purpose = "FinanceManager.AlphaVantageApiKey.v1";
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataProtectionAlphaVantageSecretProtector"/> class.
    /// </summary>
    /// <param name="provider">The Data Protection provider used to create the API-key protector.</param>
    public DataProtectionAlphaVantageSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    /// <inheritdoc />
    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        var trimmed = plaintext.Trim();
        try
        {
            return ProtectedPrefix + _protector.Protect(trimmed);
        }
        catch (CryptographicException)
        {
            throw new AlphaVantageSecretProtectionException();
        }
    }

    /// <inheritdoc />
    public string? Unprotect(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        if (!IsProtected(storedValue))
        {
            return storedValue.Trim();
        }

        var payload = storedValue[ProtectedPrefix.Length..];
        try
        {
            return _protector.Unprotect(payload);
        }
        catch (CryptographicException)
        {
            throw new AlphaVantageSecretProtectionException();
        }
        catch (ArgumentException)
        {
            throw new AlphaVantageSecretProtectionException();
        }
    }

    /// <inheritdoc />
    public bool IsProtected(string? storedValue)
        => !string.IsNullOrWhiteSpace(storedValue)
           && storedValue.StartsWith(ProtectedPrefix, StringComparison.Ordinal);
}
