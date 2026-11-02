using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Integration coverage for the <c>auth-sensitive</c> rate-limiting policy on the anonymous
/// registration and user-existence endpoints.
/// </summary>
public sealed class AuthRateLimitingTests
{
    private static TestWebApplicationFactory CreateLimitedFactory()
    {
        var factory = new TestWebApplicationFactory();
        factory.ConfigurationOverrides["RateLimiting:AuthSensitive:PermitLimit"] = "2";
        factory.ConfigurationOverrides["RateLimiting:AuthSensitive:WindowSeconds"] = "300";
        return factory;
    }

    /// <summary>The anonymous user-existence probe returns 429 once the permit limit is exhausted.</summary>
    [Fact]
    public async Task UsersExists_RateLimited_Returns429()
    {
        using var factory = CreateLimitedFactory();
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var response = await client.GetAsync("/api/users/exists", TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    /// <summary>The anonymous registration endpoint returns 429 once the permit limit is exhausted.</summary>
    [Fact]
    public async Task Register_RateLimited_Returns429()
    {
        using var factory = CreateLimitedFactory();
        using var client = factory.CreateClient();

        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 4; i++)
        {
            var content = new StringContent(
                $"{{\"username\":\"u{i}_{Guid.NewGuid():N}\",\"password\":\"Secret123\"}}",
                System.Text.Encoding.UTF8,
                "application/json");
            using var response = await client.PostAsync("/api/auth/register", content, TestContext.Current.CancellationToken);
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
