namespace FinanceManager.Web.Services;

/// <summary>
/// Configuration of the foreign-exchange provider binding (section <c>"Fx"</c>).
/// </summary>
public sealed class FxOptions
{
    /// <summary>Configuration section path.</summary>
    public const string SectionName = "Fx";

    /// <summary>Default Frankfurter API endpoint (ECB reference rates, no API key).</summary>
    public const string DefaultBaseUrl = "https://api.frankfurter.dev";

    /// <summary>Base URL of the FX API. Defaults to <see cref="DefaultBaseUrl"/>.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>HTTP timeout in seconds applied to the named "Frankfurter" client.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Master switch: <c>false</c> disables all provider calls (service falls back to stored rates or null).</summary>
    public bool Enabled { get; set; } = true;
}
