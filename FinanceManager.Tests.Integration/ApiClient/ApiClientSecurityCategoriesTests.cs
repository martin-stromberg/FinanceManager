using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end test for the security-category API, covering create/get/update, symbol assignment
/// (including the not-found case for a missing attachment) and delete.
/// </summary>
public class ApiClientSecurityCategoriesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiClientSecurityCategoriesTests"/> class.
    /// </summary>
    /// <param name="factory">Shared web application factory providing the in-memory test server.</param>
    public ApiClientSecurityCategoriesTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    /// <summary>
    /// Exercises the full security-category lifecycle end to end - list, create, get, update, symbol
    /// assignment against a nonexistent attachment (expected not-found), symbol clearing, and delete - to
    /// guard against a regression in any single step breaking the overall workflow.
    /// </summary>
    [Fact]
    public async Task SecurityCategories_List_Create_Update_Symbol_Delete_Flow()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // initial list
        var list = await api.SecurityCategories_ListAsync(TestContext.Current.CancellationToken);
        list.Should().NotBeNull();
        list.Should().BeEmpty();

        // create
        var created = await api.SecurityCategories_CreateAsync(new SecurityCategoryRequest { Name = "Tech" }, TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        created.Name.Should().Be("Tech");

        // get by id
        var got = await api.SecurityCategories_GetAsync(created.Id, TestContext.Current.CancellationToken);
        got.Should().NotBeNull();
        got!.Id.Should().Be(created.Id);
        got!.Name.Should().Be("Tech");

        // update name
        var updated = await api.SecurityCategories_UpdateAsync(created.Id, new SecurityCategoryRequest { Name = "Technology" }, TestContext.Current.CancellationToken);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Technology");

        // set symbol with non-existent attachment -> should be false (NotFound)
        var setOk = await api.SecurityCategories_SetSymbolAsync(created.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);
        setOk.Should().BeFalse();
        // clear symbol -> should be true even if none set
        var clearOk = await api.SecurityCategories_ClearSymbolAsync(created.Id, TestContext.Current.CancellationToken);
        clearOk.Should().BeTrue();

        // delete
        var delOk = await api.SecurityCategories_DeleteAsync(created.Id, TestContext.Current.CancellationToken);
        delOk.Should().BeTrue();
        var gone = await api.SecurityCategories_GetAsync(created.Id, TestContext.Current.CancellationToken);
        gone.Should().BeNull();
    }
}
