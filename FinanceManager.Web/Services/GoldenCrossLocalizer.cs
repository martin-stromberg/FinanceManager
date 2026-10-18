using System.Globalization;
using FinanceManager.Application.Securities.GoldenCross;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.Services;

/// <summary>
/// Web-layer implementation of <see cref="IGoldenCrossLocalizer"/> backed by
/// <c>Resources/Services/GoldenCrossLocalizer.*.resx</c>. Texts are resolved in the culture of the
/// notified user (background jobs have no request culture).
/// </summary>
public sealed class GoldenCrossLocalizer : IGoldenCrossLocalizer
{
    private readonly IStringLocalizer<GoldenCrossLocalizer> _localizer;

    /// <summary>
    /// Initializes a new instance of <see cref="GoldenCrossLocalizer"/>.
    /// </summary>
    /// <param name="localizer">ASP.NET Core string localizer for this resource type.</param>
    public GoldenCrossLocalizer(IStringLocalizer<GoldenCrossLocalizer> localizer)
    {
        _localizer = localizer;
    }

    /// <inheritdoc/>
    public string Format(string key, string? cultureName, params object[] args)
    {
        var original = CultureInfo.CurrentUICulture;
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            if (!string.IsNullOrWhiteSpace(cultureName))
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(cultureName);
                    CultureInfo.CurrentUICulture = culture;
                    CultureInfo.CurrentCulture = culture;
                }
                catch (CultureNotFoundException)
                {
                    // keep default culture
                }
            }
            return string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, args);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
