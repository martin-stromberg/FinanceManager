using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    // Authentication related operations (login, register, logout).
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
        CaptureAuthCookieAsBearer(resp);
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
        CaptureAuthCookieAsBearer(resp);
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
        // The mirrored bearer credential must die with the cookie session: without clearing it, a
        // still-valid JWT would keep authorizing unsafe calls after logout.
        _http.DefaultRequestHeaders.Authorization = null;
        return true;
    }

    /// <summary>
    /// Mirrors the freshly issued <c>FinanceManager.Auth</c> cookie into an
    /// <c>Authorization: Bearer</c> default header, so non-browser clients authenticate the same way
    /// the Blazor circuit does (bearer token instead of ambient cookie). This also keeps
    /// cookie-authenticated CSRF enforcement from applying to pure API clients.
    /// </summary>
    /// <param name="resp">The login/register response that carries the <c>Set-Cookie</c> header.</param>
    private void CaptureAuthCookieAsBearer(HttpResponseMessage resp)
    {
        const string authCookiePrefix = "FinanceManager.Auth=";
        if (!resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        var token = setCookies
            .Select(v => v.Split(';')[0])
            .Where(v => v.StartsWith(authCookiePrefix, StringComparison.Ordinal))
            .Select(v => v[authCookiePrefix.Length..])
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    #endregion Auth
}