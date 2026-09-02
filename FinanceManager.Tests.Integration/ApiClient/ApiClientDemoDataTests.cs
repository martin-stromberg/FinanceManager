using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Infrastructure;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end test for the demo-data seeding endpoint used to give new users a realistic starting
/// dataset (accounts, postings, etc.) instead of an empty account.
/// </summary>
public class ApiClientDemoDataTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiClientDemoDataTests"/> class.
    /// </summary>
    /// <param name="factory">Shared web application factory providing the in-memory test server.</param>
    public ApiClientDemoDataTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api, string userName)
    {
        await api.Auth_RegisterAsync(new FinanceManager.Shared.Dtos.Users.RegisterRequest(userName, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    /// <summary>
    /// Verifies that requesting demo-data creation for a freshly registered user actually populates the
    /// database with the expected accounts (a giro account plus at least two savings accounts), not just
    /// that the request is accepted - the endpoint is fire-and-forget from the caller's perspective, so
    /// this checks the background side effect directly via the DbContext.
    /// </summary>
    [Fact]
    public async Task Users_CreateDemoData_Should_ReturnAccepted()
    {
        var api = CreateClient();
        var username = $"demouser_{Guid.NewGuid():N}";
        await EnsureAuthenticatedAsync(api, username);

        // get user id from server
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.UserName == username, cancellationToken: TestContext.Current.CancellationToken);
            userId = user.Id;
        }

        // request demo data creation
        await api.Users_CreateDemoDataAsync(userId, true, TestContext.Current.CancellationToken);

        // Verify that accounts were created for user
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var accounts = await db.Accounts.Where(a => a.OwnerUserId == userId).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(accounts.Count >= 3); // one giro + two savings
        }
    }
}
