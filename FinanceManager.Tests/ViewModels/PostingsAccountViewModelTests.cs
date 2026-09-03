using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <c>AccountPostingsListViewModel</c>'s paging behavior (first-page load, load-more with the
/// standard stop-when-below-page-size heuristic) and export URL composition from the active search/date filters.
/// </summary>
public sealed class PostingsAccountViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static IServiceProvider CreateSp(Mock<IApiClient> apiMock, bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = authenticated });
        services.AddSingleton(apiMock.Object);
        return services.BuildServiceProvider();
    }

    private static List<PostingServiceDto> CreatePostings(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new PostingServiceDto(
                Id: Guid.NewGuid(),
                BookingDate: DateTime.UtcNow.Date.AddDays(-i),
                ValutaDate: DateTime.UtcNow.Date.AddDays(-i),
                Amount: 100 + i,
                Kind: PostingKind.Bank,
                AccountId: Guid.NewGuid(),
                ContactId: null,
                SavingsPlanId: null,
                SecurityId: null,
                SourceId: Guid.NewGuid(),
                Subject: $"S{i}",
                RecipientName: $"R{i}",
                Description: $"D{i}",
                SecuritySubType: null,
                Quantity: null,
                GroupId: Guid.NewGuid(),
                LinkedPostingId: null,
                LinkedPostingKind: null,
                LinkedPostingAccountId: null,
                LinkedPostingAccountSymbolAttachmentId: null,
                LinkedPostingAccountName: null,
                BankPostingAccountId: null,
                BankPostingAccountSymbolAttachmentId: null,
                BankPostingAccountName: null,
                IsReversed: false,
                IsReversal: false,
                ReversedByPostingId: null,
                ReversalForPostingId: null))
            .ToList();
    }

    /// <summary>
    /// Verifies that initialization loads the first page of postings for the account and, since fewer
    /// items than the page size were returned, correctly infers there is no further page to load.
    /// </summary>
    [Fact]
    public async Task Initialize_LoadsFirstPage_SetsItemsAndFlags()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetAccountAsync(It.IsAny<Guid>(), 0, 50, It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePostings(10));

        var accountId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.AccountPostingsListViewModel(CreateSp(apiMock), accountId);
        await vm.InitializeAsync();

        Assert.False(vm.Loading);
        Assert.Equal(10, vm.Items.Count);
        // When fewer items than the page size are returned, there is no further page to load
        Assert.False(vm.CanLoadMore);
    }

    /// <summary>
    /// Verifies that loading more pages appends items to the existing collection rather than replacing
    /// it, and that the "can load more" flag flips off once a page comes back shorter than the fixed page
    /// size (50) - the paging termination condition the infinite-scroll UI relies on.
    /// </summary>
    [Fact]
    public async Task LoadMore_AppendsItems_StopsWhenBelowPageSize()
    {
        int call = 0;
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetAccountAsync(It.IsAny<Guid>(), It.IsAny<int>(), 50, It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                call++;
                return CreatePostings(call == 1 ? 50 : 3);
            });

        var accountId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.AccountPostingsListViewModel(CreateSp(apiMock), accountId);
        await vm.InitializeAsync();
        Assert.Equal(50, vm.Items.Count);
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreAsync();
        Assert.Equal(53, vm.Items.Count);
        Assert.False(vm.CanLoadMore);
    }

    /// <summary>
    /// Verifies that the export URL embeds the account id in its path and the active search text and
    /// date range as query parameters (URL-encoded), so the exported file matches exactly what the user
    /// currently sees filtered on screen.
    /// </summary>
    [Fact]
    public async Task GetExportUrl_ComposesQuery()
    {
        var apiMock = new Mock<IApiClient>();
        var accountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var vm = new FinanceManager.Web.ViewModels.Postings.AccountPostingsListViewModel(CreateSp(apiMock), accountId);
        vm.SetSearch("test q");
        vm.SetRange(new DateTime(2024, 1, 2), new DateTime(2024, 2, 3));
        await vm.InitializeAsync();

        var url = vm.GetExportUrl("csv");

        Assert.StartsWith("/api/postings/account/11111111-1111-1111-1111-111111111111/export", url);
        Assert.Contains("format=csv", url);
        Assert.Contains("q=test%20q", url);
        Assert.Contains("from=2024-01-02", url);
        Assert.Contains("to=2024-02-03", url);
    }
}
