using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Portfolio Analysis Report

    /// <summary>
    /// Gets the portfolio analysis report for the current user (server-side cached, monthly validity).
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The <see cref="PortfolioAnalysisReportDto"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<PortfolioAnalysisReportDto> Portfolio_GetAnalysisReportAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/portfolio/analysis-report", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<PortfolioAnalysisReportDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Gets the current user's portfolio KPI tile configuration (or defaults when none saved yet).
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The <see cref="PortfolioKpiConfigurationDto"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task<PortfolioKpiConfigurationDto> Portfolio_GetKpiConfigurationAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/portfolio/kpi-configuration", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<PortfolioKpiConfigurationDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Saves the current user's portfolio KPI tile configuration and invalidates the report cache.
    /// Returns <c>null</c> when the request payload was rejected as invalid.
    /// </summary>
    /// <param name="request">KPI configuration request payload.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>The persisted <see cref="PortfolioKpiConfigurationDto"/>, or <c>null</c> on validation failure.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails for reasons other than a validation (400) response.</exception>
    public async Task<PortfolioKpiConfigurationDto?> Portfolio_SaveKpiConfigurationAsync(PortfolioKpiConfigurationRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/portfolio/kpi-configuration", request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            await EnsureSuccessOrSetErrorAsync(resp);
            return null;
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PortfolioKpiConfigurationDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Manually resets (invalidates) the portfolio analysis report cache for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>A task that completes when the cache has been invalidated.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails or the server returns a non-success status code.</exception>
    public async Task Portfolio_ResetCacheAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("/api/portfolio/cache/reset", content: null, ct);
        resp.EnsureSuccessStatusCode();
    }

    #endregion Portfolio Analysis Report
}
