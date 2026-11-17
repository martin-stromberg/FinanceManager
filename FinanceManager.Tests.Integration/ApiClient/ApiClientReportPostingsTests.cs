using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Postings;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the report postings drill-down endpoint (<c>POST /api/report-postings</c>):
/// verifies that the postings of a report cell sum up to the aggregate cell value, that foreign-owner
/// data is never returned, and that malformed cell coordinates are rejected with HTTP 400.
/// </summary>
public class ApiClientReportPostingsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientReportPostingsTests(TestWebApplicationFactory factory)
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

    private async Task<string> EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
        return username;
    }

    private async Task<Guid> GetUserIdAsync(string username)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.UserName == username);
        return user.Id;
    }

    private async Task<Account> SeedAccountWithPostingsAsync(Guid ownerUserId, string name, DateTime month, params decimal[] amounts)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = new Account(ownerUserId, AccountType.Giro, name, null, Guid.NewGuid());
        db.Accounts.Add(account);
        var day = 1;
        foreach (var amount in amounts)
        {
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null, month.AddDays(day++), amount));
        }
        // the aggregate endpoint reads PostingAggregates; keep it consistent with the raw postings
        var agg = new PostingAggregate(PostingKind.Bank, account.Id, null, null, null, month, AggregatePeriod.Month);
        foreach (var amount in amounts)
        {
            agg.Add(amount);
        }
        db.PostingAggregates.Add(agg);
        await db.SaveChangesAsync();
        return account;
    }

    /// <summary>
    /// Verifies the full server-to-API round-trip: the postings returned for a cell sum exactly to the
    /// value the aggregates endpoint reports for the same cell.
    /// </summary>
    [Fact]
    public async Task ReportPostings_SumMatchesAggregateCell()
    {
        var api = CreateClient();
        var username = await EnsureAuthenticatedAsync(api);
        var userId = await GetUserIdAsync(username);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var account = await SeedAccountWithPostingsAsync(userId, "Giro", month, 100m, 50m, -20m);

        var agg = await api.Reports_QueryAggregatesAsync(new ReportAggregatesQueryRequest(
            PostingKind: PostingKind.Bank, Interval: ReportInterval.Month, Take: 6,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: false,
            AnalysisDate: month, PostingKinds: null, Filters: null, UseValutaDate: false),
            TestContext.Current.CancellationToken);
        var cell = agg.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == month);
        cell.Amount.Should().Be(130m);

        var result = await api.Reports_QueryPostingsAsync(new ReportPostingsQueryRequest(
            GroupKey: $"Account:{account.Id}", PeriodStart: month, Period: ReportPostingsPeriod.Current,
            PostingKind: PostingKind.Bank, Interval: ReportInterval.Month, AnalysisDate: month),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Truncated.Should().BeFalse();
        result.TotalCount.Should().Be(3);
        result.Postings.Sum(p => p.Amount).Should().Be(cell.Amount);
        result.TotalAmount.Should().Be(cell.Amount);
        result.Postings.Should().OnlyContain(p => p.EntityName == "Giro");
    }

    /// <summary>
    /// Verifies that postings of entities owned by another user are never returned - the cell query
    /// is filtered by the server-side owner context.
    /// </summary>
    [Fact]
    public async Task ReportPostings_ForeignUserData_IsNotReturned()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var otherUserId = Guid.NewGuid();
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var foreignAccount = await SeedAccountWithPostingsAsync(otherUserId, "Fremd", month, 999m);

        var result = await api.Reports_QueryPostingsAsync(new ReportPostingsQueryRequest(
            GroupKey: $"Account:{foreignAccount.Id}", PeriodStart: month, Period: ReportPostingsPeriod.Current,
            PostingKind: PostingKind.Bank, Interval: ReportInterval.Month, AnalysisDate: month),
            TestContext.Current.CancellationToken);

        result.TotalCount.Should().Be(0);
        result.Postings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that an unknown group-key prefix is rejected with a bad-request error instead of being
    /// silently interpreted.
    /// </summary>
    [Fact]
    public async Task ReportPostings_InvalidGroupKey_Returns400()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var act = () => api.Reports_QueryPostingsAsync(new ReportPostingsQueryRequest(
            GroupKey: "Bogus:123", PeriodStart: month, Period: ReportPostingsPeriod.Current,
            PostingKind: PostingKind.Bank, Interval: ReportInterval.Month, AnalysisDate: month),
            TestContext.Current.CancellationToken);

        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies that comparison columns are rejected for the all-history interval (400) because
    /// previous/year-ago values do not exist there.
    /// </summary>
    [Fact]
    public async Task ReportPostings_AllHistoryWithPreviousPeriod_Returns400()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var act = () => api.Reports_QueryPostingsAsync(new ReportPostingsQueryRequest(
            GroupKey: null, PeriodStart: month, Period: ReportPostingsPeriod.Previous,
            PostingKind: PostingKind.Bank, Interval: ReportInterval.AllHistory, AnalysisDate: month),
            TestContext.Current.CancellationToken);

        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies that undefined enum members and a negative take are rejected with 400 instead of
    /// being silently coerced into a default interpretation.
    /// </summary>
    [Fact]
    public async Task ReportPostings_UndefinedEnumsAndNegativeTake_Return400()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var requests = new[]
        {
            new ReportPostingsQueryRequest(null, month, ReportPostingsPeriod.Current, PostingKind.Bank, (ReportInterval)99, AnalysisDate: month),
            new ReportPostingsQueryRequest(null, month, ReportPostingsPeriod.Current, (PostingKind)7, ReportInterval.Month, AnalysisDate: month),
            new ReportPostingsQueryRequest(null, month, ReportPostingsPeriod.Current, PostingKind.Bank, ReportInterval.Month, AnalysisDate: month, PostingKinds: new[] { PostingKind.Bank, (PostingKind)7 }),
            new ReportPostingsQueryRequest(null, month, ReportPostingsPeriod.Current, PostingKind.Bank, ReportInterval.Month, AnalysisDate: month, SavingsPlanPostingSource: (SavingsPlanPostingSource)9),
            new ReportPostingsQueryRequest(null, month, ReportPostingsPeriod.Current, PostingKind.Bank, ReportInterval.Month, AnalysisDate: month, Take: -1),
            new ReportPostingsQueryRequest(null, month, (ReportPostingsPeriod)9, PostingKind.Bank, ReportInterval.Month, AnalysisDate: month)
        };

        foreach (var req in requests)
        {
            var act = () => api.Reports_QueryPostingsAsync(req, TestContext.Current.CancellationToken);
            var ex = await act.Should().ThrowAsync<HttpRequestException>();
            ex.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }
    }

    /// <summary>
    /// Verifies that numeric names of undefined enum members inside group keys ("Type:5",
    /// "Security:{id}:5") are rejected with 400 - Enum.TryParse alone would accept them.
    /// </summary>
    [Fact]
    public async Task ReportPostings_NumericGroupKeyNames_Return400()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var requests = new[]
        {
            new ReportPostingsQueryRequest("Type:5", month, ReportPostingsPeriod.Current, PostingKind.Bank, ReportInterval.Month, AnalysisDate: month),
            new ReportPostingsQueryRequest($"Security:{Guid.NewGuid()}:5", month, ReportPostingsPeriod.Current, PostingKind.Security, ReportInterval.Month, AnalysisDate: month),
            new ReportPostingsQueryRequest("Category:9:_none", month, ReportPostingsPeriod.Current, PostingKind.Contact, ReportInterval.Month, AnalysisDate: month)
        };

        foreach (var req in requests)
        {
            var act = () => api.Reports_QueryPostingsAsync(req, TestContext.Current.CancellationToken);
            var ex = await act.Should().ThrowAsync<HttpRequestException>();
            ex.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }
    }

    /// <summary>
    /// Verifies that a sub-typed Security:{id}:{subType} key is rejected with 400 when the
    /// netted-dividend path is active - netted cells only ever carry the plain entity key.
    /// </summary>
    [Fact]
    public async Task ReportPostings_SubTypeKeyInNettedMode_Returns400()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var req = new ReportPostingsQueryRequest(
            $"Security:{Guid.NewGuid()}:{SecurityPostingSubType.Sell}", month, ReportPostingsPeriod.Current,
            PostingKind.Security, ReportInterval.Month, AnalysisDate: month,
            Filters: new ReportAggregatesFiltersRequest(null, null, null, null, null, null, null, null, true));

        var act = () => api.Reports_QueryPostingsAsync(req, TestContext.Current.CancellationToken);
        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
