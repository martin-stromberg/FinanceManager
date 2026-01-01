using System;

namespace FinanceManager.Application.Exceptions;

/// <summary>
/// Exception type used to represent domain validation failures.
/// </summary>
public sealed class DomainValidationException : Exception
{
    /// <summary>
    /// Optional machine-friendly error code for the validation failure.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Creates a new DomainValidationException with a message.
    /// </summary>
    /// <param name="message">Error message.</param>
    public DomainValidationException(string message) : base(message) { }

    /// <summary>
    /// Creates a new DomainValidationException with a code and message.
    /// </summary>
    /// <param name="code">Machine-friendly code representing the error.</param>
    /// <param name="message">Error message.</param>
    public DomainValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
