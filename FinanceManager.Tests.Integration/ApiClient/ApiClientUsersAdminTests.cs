using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

public class ApiClientUsersAdminTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiClientUsersAdminTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return new FinanceManager.Shared.ApiClient(http);
    }

    [Fact]
    public async Task Admin_CreateListUpdateDelete_User()
    {
        var api = CreateClient();
        await api.Auth_LoginAsync(new LoginRequest(TestWebApplicationFactory.BootstrapAdminUsername, TestWebApplicationFactory.BootstrapAdminPassword, null, null));

        // Create user (min length >= 3)
        var created = await api.Admin_CreateUserAsync(new CreateUserRequest("user1", "Secret123", IsAdmin: false));
        created.Username.Should().Be("user1");

        // Get single user
        var fetched = await api.Admin_GetUserAsync(created.Id);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Username.Should().Be("user1");

        // List contains new user
        var users = await api.Admin_ListUsersAsync();
        users.Should().Contain(u => u.Username == "user1");

        // Update
        var updated = await api.Admin_UpdateUserAsync(created.Id, new UpdateUserRequest("user1x", false, true, null));
        updated!.Username.Should().Be("user1x");
        updated.Active.Should().BeTrue();

        // Reset password
        var okReset = await api.Admin_ResetPasswordAsync(created.Id, new ResetPasswordRequest("Newpass123"));
        okReset.Should().BeTrue();

        // Unlock
        var okUnlock = await api.Admin_UnlockUserAsync(created.Id);
        okUnlock.Should().BeTrue();

        // Delete
        var okDel = await api.Admin_DeleteUserAsync(created.Id);
        okDel.Should().BeTrue();
    }

    [Fact]
    public async Task NonAdmin_UserAdminEndpoints_ReturnForbidden()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var api = new FinanceManager.Shared.ApiClient(http);
        await api.Auth_RegisterAsync(new RegisterRequest($"regular-{Guid.NewGuid():N}", "Secret123", PreferredLanguage: null, TimeZoneId: null));

        var id = Guid.NewGuid();
        var responses = new[]
        {
            await http.GetAsync("/api/admin/users"),
            await http.GetAsync($"/api/admin/users/{id}"),
            await http.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("blocked-user", "Secret123", IsAdmin: false)),
            await http.PutAsJsonAsync($"/api/admin/users/{id}", new UpdateUserRequest("blocked-user", false, true, null)),
            await http.PostAsJsonAsync($"/api/admin/users/{id}/reset-password", new ResetPasswordRequest("Newpass123")),
            await http.PostAsync($"/api/admin/users/{id}/unlock", content: null),
            await http.DeleteAsync($"/api/admin/users/{id}")
        };

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task Anonymous_UserAdminEndpoint_ReturnsUnauthorized()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await http.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
