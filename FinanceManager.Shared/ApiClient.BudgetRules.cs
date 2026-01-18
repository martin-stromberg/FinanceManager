using FinanceManager.Shared.Dtos.Budget;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Budget Rules

    /// <summary>
    /// Creates a budget rule for the current user.
    /// </summary>
    /// <param name="request">Create request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created budget rule.</returns>
    public async Task<BudgetRuleDto> BudgetRules_CreateAsync(BudgetRuleCreateRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/budget/rules", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<BudgetRuleDto>(cancellationToken: ct))!;
    }

    #endregion Budget Rules
}
