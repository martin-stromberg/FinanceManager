using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <c>SecurityPostingsListViewModel</c>'s paging behavior and, in particular, the security-specific
/// "quantity" grid column: its position between the kind and amount columns, and how the quantity cell is
/// rendered for null, zero, and positive values (empty vs. a formatted number without spurious trailing zeros).
/// </summary>
public sealed class PostingsSecurityViewModelTests
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
                Kind: PostingKind.Security,
                AccountId: null,
                ContactId: null,
                SavingsPlanId: null,
                SecurityId: Guid.NewGuid(),
                SourceId: Guid.NewGuid(),
                Subject: $"S{i}",
                RecipientName: null,
                Description: $"D{i}",
                SecuritySubType: SecurityPostingSubType.Buy,
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
    /// Verifies that initialization loads a full page (50) of security postings and correctly infers
    /// there is a further page to load since the page came back exactly at the page-size limit.
    /// </summary>
    [Fact]
    public async Task Initialize_LoadsFirstPage_SetsItemsAndFlags()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSecurityAsync(It.IsAny<Guid>(), 0, 50, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePostings(50));

        var securityId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel(CreateSp(apiMock), securityId);
        await vm.InitializeAsync();

        Assert.False(vm.Loading);
        Assert.Equal(50, vm.Items.Count);
        Assert.True(vm.CanLoadMore);
    }

    /// <summary>
    /// Verifies that loading more pages appends to the existing collection and that "can load more" flips
    /// off once a page comes back shorter than the fixed page size (50).
    /// </summary>
    [Fact]
    public async Task LoadMore_AppendsItems_StopsWhenBelowPageSize()
    {
        int call = 0;
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSecurityAsync(It.IsAny<Guid>(), It.IsAny<int>(), 50, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                call++;
                return CreatePostings(call == 1 ? 50 : 5);
            });

        var securityId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel(CreateSp(apiMock), securityId);
        await vm.InitializeAsync();
        Assert.Equal(50, vm.Items.Count);
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreAsync();
        Assert.Equal(55, vm.Items.Count);
        Assert.False(vm.CanLoadMore);
    }

    // ── Column layout and Quantity rendering ────────────────────────────────

    /// <summary>
    /// Verifies that the security postings grid defines a "quantity" column and that it is positioned
    /// between "kind" and "amount" - a layout guarantee for the security-specific grid so the quantity
    /// figure reads naturally alongside the transaction kind and its monetary amount.
    /// </summary>
    [Fact]
    public void Columns_ContainsQuantityBetweenKindAndAmount()
    {
        var apiMock = new Mock<IApiClient>();
        var securityId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel(CreateSp(apiMock), securityId);

        var keys = vm.Columns.Select(c => c.Key).ToArray();

        // Quantity must exist
        Assert.Contains("quantity", keys);

        // Quantity must come after kind and before amount
        var kindIdx = Array.IndexOf(keys, "kind");
        var quantityIdx = Array.IndexOf(keys, "quantity");
        var amountIdx = Array.IndexOf(keys, "amount");

        Assert.True(kindIdx >= 0, "Column 'kind' must be present");
        Assert.True(quantityIdx >= 0, "Column 'quantity' must be present");
        Assert.True(amountIdx >= 0, "Column 'amount' must be present");
        Assert.True(quantityIdx > kindIdx, "Quantity must come after Kind");
        Assert.True(quantityIdx < amountIdx, "Quantity must come before Amount");
    }

    /// <summary>
    /// Verifies that a posting with no quantity (e.g. a fee or cash posting on a security account) renders
    /// an empty quantity cell rather than "0" or a null-reference placeholder, avoiding a misleading "zero units" reading.
    /// </summary>
    [Fact]
    public async Task BuildRecords_NullQuantity_QuantityCellIsEmpty()
    {
        var posting = CreateSinglePosting(quantity: null);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSecurityAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostingServiceDto> { posting });

        var securityId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel(CreateSp(apiMock), securityId);
        await vm.InitializeAsync();

        var record = Assert.Single(vm.Records);
        var qtyCell = GetQuantityCell(vm, record);
        Assert.Equal(string.Empty, qtyCell.Text);
    }

    /// <summary>
    /// Verifies that an explicit quantity of zero also renders an empty cell, the same as a null quantity,
    /// so the UI does not distinguish "not applicable" from "literally zero units" in this column.
    /// </summary>
    [Fact]
    public async Task BuildRecords_ZeroQuantity_QuantityCellIsEmpty()
    {
        var posting = CreateSinglePosting(quantity: 0m);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSecurityAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostingServiceDto> { posting });

        var securityId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel(CreateSp(apiMock), securityId);
        await vm.InitializeAsync();

        var record = Assert.Single(vm.Records);
        var qtyCell = GetQuantityCell(vm, record);
        Assert.Equal(string.Empty, qtyCell.Text);
    }

    /// <summary>
    /// Verifies that a real quantity value (12.5) is rendered as a formatted, non-empty string that does
    /// not carry excessive trailing zeros from the underlying decimal's storage precision (e.g. not "12,500000"),
    /// guarding against a naive ToString() leaking implementation detail into the UI.
    /// </summary>
    [Fact]
    public async Task BuildRecords_PositiveQuantity_QuantityCellShowsFormattedValue()
    {
        var posting = CreateSinglePosting(quantity: 12.5m);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSecurityAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostingServiceDto> { posting });

        var securityId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel(CreateSp(apiMock), securityId);
        await vm.InitializeAsync();

        var record = Assert.Single(vm.Records);
        var qtyCell = GetQuantityCell(vm, record);

        // Value must be non-empty and not contain excessive trailing zeros
        Assert.NotEmpty(qtyCell.Text!);
        Assert.DoesNotContain("500000", qtyCell.Text); // "12,500000" would fail
    }

    /// <summary>
    /// Verifies that every built record has exactly as many cells as the grid has columns, a structural
    /// invariant the rendering layer relies on to zip columns and cells by index without going out of bounds.
    /// </summary>
    [Fact]
    public async Task BuildRecords_RecordHasSameCellCountAsColumns()
    {
        var posting = CreateSinglePosting(quantity: 5m);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSecurityAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostingServiceDto> { posting });

        var securityId = Guid.NewGuid();
        var vm = new FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel(CreateSp(apiMock), securityId);
        await vm.InitializeAsync();

        var record = Assert.Single(vm.Records);
        Assert.Equal(vm.Columns.Count, record.Cells.Count);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PostingServiceDto CreateSinglePosting(decimal? quantity) =>
        new PostingServiceDto(
            Id: Guid.NewGuid(),
            BookingDate: DateTime.UtcNow.Date,
            ValutaDate: DateTime.UtcNow.Date,
            Amount: 100m,
            Kind: PostingKind.Security,
            AccountId: null,
            ContactId: null,
            SavingsPlanId: null,
            SecurityId: Guid.NewGuid(),
            SourceId: Guid.NewGuid(),
            Subject: "Test",
            RecipientName: null,
            Description: "Desc",
            SecuritySubType: SecurityPostingSubType.Buy,
            Quantity: quantity,
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
            ReversalForPostingId: null);

    /// <summary>Returns the Quantity cell from a record by looking up the column index.</summary>
    /// <param name="vm">The view model whose column layout determines the quantity column's index.</param>
    /// <param name="record">The record to pull the quantity cell from.</param>
    /// <returns>The <see cref="ListCell"/> at the "quantity" column's position.</returns>
    private static ListCell GetQuantityCell(
        FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel vm,
        ListRecord record)
    {
        var idx = vm.Columns.ToList().FindIndex(c => c.Key == "quantity");
        Assert.True(idx >= 0, "Column 'quantity' not found");
        return record.Cells[idx];
    }
}
