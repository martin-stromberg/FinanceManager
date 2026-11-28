using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Web.ViewModels.Contacts;

/// <summary>
/// Covers <c>ContactCardViewModel</c>'s interaction with the surrounding shell: when a contact
/// card is loaded, it must ask the host to open the alias panel as an embedded panel positioned
/// next to the card, carrying the correct component type and contact id.
/// </summary>
public sealed class ContactCardViewModelTests
{
    /// <summary>
    /// Minimal <see cref="ICurrentUserService"/> double, pre-set to authenticated, used to satisfy
    /// the view model's DI dependency without a real auth pipeline.
    /// </summary>
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        /// <inheritdoc />
        public Guid UserId { get; set; } = Guid.NewGuid();
        /// <inheritdoc />
        public string? PreferredLanguage { get; set; }
        /// <inheritdoc />
        public bool IsAuthenticated { get; set; } = true;
        /// <inheritdoc />
        public bool IsAdmin { get; set; }
    }

    /// <summary>
    /// <see cref="IStringLocalizer{T}"/> stub that echoes the requested resource key back as the
    /// localized value, satisfying <c>BaseViewModel</c>'s localizer dependency without real
    /// resource files.
    /// </summary>
    private sealed class PassthroughLocalizerGeneric<T> : IStringLocalizer<T>
    {
        /// <inheritdoc />
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        /// <inheritdoc />
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        /// <inheritdoc />
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        /// <inheritdoc />
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => (IStringLocalizer)this;
    }

    /// <summary>
    /// Non-generic <see cref="IStringLocalizer"/> stub echoing resource keys, used for ribbon lookups.
    /// </summary>
    private sealed class PassthroughLocalizer : IStringLocalizer
    {
        /// <inheritdoc />
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        /// <inheritdoc />
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        /// <inheritdoc />
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    /// <summary>
    /// Builds a <c>ContactCardViewModel</c> wired to a minimal DI container using the given API
    /// mock, so tests can control what the card loads without a real backend.
    /// </summary>
    /// <param name="apiMock">The mocked <see cref="IApiClient"/> to register for the view model to resolve.</param>
    /// <returns>A ready-to-use <c>ContactCardViewModel</c> instance.</returns>
    private static FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel CreateVm(Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(apiMock.Object);
        services.AddSingleton<IStringLocalizer<FinanceManager.Web.Pages>>(new PassthroughLocalizerGeneric<FinanceManager.Web.Pages>());
        var sp = services.BuildServiceProvider();
        return new FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel(sp);
    }

    /// <summary>
    /// Verifies that loading an existing contact raises a UI action requesting the alias panel be
    /// shown as an embedded panel positioned after the card, with the contact's id passed through -
    /// this is what makes the alias-management panel actually appear next to the contact card.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldRequestAliasPanel_WhenContactExists()
    {
        var apiMock = new Mock<IApiClient>();
        var contactId = Guid.NewGuid();
        apiMock.Setup(a => a.Contacts_GetAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(contactId, "Alice", ContactType.Person, null, null, false, null));

        var vm = CreateVm(apiMock);
        BaseViewModel.UiActionEventArgs? lastAction = null;
        vm.UiActionRequested += (_, e) => lastAction = e;

        await vm.LoadAsync(contactId);

        Assert.NotNull(lastAction);
        Assert.Equal("EmbeddedPanel", lastAction!.Action);
        var spec = Assert.IsType<BaseViewModel.EmbeddedPanelSpec>(lastAction.PayloadObject);
        Assert.Equal(typeof(FinanceManager.Web.Components.Pages.ContactDetail), spec.ComponentType);
        Assert.Equal(EmbeddedPanelPosition.AfterCard, spec.Position);
        Assert.Equal(contactId, Assert.IsType<Guid>(spec.Parameters!["ContactId"]));
    }

    /// <summary>
    /// The "Zurücksetzen" action must sit in the manage group directly behind Save as a small
    /// item with the undo icon, the localized Ribbon_Reset label, no mobile shortcut and a
    /// disabled state while no pending changes exist.
    /// </summary>
    [Fact]
    public async Task Ribbon_ContainsReset_AfterSave_InManageGroup()
    {
        var apiMock = new Mock<IApiClient>();
        var vm = CreateVm(apiMock);
        var loc = new PassthroughLocalizer();
        await vm.LoadAsync(Guid.Empty);

        var manage = FindTab(vm, loc, "Ribbon_Group_Manage");
        var ids = manage.Items.Select(i => i.Id).ToList();
        var saveIndex = ids.IndexOf("Save");
        Assert.True(saveIndex >= 0);
        Assert.Equal(saveIndex + 1, ids.IndexOf("Reset"));

        var reset = manage.Items[saveIndex + 1];
        Assert.Equal(UiRibbonItemSize.Small, reset.Size);
        Assert.Contains("#undo", reset.IconSvg);
        Assert.Equal("Ribbon_Reset", reset.Label);
        Assert.False(reset.MobileShortcut);
        Assert.True(reset.Disabled);
    }

    /// <summary>
    /// Invoking the Reset action must discard pending edits and rebuild the card record from
    /// the loaded contact DTO - without fetching the contact from the API again.
    /// </summary>
    [Fact]
    public async Task Reset_DiscardsPendingChanges_AndRestoresBaseline()
    {
        var apiMock = new Mock<IApiClient>();
        var contactId = Guid.NewGuid();
        apiMock.Setup(a => a.Contacts_GetAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(contactId, "Alice", ContactType.Person, null, null, false, null));

        var vm = CreateVm(apiMock);
        var loc = new PassthroughLocalizer();
        await vm.LoadAsync(contactId);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Contact_Name");
        vm.ValidateFieldValue(nameField, "Changed");
        Assert.True(vm.HasPendingChanges);

        var reset = FindAction(vm, loc, "Reset");
        Assert.False(reset.Disabled);
        await reset.Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("Alice", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Contact_Name").Text);
        apiMock.Verify(a => a.Contacts_GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// In new-create mode Reset must restore the initial state including the InitPrefill name
    /// and must not trigger any create/update/fetch API calls.
    /// </summary>
    [Fact]
    public async Task Reset_NewMode_RestoresPrefill_WithoutApiCalls()
    {
        var apiMock = new Mock<IApiClient>();
        var vm = CreateVm(apiMock);
        var loc = new PassthroughLocalizer();
        vm.SetInitValue("PrefillCo");
        await vm.LoadAsync(Guid.Empty);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Contact_Name");
        Assert.Equal("PrefillCo", nameField.Text);
        vm.ValidateFieldValue(nameField, "Other");
        Assert.True(vm.HasPendingChanges);

        await FindAction(vm, loc, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("PrefillCo", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Contact_Name").Text);
        apiMock.Verify(a => a.Contacts_GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        apiMock.Verify(a => a.Contacts_CreateAsync(It.IsAny<ContactCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UiRibbonTab FindTab(FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel vm, IStringLocalizer loc, string title)
        => vm.GetRibbonRegisters(loc)!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .Single(t => t.Title == title);

    private static UiRibbonAction FindAction(FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel vm, IStringLocalizer loc, string actionId)
        => vm.GetRibbonRegisters(loc)!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .SelectMany(t => t.Items)
            .Single(a => a.Id == actionId);
}
