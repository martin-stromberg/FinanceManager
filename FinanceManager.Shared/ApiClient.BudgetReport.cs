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

    #endregion Budget Report
}
