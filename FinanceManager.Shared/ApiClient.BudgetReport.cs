using FinanceManager.Shared.Dtos.Budget;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Budget Report

    /// <summary>
    /// Generates a budget report for the current user.
    /// </summary>
    public async Task<BudgetReportDto> Budgets_GetReportAsync(BudgetReportRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/budget/report", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<BudgetReportDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Lists postings that are not covered by any budget purpose for the given range.
    /// </summary>
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

    #endregion Budget Report
}
