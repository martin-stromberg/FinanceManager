using FinanceManager.Application.Exceptions;
using FinanceManager.Shared.Dtos.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.Infrastructure.ApiErrors;

/// <summary>
/// Factory for producing standardized <see cref="ApiErrorDto"/> instances following the
/// project-wide Origin/Code/Message error contract.
/// </summary>
public static class ApiErrorFactory
{
    /// <summary>
    /// Creates an <see cref="ApiErrorDto"/> for an <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="origin">The logical origin for the error (e.g. API_BudgetRule).</param>
    /// <param name="ex">The thrown exception.</param>
    /// <param name="localizer">Localizer used to resolve <c>{origin}_{code}</c> message keys.</param>
    /// <returns>Standardized API error DTO.</returns>
    public static ApiErrorDto FromArgumentException(string origin, ArgumentException ex, IStringLocalizer? localizer)
    {
        var code = !string.IsNullOrWhiteSpace(ex.ParamName)
            ? $"Err_Invalid_{ex.ParamName}"
            : "Err_InvalidArgument";

        var message = ResolveMessage(origin, code, ex.Message, localizer);
        return ApiErrorDto.Create(origin, code, message);
    }

    /// <summary>
    /// Creates an <see cref="ApiErrorDto"/> for an <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="origin">The logical origin for the error (e.g. API_BudgetRule).</param>
    /// <param name="ex">The thrown exception.</param>
    /// <param name="localizer">Localizer used to resolve <c>{origin}_{code}</c> message keys.</param>
    /// <returns>Standardized API error DTO.</returns>
    public static ApiErrorDto FromArgumentOutOfRangeException(string origin, ArgumentOutOfRangeException ex, IStringLocalizer? localizer)
    {
        var code = !string.IsNullOrWhiteSpace(ex.ParamName)
            ? $"Err_OutOfRange_{ex.ParamName}"
            : "Err_OutOfRange";

        var message = ResolveMessage(origin, code, ex.Message, localizer);
        return ApiErrorDto.Create(origin, code, message);
    }

    /// <summary>
    /// Creates an <see cref="ApiErrorDto"/> for a <see cref="DomainValidationException"/>.
    /// </summary>
    /// <param name="origin">The logical origin for the error (e.g. API_BudgetRule).</param>
    /// <param name="ex">The thrown exception.</param>
    /// <param name="localizer">Localizer used to resolve <c>{origin}_{code}</c> message keys.</param>
    /// <returns>Standardized API error DTO.</returns>
    public static ApiErrorDto FromDomainValidationException(string origin, DomainValidationException ex, IStringLocalizer? localizer)
    {
        var code = ex.Code;
        var message = ResolveMessage(origin, code, ex.Message, localizer);
        return ApiErrorDto.Create(origin, code, message);
    }

    /// <summary>
    /// Creates an <see cref="ApiErrorDto"/> for an unexpected error.
    /// </summary>
    /// <param name="origin">The logical origin for the error (e.g. API_BudgetRule).</param>
    /// <param name="localizer">Localizer used to resolve <c>{origin}_{code}</c> message keys.</param>
    /// <returns>Standardized API error DTO.</returns>
    public static ApiErrorDto Unexpected(string origin, IStringLocalizer localizer)
    {
        const string code = "Err_Unexpected";

        var primaryKey = $"{origin}_{code}";
        var entry = localizer[primaryKey];

        if (!entry.ResourceNotFound)
        {
            return ApiErrorDto.Create(origin, code, entry.Value);
        }

        // Shared controller resource bundle fallback
        var fallbackEntry = localizer[$"API_Common_{code}"];
        var message = fallbackEntry.ResourceNotFound ? "Unexpected error" : fallbackEntry.Value;
        return ApiErrorDto.Create(origin, code, message);
    }

    private static string ResolveMessage(string origin, string code, string fallback, IStringLocalizer? localizer)
    {
        if (localizer == null)
        {
            return fallback;
        }

        var key = $"{origin}_{code}";
        var entry = localizer[key];
        return entry.ResourceNotFound ? fallback : entry.Value;
    }
}
