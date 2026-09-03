using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the reports API: aggregate queries and the full CRUD lifecycle of report favorites
/// (create, read, update, delete), verifying that persisted favorite settings (such as
/// <c>CompareProjection</c>) round-trip correctly through the ApiClient.
/// </summary>
public class ApiClientReportsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientReportsTests(TestWebApplicationFactory factory)
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
    /// Verifies the aggregates query endpoint responds successfully for a minimal request, then drives the
    /// complete favorite lifecycle (create, get, update, delete) and confirms that a toggled flag such as
    /// <c>CompareProjection</c> is actually persisted and reflected on subsequent reads/updates, and that a
    /// deleted favorite is no longer retrievable.
    /// </summary>
    [Fact]
    public async Task Reports_Aggregates_And_Favorites_Flow()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        // Aggregates query minimal
        var aggReq = new ReportAggregatesQueryRequest(PostingKind: 0, Interval: 0, Take: 6, IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: false, AnalysisDate: null, PostingKinds: null, Filters: null, UseValutaDate: false);
        var agg = await api.Reports_QueryAggregatesAsync(aggReq, TestContext.Current.CancellationToken);
        agg.Should().NotBeNull();

        // List favorites initially empty
        var favs = await api.Reports_ListFavoritesAsync(TestContext.Current.CancellationToken);
        favs.Should().NotBeNull();

        // Create favorite
        var createReq = new ReportFavoriteCreateApiRequest
        {
            Name = $"Fav_{Guid.NewGuid():N}",
            PostingKind = 0,
            IncludeCategory = false,
            Interval = 0,
            Take = 6,
            ComparePrevious = false,
            CompareYear = false,
            CompareProjection = true,
            ShowChart = false,
            Expandable = false,
            UseValutaDate = false
        };
        var created = await api.Reports_CreateFavoriteAsync(createReq, TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        created.CompareProjection.Should().BeTrue();

        // Get by id
        var got = await api.Reports_GetFavoriteAsync(created.Id, TestContext.Current.CancellationToken);
        got.Should().NotBeNull();
        got!.Id.Should().Be(created.Id);
        got.CompareProjection.Should().BeTrue();

        // Update
        var updateReq = new ReportFavoriteUpdateApiRequest
        {
            Name = created.Name + "_X",
            PostingKind = created.PostingKind,
            IncludeCategory = created.IncludeCategory,
            Interval = (int)created.Interval,
            Take = created.Take,
            ComparePrevious = created.ComparePrevious,
            CompareYear = created.CompareYear,
            CompareProjection = false,
            ShowChart = created.ShowChart,
            Expandable = created.Expandable,
            UseValutaDate = created.UseValutaDate,
            PostingKinds = created.PostingKinds,
            Filters = created.Filters
        };
        var updated = await api.Reports_UpdateFavoriteAsync(created.Id, updateReq, TestContext.Current.CancellationToken);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be(createReq.Name + "_X");
        updated.CompareProjection.Should().BeFalse();

        // Delete
        var del = await api.Reports_DeleteFavoriteAsync(created.Id, TestContext.Current.CancellationToken);
        del.Should().BeTrue();
        var gone = await api.Reports_GetFavoriteAsync(created.Id, TestContext.Current.CancellationToken);
        gone.Should().BeNull();
    }
}
