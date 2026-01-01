using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    /// <summary>
    /// Authentication related operations (login, register, logout).
    /// </summary>
    #region Auth

    /// <summary>
    /// Authenticates an existing user and returns authentication result (including tokens or session info).
    /// </summary>
    /// <param name="request">Login request containing credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Authentication result.</returns>
    public async Task<AuthOkResponse> Auth_LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/login", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AuthOkResponse>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Registers a new user and returns authentication result (user created and signed in).
    /// </summary>
    /// <param name="request">Registration request with user details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Authentication result for the new user.</returns>
    public async Task<AuthOkResponse> Auth_RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/register", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<AuthOkResponse>(cancellationToken: ct))!;
    }

    /// <summary>
    /// Logs out the current user and clears server-side session or cookie state.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True on successful logout.</returns>
    public async Task<bool> Auth_LogoutAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("/api/auth/logout", content: null, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }

    #endregion Auth
}