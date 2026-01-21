using FinanceManager.Shared.Dtos.Budget;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Budget Report

    /// <summary>
    /// Generates a budget report for the current user.
    /// </summary>
    /// <param name="request">The report request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated <see cref="BudgetReportDto"/>.</returns>
    public async Task<BudgetReportDto> Budgets_GetReportAsync(BudgetReportRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/budget/report", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<BudgetReportDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Lists postings that are not covered by any budget purpose for the given range.
    /// </summary>
    /// <param name="from">Start date (optional).</param>
    /// <param name="to">End date (optional).</param>
    /// <param name="dateBasis">Date basis for filtering.</param>
    /// <param name="kind">Optional kind filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of unbudgeted postings.</returns>
    public async Task<IReadOnlyList<PostingServiceDto>> Budgets_GetUnbudgetedPostingsAsync(DateTime? from, DateTime? to, BudgetReportDateBasis dateBasis, string? kind = null, CancellationToken ct = default)
    {
        var url = $"/api/budget/report/unbudgeted?dateBasis={(int)dateBasis}";
        if (from.HasValue) url += $"&from={from.Value:O}";
        if (to.HasValue) url += $"&to={to.Value:O}";
        if (!string.IsNullOrWhiteSpace(kind)) url += $"&kind={Uri.EscapeDataString(kind)}";
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<PostingServiceDto>>(cancellationToken: ct) ?? Array.Empty<PostingServiceDto>();
    }

    /// <summary>
    /// Gets the Home Monthly Budget KPI values (planned/actual income and expenses).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="MonthlyBudgetKpiDto"/> for the current user/month.</returns>
    public async Task<MonthlyBudgetKpiDto> Budgets_GetMonthlyKpiAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/budget/report/kpi-monthly", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<MonthlyBudgetKpiDto>(cancellationToken: ct))!;
    }

    #endregion Budget Report
}
