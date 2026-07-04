using System.Net.Http.Json;

namespace FinanceManager.Tests.E2E;

public sealed class AuthGateway
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public AuthGateway(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task RegisterAsync(string username, string password)
    {
        await _page.GotoAsync("/register");
        await _page.Locator("#username").FillAsync(username);
        await _page.Locator("#password").FillAsync(password);

        await RegisterOrLoginAsync("/api/auth/register", new RegisterRequest(username, password, null, null));

        await _page.GotoAsync("/");
    }

    public async Task LoginAsync(string username, string password)
    {
        await _page.GotoAsync("/login");
        await _page.Locator("#login-user").FillAsync(username);
        await _page.Locator("#login-pass").FillAsync(password);

        await BrowserApiHelper.PostJsonAsync<LoginRequest, AuthOkResponse>(_page, "/api/auth/login", new LoginRequest(username, password, null, null));

        await _page.GotoAsync("/");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.Locator("body").WaitForAsync();
    }

    public async Task LogoutAsync()
    {
        await _page.Context.ClearCookiesAsync();
        await _page.GotoAsync("/login");
        await _page.Locator("button[type=submit]").WaitForAsync();
    }

    private async Task<string> RegisterOrLoginAsync<TRequest>(string path, TRequest request)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl),
        };

        using var response = await client.PostAsJsonAsync(path, request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
        }

        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            throw new InvalidOperationException("Authentication response did not include a session cookie.");
        }

        foreach (var header in values)
        {
            if (header.StartsWith("FinanceManager.Auth=", StringComparison.OrdinalIgnoreCase))
            {
                var semi = header.IndexOf(';');
                return semi > 0
                    ? header["FinanceManager.Auth=".Length..semi]
                    : header["FinanceManager.Auth=".Length..];
            }
        }

        throw new InvalidOperationException("Authentication cookie not found in response.");
    }

}
