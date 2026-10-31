using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace FinanceManager.Tests.E2E;

/// <summary>Observes real form logins and JWT expiry without setting authentication cookies.</summary>
/// <param name="fixture">The regular application host and request observations.</param>
/// <param name="page">The browser page used for real user interactions.</param>
/// <returns>A probe bound to the supplied application and page.</returns>
public sealed class SessionExpiryProbe(SessionExpiryWebAppFixture fixture, IPage page)
{
    /// <summary>Seeds a user and signs in exclusively through the visible login form.</summary>
    /// <param name="scenario">The scenario label included in the unique username.</param>
    /// <returns>The signed-in user ID, original token expiry, and username.</returns>
    public async Task<(Guid UserId, DateTimeOffset Expires, string Username)> LoginAsync(string scenario)
    {
        var username = $"session-{scenario}-{Guid.NewGuid():N}";
        const string password = "Secret123";
        var user = await new TestUserSeeder(fixture.DatabasePath).EnsureUserAsync(username, password);
        var loginResponseTask = page.WaitForResponseAsync(response =>
            new Uri(response.Url).AbsolutePath == "/api/auth/login" && response.Request.Method == "POST");
        await new AuthGateway(page, fixture.BaseUrl).LoginThroughUiAsync(username, password);
        var response = await loginResponseTask;
        response.Status.Should().Be(200, "the actual form submission must authenticate successfully");
        var cookieHeader = (await response.HeadersArrayAsync()).First(h =>
            h.Name.Equals("set-cookie", StringComparison.OrdinalIgnoreCase)
            && h.Value.StartsWith("FinanceManager.Auth=", StringComparison.Ordinal)).Value;
        var token = cookieHeader["FinanceManager.Auth=".Length..].Split(';')[0];
        var expires = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
        Record($"form-login status=200 user={user.Id} originalExp={expires:O}");
        await page.WaitForURLAsync(url => new Uri(url).AbsolutePath == "/", new() { Timeout = 15_000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return (user.Id, new DateTimeOffset(expires, TimeSpan.Zero), username);
    }

    /// <summary>Waits for a specific new server-side API request and verifies its outcome.</summary>
    /// <param name="path">The API path to observe.</param>
    /// <param name="afterSequence">The last request sequence before the expected call.</param>
    /// <param name="userId">The signed-in scenario user ID supplied by the caller.</param>
    /// <param name="status">The expected HTTP response status.</param>
    /// <returns>The matching server-side request observation.</returns>
    public async Task<ApiObservation> ExpectApiAsync(string path, long afterSequence, Guid userId, int status = 200)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var entry = fixture.ReadObservations().Where(e => e.Sequence > afterSequence && e.Path == path)
                .OrderBy(e => e.Sequence).FirstOrDefault();
            if (entry is not null)
            {
                Record($"api sequence={entry.Sequence} path={path} status={entry.Status} url={new Uri(page.Url).AbsolutePath}");
                entry.Status.Should().Be(status, $"{path} is the actual server API request after UI login; log: {fixture.ObservationPath}");

                return entry;
            }
            await Task.Delay(200);
        }
        throw new TimeoutException($"No server API request for {path} after sequence {afterSequence}. URL: {page.Url}. Log: {fixture.ObservationPath}");
    }

    /// <summary>Reads the current browser JWT expiration and fingerprint, never mutating cookies.</summary>
    /// <returns>The current token expiry and a SHA-256 fingerprint.</returns>
    public async Task<(DateTimeOffset Expires, string Fingerprint)> ReadBrowserTokenAsync()
    {
        var cookie = (await page.Context.CookiesAsync()).Single(c => c.Name == "FinanceManager.Auth");
        Record($"browser-token exp={new JwtSecurityTokenHandler().ReadJwtToken(cookie.Value).ValidTo:O}");
        return (new DateTimeOffset(new JwtSecurityTokenHandler().ReadJwtToken(cookie.Value).ValidTo, TimeSpan.Zero),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie.Value))));
    }

    private void Record(string message) => File.AppendAllText(fixture.ObservationPath + ".timeline", $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
}
