using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Infrastructure;

namespace FinanceManager.Tests.Integration.ApiClient;

public class ApiClientDemoDataTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

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
            var user = await db.Users.FirstAsync(u => u.UserName == username);
            userId = user.Id;
        }

        // request demo data creation
        await api.Users_CreateDemoDataAsync(userId, true);

        // Verify that accounts were created for user
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var accounts = await db.Accounts.Where(a => a.OwnerUserId == userId).ToListAsync();
            Assert.True(accounts.Count >= 3); // one giro + two savings
        }
    }
}
