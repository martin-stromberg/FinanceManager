using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end test for the admin user-management API, covering both the happy path for an admin
/// (create/get/list/update/reset-password/unlock/delete) and the authorization boundary: non-admin and
/// anonymous callers must be rejected and must never be able to mutate user records.
/// </summary>
public class ApiClientUsersAdminTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiClientUsersAdminTests"/> class.
    /// </summary>
    /// <param name="factory">Shared web application factory providing the in-memory test server.</param>
    public ApiClientUsersAdminTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>
    /// Walks a user account through the full admin lifecycle as an authenticated admin: create, get by
    /// id, appear in the list, rename and activate via update, reset password, unlock, and delete - the
    /// baseline regression guard for the admin user-management CRUD contract.
    /// </summary>
    [Fact]
    public async Task Admin_CreateListUpdateDelete_User()
    {
        var api = CreateClient();
        await api.Auth_LoginAsync(new LoginRequest(TestWebApplicationFactory.BootstrapAdminUsername, TestWebApplicationFactory.BootstrapAdminPassword, null, null), TestContext.Current.CancellationToken);

        // Create user (min length >= 3)
        var created = await api.Admin_CreateUserAsync(new CreateUserRequest("user1", "Secret123", IsAdmin: false), TestContext.Current.CancellationToken);
        created.Username.Should().Be("user1");

        // Get single user
        var fetched = await api.Admin_GetUserAsync(created.Id, TestContext.Current.CancellationToken);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Username.Should().Be("user1");

        // List contains new user
        var users = await api.Admin_ListUsersAsync(TestContext.Current.CancellationToken);
        users.Should().Contain(u => u.Username == "user1");

        // Update
        var updated = await api.Admin_UpdateUserAsync(created.Id, new UpdateUserRequest("user1x", false, true, null), TestContext.Current.CancellationToken);
        updated!.Username.Should().Be("user1x");
        updated.Active.Should().BeTrue();

        // Reset password
        var okReset = await api.Admin_ResetPasswordAsync(created.Id, new ResetPasswordRequest("Newpass123"), TestContext.Current.CancellationToken);
        okReset.Should().BeTrue();

        // Unlock
        var okUnlock = await api.Admin_UnlockUserAsync(created.Id, TestContext.Current.CancellationToken);
        okUnlock.Should().BeTrue();

        // Delete
        var okDel = await api.Admin_DeleteUserAsync(created.Id, TestContext.Current.CancellationToken);
        okDel.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that every admin user-management endpoint (list, get, create, update, reset-password,
    /// unlock, delete) rejects an authenticated but non-admin caller with Forbidden - guards the
    /// authorization boundary around the admin surface.
    /// </summary>
    [Fact]
    public async Task NonAdmin_UserAdminEndpoints_ReturnForbidden()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var api = new FinanceManager.Shared.ApiClient(http);
        await api.Auth_RegisterAsync(new RegisterRequest($"regular-{Guid.NewGuid():N}", "Secret123", PreferredLanguage: null, TimeZoneId: null), TestContext.Current.CancellationToken);

        var id = Guid.NewGuid();
        var responses = new[]
        {
            await http.GetAsync("/api/admin/users", TestContext.Current.CancellationToken),
            await http.GetAsync($"/api/admin/users/{id}", TestContext.Current.CancellationToken),
            await http.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("blocked-user", "Secret123", IsAdmin: false), cancellationToken: TestContext.Current.CancellationToken),
            await http.PutAsJsonAsync($"/api/admin/users/{id}", new UpdateUserRequest("blocked-user", false, true, null), cancellationToken: TestContext.Current.CancellationToken),
            await http.PostAsJsonAsync($"/api/admin/users/{id}/reset-password", new ResetPasswordRequest("Newpass123"), cancellationToken: TestContext.Current.CancellationToken),
            await http.PostAsync($"/api/admin/users/{id}/unlock", content: null, cancellationToken: TestContext.Current.CancellationToken),
            await http.DeleteAsync($"/api/admin/users/{id}", TestContext.Current.CancellationToken)
        };

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// Verifies that a rejected non-admin create/update attempt has no side effects at all: the attempted
    /// new user never appears in the admin list, and a protected existing user's data remains unchanged -
    /// a stronger guarantee than a Forbidden status code alone, since it rules out partial writes slipping
    /// through before the authorization check.
    /// </summary>
    [Fact]
    public async Task NonAdmin_CreateAndUpdate_DoNotPersistChanges()
    {
        var nonAdminHttp = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var nonAdminApi = new FinanceManager.Shared.ApiClient(nonAdminHttp);
        var regularName = $"regular-{Guid.NewGuid():N}";
        await nonAdminApi.Auth_RegisterAsync(new RegisterRequest(regularName, "Secret123", PreferredLanguage: null, TimeZoneId: null), TestContext.Current.CancellationToken);

        var adminApi = CreateClient();
        await adminApi.Auth_LoginAsync(new LoginRequest(TestWebApplicationFactory.BootstrapAdminUsername, TestWebApplicationFactory.BootstrapAdminPassword, null, null), TestContext.Current.CancellationToken);
        var protectedUser = await adminApi.Admin_CreateUserAsync(new CreateUserRequest($"protected-{Guid.NewGuid():N}", "Secret123", IsAdmin: false), TestContext.Current.CancellationToken);

        var blockedCreateName = $"blocked-{Guid.NewGuid():N}";
        var createResponse = await nonAdminHttp.PostAsJsonAsync("/api/admin/users", new CreateUserRequest(blockedCreateName, "Secret123", IsAdmin: false), cancellationToken: TestContext.Current.CancellationToken);
        var updateResponse = await nonAdminHttp.PutAsJsonAsync($"/api/admin/users/{protectedUser.Id}", new UpdateUserRequest("changed-by-non-admin", false, true, null), cancellationToken: TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var users = await adminApi.Admin_ListUsersAsync(TestContext.Current.CancellationToken);
        users.Should().NotContain(u => u.Username == blockedCreateName);
        users.Should().ContainSingle(u => u.Id == protectedUser.Id)
            .Which.Username.Should().Be(protectedUser.Username);
    }

    /// <summary>
    /// Verifies that an unauthenticated caller gets Unauthorized (not Forbidden) from the admin user list
    /// endpoint - distinguishes "not logged in" from "logged in but lacking permission".
    /// </summary>
    [Fact]
    public async Task Anonymous_UserAdminEndpoint_ReturnsUnauthorized()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await http.GetAsync("/api/admin/users", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
