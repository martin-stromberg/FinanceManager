using System;

namespace FinanceManager.Application.Exceptions;

public sealed class DomainValidationException : Exception
{
    public string? Code { get; }

    public DomainValidationException(string message) : base(message) { }

    public DomainValidationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
