using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.ViewModels.Postings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Unit tests for the marks column rendered by <c>BasePostingsListViewModel</c>:
/// informal funding postings of account-linked savings plans and actual-balance reports
/// ("Istmeldung") show a flag text, regular postings an empty cell.
/// </summary>
public sealed class PostingsListMarksColumnTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static SavingsPlanPostingsListViewModel CreateVm(Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        return new SavingsPlanPostingsListViewModel(sp, Guid.NewGuid());
    }

    private static PostingServiceDto BuildPostingDto(bool informal = false, SavingsPlanPostingSubType? subType = null)
        => new(
            Id: Guid.NewGuid(),
            BookingDate: new DateTime(2025, 2, 1),
            ValutaDate: new DateTime(2025, 2, 1),
            Amount: 100m,
            Kind: PostingKind.SavingsPlan,
            AccountId: null,
            ContactId: null,
            SavingsPlanId: Guid.NewGuid(),
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
            IsReversed: false,
            IsReversal: false,
            ReversedByPostingId: null,
            ReversalForPostingId: null,
            IsPreliminary: false,
            Informal: informal,
            SavingsPlanSubType: subType);

    /// <summary>
    /// An informal funding posting shows the informal flag in the marks column.
    /// </summary>
    [Fact]
    public async Task BuildRecords_MarksCell_ShouldShowInformal_ForInformalPosting()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSavingsPlanAsync(
                It.IsAny<Guid>(), 0, It.IsAny<int>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<PostingServiceDto> { BuildPostingDto(informal: true) });
        var vm = CreateVm(apiMock);

        await vm.InitializeAsync();

        vm.Records.Should().HaveCount(1);
        vm.Records[0].Cells[7].Text.Should().Contain("informell");
    }

    /// <summary>
    /// An actual-balance report posting shows the Istmeldung flag in the marks column.
    /// </summary>
    [Fact]
    public async Task BuildRecords_MarksCell_ShouldShowActualReport_ForIstmeldung()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSavingsPlanAsync(
                It.IsAny<Guid>(), 0, It.IsAny<int>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<PostingServiceDto> { BuildPostingDto(subType: SavingsPlanPostingSubType.ActualReport) });
        var vm = CreateVm(apiMock);

        await vm.InitializeAsync();

        vm.Records.Should().HaveCount(1);
        // Without a localizer the cell falls back to the enum name of the sub type.
        vm.Records[0].Cells[7].Text.Should().Contain("ActualReport");
    }

    /// <summary>
    /// A regular posting has an empty marks cell.
    /// </summary>
    [Fact]
    public async Task BuildRecords_MarksCell_ShouldBeEmpty_ForRegularPosting()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Postings_GetSavingsPlanAsync(
                It.IsAny<Guid>(), 0, It.IsAny<int>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<PostingServiceDto> { BuildPostingDto() });
        var vm = CreateVm(apiMock);

        await vm.InitializeAsync();

        vm.Records.Should().HaveCount(1);
        vm.Records[0].Cells[7].Text.Should().BeNullOrEmpty();
    }
}
