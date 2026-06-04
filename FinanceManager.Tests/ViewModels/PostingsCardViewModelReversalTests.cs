using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Postings.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Unit tests for reversal-related behaviour in <see cref="PostingsCardViewModel"/>.
/// Covers ribbon state, ReverseAsync navigation, and error propagation.
/// </summary>
public sealed class PostingsCardViewModelReversalTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Infrastructure helpers
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class DummyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = new("11111111-0000-0000-0000-000000000001");
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (PostingsCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new PostingsCardViewModel(sp);
        return (vm, apiMock);
    }

    private static PostingServiceDto BuildPostingDto(
        Guid? id = null,
        bool isReversed = false,
        bool isReversal = false,
        Guid? reversedByPostingId = null,
        Guid? reversalForPostingId = null)
    {
        return new PostingServiceDto(
            Id: id ?? Guid.NewGuid(),
            BookingDate: new DateTime(2025, 1, 15),
            ValutaDate: new DateTime(2025, 1, 15),
            Amount: 100m,
            Kind: PostingKind.Bank,
            AccountId: Guid.NewGuid(),
            ContactId: null,
            SavingsPlanId: null,
            SecurityId: null,
            SourceId: Guid.NewGuid(),
            Subject: "Test Posting",
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
            ReversedByPostingId: reversedByPostingId,
            ReversalForPostingId: reversalForPostingId);
    }

    private static UiRibbonAction? FindReverseAction(PostingsCardViewModel vm)
    {
        var localizer = new DummyLocalizer();
        var registers = vm.GetRibbonRegisters(localizer);
        return registers?
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .SelectMany(t => t.Items ?? Enumerable.Empty<UiRibbonAction>())
            .SingleOrDefault(a => a.Id == "Reverse");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// L28 – Ribbon "Reverse" action is enabled (Disabled == false) when the posting is neither
    /// reversed nor a reversal itself.
    /// </summary>
    [Fact]
    public async Task GetRibbonRegisters_ReverseAction_ShouldBeEnabled_ForNormalPosting()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var postingDto = BuildPostingDto(id: postingId, isReversed: false, isReversal: false);
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(postingDto);

        // Act
        await vm.InitializeAsync(postingId);
        var reverseAction = FindReverseAction(vm);

        // Assert
        reverseAction.Should().NotBeNull();
        reverseAction!.Disabled.Should().BeFalse("the posting has not been reversed yet");
    }

    /// <summary>
    /// L29 – Ribbon "Reverse" action is disabled when the posting has already been reversed.
    /// </summary>
    [Fact]
    public async Task GetRibbonRegisters_ReverseAction_ShouldBeDisabled_WhenPostingIsReversed()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var reversedById = Guid.NewGuid();
        var postingDto = BuildPostingDto(id: postingId, isReversed: true, reversedByPostingId: reversedById);
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(postingDto);

        // Act
        await vm.InitializeAsync(postingId);
        var reverseAction = FindReverseAction(vm);

        // Assert
        reverseAction.Should().NotBeNull();
        reverseAction!.Disabled.Should().BeTrue("a reversed posting cannot be reversed again");
    }

    /// <summary>
    /// L30 – Ribbon "Reverse" action is disabled when the posting is itself a reversal posting.
    /// </summary>
    [Fact]
    public async Task GetRibbonRegisters_ReverseAction_ShouldBeDisabled_WhenPostingIsReversal()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var postingDto = BuildPostingDto(id: postingId, isReversal: true, reversalForPostingId: originalId);
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(postingDto);

        // Act
        await vm.InitializeAsync(postingId);
        var reverseAction = FindReverseAction(vm);

        // Assert
        reverseAction.Should().NotBeNull();
        reverseAction!.Disabled.Should().BeTrue("a reversal posting cannot itself be reversed");
    }

    /// <summary>
    /// L31 – When the API reversal call succeeds, a NavigateToPosting UiAction is raised with the new reversal id.
    /// </summary>
    [Fact]
    public async Task ReverseAsync_ShouldRaiseNavigateToPostingAction_OnSuccess()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var reversalId = new Guid("BBBBBBBB-0000-0000-0000-000000000001");
        var postingDto = BuildPostingDto(id: postingId);
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(postingDto);
        apiMock.Setup(a => a.Postings_ReverseAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ReversalResultDto(
                   ReversedPostingIds: new[] { postingId },
                   CreatedReversalIds: new[] { reversalId },
                   StatementImportId: Guid.NewGuid()));

        string? capturedAction = null;
        string? capturedPayload = null;
        vm.UiActionRequested += (_, e) => { capturedAction = e?.Action; capturedPayload = e?.Payload; };

        await vm.InitializeAsync(postingId);

        // Act – invoke the Reverse ribbon callback
        var reverseAction = FindReverseAction(vm);
        reverseAction.Should().NotBeNull();
        await reverseAction!.Callback!();

        // Assert
        capturedAction.Should().Be("NavigateToPosting");
        capturedPayload.Should().Be(reversalId.ToString());
    }

    /// <summary>
    /// L32 – When the API reversal call fails (returns null), LastError is populated from the API client's
    /// error properties and no navigation event is raised.
    /// </summary>
    [Fact]
    public async Task ReverseAsync_ShouldSetLastError_WhenApiFails()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var postingDto = BuildPostingDto(id: postingId);
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(postingDto);
        apiMock.Setup(a => a.Postings_ReverseAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((ReversalResultDto?)null);
        apiMock.Setup(a => a.LastError).Returns("Already reversed");
        apiMock.Setup(a => a.LastErrorCode).Returns("ALREADY_REVERSED");

        bool navigationRaised = false;
        vm.UiActionRequested += (_, e) =>
        {
            if (e?.Action == "NavigateToPosting") navigationRaised = true;
        };

        await vm.InitializeAsync(postingId);

        // Act – invoke the Reverse ribbon callback
        var reverseAction = FindReverseAction(vm);
        reverseAction.Should().NotBeNull();
        await reverseAction!.Callback!();

        // Assert
        vm.LastError.Should().Be("Already reversed");
        navigationRaised.Should().BeFalse("navigation must not occur when reversal fails");
    }
}
