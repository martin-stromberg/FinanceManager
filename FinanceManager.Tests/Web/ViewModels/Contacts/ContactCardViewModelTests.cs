using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.Common;

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
}
