using FinanceManager.Application.Securities.ReturnAnalysis;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.Services;

/// <summary>
/// Web-layer implementation of <see cref="IReturnAnalysisLocalizer"/>.
/// Delegates all lookups to the standard ASP.NET Core <see cref="IStringLocalizer{T}"/>
/// so that resource files under <c>Resources/Services/ReturnAnalysisLocalizer.*.resx</c> are used.
/// </summary>
public sealed class ReturnAnalysisLocalizer : IReturnAnalysisLocalizer
{
    private readonly IStringLocalizer<ReturnAnalysisLocalizer> _localizer;

    /// <summary>
    /// Initializes a new instance of <see cref="ReturnAnalysisLocalizer"/>.
    /// </summary>
    /// <param name="localizer">ASP.NET Core string localizer for this resource type.</param>
    public ReturnAnalysisLocalizer(IStringLocalizer<ReturnAnalysisLocalizer> localizer)
    {
        _localizer = localizer;
    }

    /// <inheritdoc/>
    public string this[string key] => _localizer[key].Value;

    /// <inheritdoc/>
    public string Format(string key, params object[] args)
    {
        var template = _localizer[key].Value;
        return string.Format(template, args);
    }
}
