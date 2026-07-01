using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Tests.Web.ViewModels.Contacts;

public sealed class ContactCardViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizerGeneric<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => (IStringLocalizer)this;
    }

    private static FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel CreateVm(Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(apiMock.Object);
        services.AddSingleton<IStringLocalizer<FinanceManager.Web.Pages>>(new PassthroughLocalizerGeneric<FinanceManager.Web.Pages>());
        var sp = services.BuildServiceProvider();
        return new FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel(sp);
    }

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
