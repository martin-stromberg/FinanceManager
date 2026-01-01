using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    #region Accounts

    /// <summary>
    /// Lists accounts for the current user with optional pagination and bank contact filter.
    /// </summary>
    public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(int skip = 0, int take = 100, Guid? bankContactId = null, CancellationToken ct = default)
    {
        var url = $"/api/accounts?skip={skip}&take={take}";
        if (bankContactId.HasValue) url += $"&bankContactId={Uri.EscapeDataString(bankContactId.Value.ToString())}";
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<IReadOnlyList<AccountDto>>(cancellationToken: ct) ?? Array.Empty<AccountDto>();
    }

    /// <summary>
    /// Gets a single account by id or null when not found.
    /// </summary>
    public async Task<AccountDto?> GetAccountAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/accounts/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<AccountDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Creates a new account.
    /// </summary>
    public async Task<AccountDto> CreateAccountAsync(AccountCreateRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/accounts", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AccountDto>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Updates an existing account. Returns null when not found.
    /// </summary>
    public async Task<AccountDto?> UpdateAccountAsync(Guid id, AccountUpdateRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"/api/accounts/{id}", request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<AccountDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Deletes an account. Returns false when not found.
    /// </summary>
    public async Task<bool> DeleteAccountAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/accounts/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    /// <summary>
    /// Assigns a symbol attachment to an account.
    /// </summary>
    public async Task SetAccountSymbolAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/accounts/{id}/symbol/{attachmentId}", content: null, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
    }

    /// <summary>
    /// Clears the symbol attachment from an account.
    /// </summary>
    public async Task ClearAccountSymbolAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"/api/accounts/{id}/symbol", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
    }

    #endregion Accounts
}
