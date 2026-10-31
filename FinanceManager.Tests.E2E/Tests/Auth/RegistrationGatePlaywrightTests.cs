using System.Text.Json;

namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end coverage for the self-registration gate (#456): with at least one existing user and
/// <c>Auth:AllowSelfRegistration=false</c> the <c>/register</c> page redirects to <c>/login</c> and
/// the registration API rejects further sign-ups with 403 and the stable
/// <c>Err_RegistrationDisabled</c> code.
/// </summary>
[Collection(RegistrationGateCollection.Name)]
public sealed class RegistrationGatePlaywrightTests
{
    private readonly RegistrationGateWebAppFixture _fixture;

    /// <summary>Initializes the test with the shared disabled-registration host.</summary>
    /// <param name="fixture">Shared web app fixture running with self-registration disabled.</param>
    public RegistrationGatePlaywrightTests(RegistrationGateWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>An anonymous visitor on /register is redirected to /login once users exist.</summary>
    [Fact]
    public async Task RegisterPage_WhenSelfRegistrationDisabled_ShouldRedirectToLogin()
    {
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        await seed.EnsureUserAsync($"gate-user-{Guid.NewGuid():N}", "Secret123");

        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;

        await page.GotoAsync("/register");
        await page.WaitForFunctionAsync("() => location.pathname.toLowerCase() === '/login'");

        page.Url.Should().Contain("/login");
    }

    /// <summary>The registration API rejects a non-first-user sign-up with 403 and the stable error code.</summary>
    [Fact]
    public async Task RegisterApi_WhenSelfRegistrationDisabled_ShouldReturnForbidden()
    {
        var seed = new TestUserSeeder(_fixture.DatabasePath);
        await seed.EnsureUserAsync($"gate-api-user-{Guid.NewGuid():N}", "Secret123");

        await using var session = await _fixture.CreateSessionAsync();
        var page = session.Page;
        await page.GotoAsync("/login");

        var outcome = await page.EvaluateAsync<JsonElement>(
            """
            async () => {
                const response = await fetch('/api/auth/register', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': 'e2e' },
                    body: JSON.stringify({ username: 'blocked-user', password: 'Secret123' })
                });
                return { status: response.status, body: await response.text() };
            }
            """);

        outcome.GetProperty("status").GetInt32().Should().Be(403);
        outcome.GetProperty("body").GetString().Should().Contain("Err_RegistrationDisabled");
    }
}
