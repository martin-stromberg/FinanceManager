using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Postings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Unit tests for the storno column rendered by <see cref="AccountPostingsListViewModel"/>
/// (via the shared <c>BuildRecords</c> logic in <c>BasePostingsListViewModel</c>).
/// Storno column is the last cell (index 7) in every <see cref="ListRecord"/>.
/// </summary>
public sealed class PostingsListReversalColumnTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Infrastructure helpers
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static AccountPostingsListViewModel CreateVm(Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        return new AccountPostingsListViewModel(sp, Guid.NewGuid());
    }

    private static PostingServiceDto BuildPostingDto(bool isReversed = false, bool isReversal = false)
    {
        return new PostingServiceDto(
            Id: Guid.NewGuid(),
            BookingDate: new DateTime(2025, 2, 1),
            ValutaDate: new DateTime(2025, 2, 1),
            Amount: 100m,
            Kind: PostingKind.Bank,
            AccountId: Guid.NewGuid(),
            ContactId: null,
            SavingsPlanId: null,
            SecurityId: null,
            SourceId: Guid.NewGuid(),
            Subject: "Subject",
            RecipientName: "Recipient",
            Description: null,
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
            IsReversed: isReversed,
            IsReversal: isReversal,
            ReversedByPostingId: null,
            ReversalForPostingId: null);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// L33 – The storno cell displays "✓" for a posting that is itself a reversal.
    /// </summary>
    [Fact]
    public async Task BuildRecords_StornoCell_ShouldShowCheckmark_ForReversalPosting()
    {
        // Arrange
        var apiMock = new Mock<IApiClient>();
        var postingDto = BuildPostingDto(isReversal: true);
        apiMock.Setup(a => a.Postings_GetAccountAsync(
                It.IsAny<Guid>(), 0, It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<PostingServiceDto> { postingDto });

        var vm = CreateVm(apiMock);

        // Act
        await vm.InitializeAsync();

        // Assert – storno column is last cell (index 7)
        vm.Records.Should().HaveCount(1);
        var stornoCell = vm.Records[0].Cells.Last();
        stornoCell.Text.Should().Be("✓", "a reversal posting is marked with a check");
    }

    /// <summary>
    /// L34 – The storno cell displays "—" for a posting that has been reversed (but is not itself a reversal).
    /// </summary>
    [Fact]
    public async Task BuildRecords_StornoCell_ShouldShowDash_ForReversedPosting()
    {
        // Arrange
        var apiMock = new Mock<IApiClient>();
        var postingDto = BuildPostingDto(isReversed: true);
        apiMock.Setup(a => a.Postings_GetAccountAsync(
                It.IsAny<Guid>(), 0, It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<PostingServiceDto> { postingDto });

        var vm = CreateVm(apiMock);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.Records.Should().HaveCount(1);
        var stornoCell = vm.Records[0].Cells.Last();
        stornoCell.Text.Should().Be("—", "a posting that has been reversed is marked with an em-dash");
    }

    /// <summary>
    /// L35 – The storno cell is empty for a normal posting (neither reversed nor a reversal).
    /// </summary>
    [Fact]
    public async Task BuildRecords_StornoCell_ShouldBeEmpty_ForNormalPosting()
    {
        // Arrange
        var apiMock = new Mock<IApiClient>();
        var postingDto = BuildPostingDto(isReversed: false, isReversal: false);
        apiMock.Setup(a => a.Postings_GetAccountAsync(
                It.IsAny<Guid>(), 0, It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<PostingServiceDto> { postingDto });

        var vm = CreateVm(apiMock);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.Records.Should().HaveCount(1);
        var stornoCell = vm.Records[0].Cells.Last();
        stornoCell.Text.Should().BeNullOrEmpty("a normal posting has no storno indicator");
    }
}
