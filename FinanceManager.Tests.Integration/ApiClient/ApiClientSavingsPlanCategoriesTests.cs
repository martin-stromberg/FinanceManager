using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the savings plan categories API, verifying the full CRUD lifecycle plus the
/// symbol-attachment set/clear operations through the real HTTP pipeline.
/// </summary>
public class ApiClientSavingsPlanCategoriesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientSavingsPlanCategoriesTests(TestWebApplicationFactory factory)
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
    /// Drives the complete savings plan category lifecycle - list, create, get, update, set/clear the symbol
    /// attachment, and delete - and confirms each step's effect is visible on the next read, including that a
    /// deleted category resolves to null afterwards.
    /// </summary>
    [Fact]
    public async Task SavingsPlanCategories_Flow_CRUD_And_Symbol()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // initial list
        var list = await api.SavingsPlanCategories_ListAsync(TestContext.Current.CancellationToken);
        list.Should().NotBeNull();

        // create
        var created = await api.SavingsPlanCategories_CreateAsync(new SavingsPlanCategoryDto { Name = "CatA" }, TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        created!.Name.Should().Be("CatA");

        // get
        var got = await api.SavingsPlanCategories_GetAsync(created.Id, TestContext.Current.CancellationToken);
        got.Should().NotBeNull();
        got!.Id.Should().Be(created.Id);

        // update
        var updated = await api.SavingsPlanCategories_UpdateAsync(created.Id, new SavingsPlanCategoryDto { Name = "CatB" }, TestContext.Current.CancellationToken);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("CatB");

        // set/clear symbol (no actual attachment, just expect not found=false semantics)
        var setOk = await api.SavingsPlanCategories_SetSymbolAsync(created.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);
        setOk.Should().BeTrue();
        var clearOk = await api.SavingsPlanCategories_ClearSymbolAsync(created.Id, TestContext.Current.CancellationToken);
        clearOk.Should().BeTrue();

        // delete
        var del = await api.SavingsPlanCategories_DeleteAsync(created.Id, TestContext.Current.CancellationToken);
        del.Should().BeTrue();
        var gone = await api.SavingsPlanCategories_GetAsync(created.Id, TestContext.Current.CancellationToken);
        gone.Should().BeNull();
    }
}
