using FinanceManager.Shared.Dtos.Budget;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Budget Purposes

    /// <summary>
    /// Creates a budget purpose for the current user.
    /// </summary>
    /// <param name="request">Create request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created budget purpose.</returns>
    public async Task<BudgetPurposeDto> BudgetPurposes_CreateAsync(BudgetPurposeCreateRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/budget/purposes", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<BudgetPurposeDto>(cancellationToken: ct))!;
    }

    #endregion Budget Purposes
}
