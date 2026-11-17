using FinanceManager.Application;
using FinanceManager.Domain.Attachments;
using FinanceManager.Shared;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Postings.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Unit tests for the linked-information ribbon actions in <see cref="PostingsCardViewModel"/>.
/// Covers the URL payloads fired via the generic <c>OpenPostings</c> action, the
/// <c>OpenAttachments</c> overlay spec, and the disabled-state constellations of the five actions.
/// </summary>
public sealed class PostingsCardViewModelLinkedTests
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
        Guid? accountId = null,
        Guid? contactId = null,
        Guid? savingsPlanId = null,
        Guid? securityId = null,
        bool withAccount = true)
    {
        if (withAccount)
        {
            accountId ??= Guid.NewGuid();
        }

        return new PostingServiceDto(
            Id: id ?? Guid.NewGuid(),
            BookingDate: new DateTime(2025, 1, 15),
            ValutaDate: new DateTime(2025, 1, 15),
            Amount: 100m,
            Kind: PostingKind.Bank,
            AccountId: accountId,
            ContactId: contactId,
            SavingsPlanId: savingsPlanId,
            SecurityId: securityId,
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
            IsReversed: false,
            IsReversal: false,
            ReversedByPostingId: null,
            ReversalForPostingId: null);
    }

    private static UiRibbonAction? FindLinkedAction(PostingsCardViewModel vm, string id)
    {
        var localizer = new DummyLocalizer();
        var registers = vm.GetRibbonRegisters(localizer);
        return registers?
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .SelectMany(t => t.Items ?? Enumerable.Empty<UiRibbonAction>())
            .SingleOrDefault(a => a.Id == id);
    }

    private static List<BaseViewModel.UiActionEventArgs> CaptureUiActions(PostingsCardViewModel vm)
    {
        var events = new List<BaseViewModel.UiActionEventArgs>();
        vm.UiActionRequested += (_, e) =>
        {
            if (e != null)
            {
                events.Add(e);
            }
        };
        return events;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The "OpenAccount" ribbon action fires the generic "OpenPostings" action with the
    /// account card URL as payload.
    /// </summary>
    [Fact]
    public async Task OpenAccount_ShouldFireOpenPostings_WithAccountCardUrl()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(id: postingId, accountId: accountId));

        var events = CaptureUiActions(vm);
        await vm.InitializeAsync(postingId);

        // Act
        var action = FindLinkedAction(vm, "OpenAccount");
        action.Should().NotBeNull();
        await action!.Callback!();

        // Assert
        var evt = events.Should().ContainSingle().Which;
        evt.Action.Should().Be("OpenPostings");
        evt.Payload.Should().StartWith($"/card/accounts/{accountId}");
    }

    /// <summary>
    /// The "OpenContact" ribbon action fires the generic "OpenPostings" action with the
    /// contact card URL as payload.
    /// </summary>
    [Fact]
    public async Task OpenContact_ShouldFireOpenPostings_WithContactCardUrl()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(id: postingId, contactId: contactId));

        var events = CaptureUiActions(vm);
        await vm.InitializeAsync(postingId);

        // Act
        var action = FindLinkedAction(vm, "OpenContact");
        action.Should().NotBeNull();
        await action!.Callback!();

        // Assert
        var evt = events.Should().ContainSingle().Which;
        evt.Action.Should().Be("OpenPostings");
        evt.Payload.Should().StartWith($"/card/contacts/{contactId}");
    }

    /// <summary>
    /// The "OpenSavingsPlan" ribbon action fires the generic "OpenPostings" action with the
    /// savings plan card URL as payload.
    /// </summary>
    [Fact]
    public async Task OpenSavingsPlan_ShouldFireOpenPostings_WithSavingsPlanCardUrl()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var savingsPlanId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(id: postingId, savingsPlanId: savingsPlanId));

        var events = CaptureUiActions(vm);
        await vm.InitializeAsync(postingId);

        // Act
        var action = FindLinkedAction(vm, "OpenSavingsPlan");
        action.Should().NotBeNull();
        await action!.Callback!();

        // Assert
        var evt = events.Should().ContainSingle().Which;
        evt.Action.Should().Be("OpenPostings");
        evt.Payload.Should().StartWith($"/card/savings-plans/{savingsPlanId}");
    }

    /// <summary>
    /// The "OpenSecurity" ribbon action fires the generic "OpenPostings" action with the
    /// security card URL as payload.
    /// </summary>
    [Fact]
    public async Task OpenSecurity_ShouldFireOpenPostings_WithSecurityCardUrl()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(id: postingId, securityId: securityId));

        var events = CaptureUiActions(vm);
        await vm.InitializeAsync(postingId);

        // Act
        var action = FindLinkedAction(vm, "OpenSecurity");
        action.Should().NotBeNull();
        await action!.Callback!();

        // Assert
        var evt = events.Should().ContainSingle().Which;
        evt.Action.Should().Be("OpenPostings");
        evt.Payload.Should().StartWith($"/card/securities/{securityId}");
    }

    /// <summary>
    /// The "OpenAttachments" ribbon action fires an "OpenAttachments" action whose payload object is a
    /// <see cref="BaseViewModel.UiOverlaySpec"/> describing the <see cref="AttachmentsPanel"/> for the
    /// loaded posting (<see cref="AttachmentEntityKind.Posting"/>, <c>ParentId</c> = posting id).
    /// </summary>
    [Fact]
    public async Task OpenAttachments_ShouldFireOverlaySpec_ForPosting()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(id: postingId));

        var events = CaptureUiActions(vm);
        await vm.InitializeAsync(postingId);

        // Act
        var action = FindLinkedAction(vm, "OpenAttachments");
        action.Should().NotBeNull();
        await action!.Callback!();

        // Assert
        var evt = events.Should().ContainSingle().Which;
        evt.Action.Should().Be("OpenAttachments");
        var spec = evt.PayloadObject.Should().BeOfType<BaseViewModel.UiOverlaySpec>().Which;
        spec.ComponentType.Should().Be(typeof(AttachmentsPanel));
        spec.Parameters.Should().NotBeNull();
        spec.Parameters!["ParentKind"].Should().Be(AttachmentEntityKind.Posting);
        spec.Parameters["ParentId"].Should().Be(postingId);
    }

    /// <summary>
    /// The linked navigation actions are disabled when their respective link id is not set on
    /// the loaded posting.
    /// </summary>
    [Fact]
    public async Task LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing()
    {
        // Arrange — posting without contact/savings-plan/security links
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(id: postingId));

        // Act
        await vm.InitializeAsync(postingId);

        // Assert — only the account link exists
        FindLinkedAction(vm, "OpenAccount")!.Disabled.Should().BeFalse("the account link is set");
        FindLinkedAction(vm, "OpenContact")!.Disabled.Should().BeTrue("the contact link is missing");
        FindLinkedAction(vm, "OpenSavingsPlan")!.Disabled.Should().BeTrue("the savings plan link is missing");
        FindLinkedAction(vm, "OpenSecurity")!.Disabled.Should().BeTrue("the security link is missing");
    }

    /// <summary>
    /// The linked navigation actions are enabled when their respective link id is set on
    /// the loaded posting.
    /// </summary>
    [Fact]
    public async Task LinkedNavigationActions_ShouldBeEnabled_WhenLinkPresent()
    {
        // Arrange — posting with all links set
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(
                    id: postingId,
                    contactId: Guid.NewGuid(),
                    savingsPlanId: Guid.NewGuid(),
                    securityId: Guid.NewGuid()));

        // Act
        await vm.InitializeAsync(postingId);

        // Assert — every linked navigation action is enabled
        FindLinkedAction(vm, "OpenAccount")!.Disabled.Should().BeFalse();
        FindLinkedAction(vm, "OpenContact")!.Disabled.Should().BeFalse();
        FindLinkedAction(vm, "OpenSavingsPlan")!.Disabled.Should().BeFalse();
        FindLinkedAction(vm, "OpenSecurity")!.Disabled.Should().BeFalse();
    }

    /// <summary>
    /// When no posting is loaded (for example after <c>InitializeAsync(Guid.Empty)</c>), all five
    /// linked actions — including "OpenAttachments" — are disabled so that no action can fire.
    /// </summary>
    [Fact]
    public async Task OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded()
    {
        // Arrange
        var (vm, _) = CreateVm();

        // Act
        await vm.InitializeAsync(Guid.Empty);

        // Assert
        FindLinkedAction(vm, "OpenAccount")!.Disabled.Should().BeTrue();
        FindLinkedAction(vm, "OpenContact")!.Disabled.Should().BeTrue();
        FindLinkedAction(vm, "OpenSavingsPlan")!.Disabled.Should().BeTrue();
        FindLinkedAction(vm, "OpenSecurity")!.Disabled.Should().BeTrue();
        FindLinkedAction(vm, "OpenAttachments")!.Disabled.Should().BeTrue(
            "no attachments overlay may be opened without a loaded posting");
    }

    /// <summary>
    /// Invoking a linked navigation callback directly while its link id is <c>null</c> (for example
    /// when the link was removed after the ribbon state was computed) raises no UI action at all.
    /// </summary>
    [Fact]
    public async Task LinkedNavigationCallback_ShouldNotFire_WhenLinkRemoved()
    {
        // Arrange — posting without a contact link: the OpenContact callback carries a null id
        var (vm, apiMock) = CreateVm();
        var postingId = Guid.NewGuid();
        apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BuildPostingDto(id: postingId, contactId: null));

        var events = CaptureUiActions(vm);
        await vm.InitializeAsync(postingId);

        // Act — invoke the callback directly, bypassing the disabled-button guard
        var action = FindLinkedAction(vm, "OpenContact");
        action.Should().NotBeNull();
        await action!.Callback!();

        // Assert
        events.Should().BeEmpty("a null link id must not raise any UI action");
    }
}
