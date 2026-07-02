using Bunit;
using FinanceManager.Domain.Attachments;
using FinanceManager.Shared;
using FinanceManager.Web;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

public sealed class OverlayHostTests : BunitContext
{
    /// <summary>
    /// Verifies that an explicit overlay title parameter is rendered in the host header.
    /// </summary>
    [Fact]
    public void OverlayHost_ShouldRenderOverlayTitle_WhenProvidedInParameters()
    {
        Services.AddSingleton(Mock.Of<IApiClient>());
        Services.AddSingleton<IStringLocalizer<Pages>>(new PassthroughLocalizer<Pages>());

        var provider = new TestCardViewModel(Services);
        var cut = Render<OverlayHost<(string, string)>>(parameters => parameters
            .Add(p => p.Provider, provider)
            .Add(p => p.Localizer, new PassthroughLocalizer<Pages>()));

        provider.RaiseOverlay(new BaseViewModel.UiOverlaySpec(
            typeof(SecurityPriceImportPanel),
            new Dictionary<string, object?>
            {
                ["OverlayTitle"] = "Import prices custom",
                ["SecurityId"] = Guid.NewGuid()
            }));

        cut.WaitForAssertion(() => Assert.Equal("Import prices custom", cut.Find("h2").TextContent));
    }

    /// <summary>
    /// Verifies that title mapping falls back to localized import title for the import panel.
    /// </summary>
    [Fact]
    public void OverlayHost_ShouldUseLocalizedFallbackTitle_WhenOverlayTitleIsMissing()
    {
        Services.AddSingleton(Mock.Of<IApiClient>());
        Services.AddSingleton<IStringLocalizer<Pages>>(new PassthroughLocalizer<Pages>());

        var provider = new TestCardViewModel(Services);
        var cut = Render<OverlayHost<(string, string)>>(parameters => parameters
            .Add(p => p.Provider, provider)
            .Add(p => p.Localizer, new PassthroughLocalizer<Pages>()));

        provider.RaiseOverlay(new BaseViewModel.UiOverlaySpec(
            typeof(SecurityPriceImportPanel),
            new Dictionary<string, object?>
            {
                ["SecurityId"] = Guid.NewGuid()
            }));

        cut.WaitForAssertion(() => Assert.Equal("SecurityPricesImport_Title", cut.Find("h2").TextContent));
    }

    private sealed class TestCardViewModel : BaseCardViewModel<(string Key, string Value)>
    {
        public TestCardViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override Task LoadAsync(Guid id) => Task.CompletedTask;

        protected override bool IsSymbolUploadAllowed() => false;

        protected override (AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent()
            => (AttachmentEntityKind.Security, Guid.Empty);

        protected override Task AssignNewSymbolAsync(Guid? attachmentId) => Task.CompletedTask;

        public void RaiseOverlay(BaseViewModel.UiOverlaySpec spec)
        {
            RaiseUiActionRequested("OpenOverlay", spec);
        }
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();

        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
