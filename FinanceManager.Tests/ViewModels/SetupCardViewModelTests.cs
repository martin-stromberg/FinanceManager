using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FinanceManager.Web.ViewModels.Setup;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Tests.ViewModels
{
    public class SetupCardViewModelTests
    {
        private sealed class TestCurrentUserService : FinanceManager.Application.ICurrentUserService
        {
            public TestCurrentUserService(Guid userId, bool isAuthenticated = true)
            {
                UserId = userId;
                IsAuthenticated = isAuthenticated;
                PreferredLanguage = null;
            }

            public Guid UserId { get; }
            public string? PreferredLanguage { get; }
            public bool IsAuthenticated { get; }
            public bool IsAdmin => false;
        }

        private static IServiceProvider BuildServices()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<FinanceManager.Application.ICurrentUserService>(new TestCurrentUserService(Guid.NewGuid()));
            // no other services required for these tests
            return sc.BuildServiceProvider();
        }

        [Fact]
        public async Task LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);

            BaseViewModel.UiActionEventArgs? received = null;
            // subscribe to the legacy UiActionRequested event which carries UiActionEventArgs
            BaseViewModel baseVm = vm;
            baseVm.UiActionRequested += (_, e) => received = e;

            await vm.LoadAsync(Guid.Empty);

            Assert.NotNull(received);
            Assert.Equal("EmbeddedPanel", received!.Action);
            Assert.IsType<BaseViewModel.EmbeddedPanelSpec>(received.PayloadObject);

            var spec = (BaseViewModel.EmbeddedPanelSpec)received.PayloadObject!;
            Assert.Equal(typeof(FinanceManager.Web.Components.Shared.SetupPanel), spec.ComponentType);
            Assert.Equal(EmbeddedPanelPosition.AfterRibbon, spec.Position);

            // Outer parameters should include InnerComponentType and InnerParameters
            var parms = spec.Parameters as IDictionary<string, object>;
            Assert.NotNull(parms);
            Assert.True(parms!.ContainsKey("InnerComponentType"));
            Assert.True(parms.ContainsKey("InnerParameters"));
        }

        [Fact]
        public void ChangeView_Profile_Clears_AfterCard_And_Opens_ProfilePanel()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);

            var events = new List<BaseViewModel.UiActionEventArgs?>();
            BaseViewModel baseVm = vm;
            baseVm.UiActionRequested += (_, e) => events.Add(e);

            vm.ChangeView("profile");

            // Expect ClearEmbeddedPanel then EmbeddedPanel
            Assert.True(events.Count >= 1);

            // First event should be ClearEmbeddedPanel
            var first = events[0];
            Assert.Equal("ClearEmbeddedPanel", first!.Action);
            Assert.IsType<EmbeddedPanelPosition>(first.PayloadObject);
            Assert.Equal(EmbeddedPanelPosition.AfterCard, (EmbeddedPanelPosition)first.PayloadObject!);

            // There should be a second event for the embedded panel
            var embedded = events.Find(e => e != null && e.Action == "EmbeddedPanel");
            Assert.NotNull(embedded);
            var spec = Assert.IsType<BaseViewModel.EmbeddedPanelSpec>(embedded!.PayloadObject);
            Assert.Equal(typeof(FinanceManager.Web.Components.Shared.SetupPanel), spec.ComponentType);

            var outerParms = spec.Parameters as IDictionary<string, object>;
            Assert.NotNull(outerParms);
            Assert.True(outerParms!.ContainsKey("InnerComponentType"));
            Assert.Equal(typeof(FinanceManager.Web.Components.Pages.Setup.SetupProfileTab), outerParms["InnerComponentType"]);

            Assert.True(outerParms.ContainsKey("InnerParameters"));
            var innerParms = outerParms["InnerParameters"] as IDictionary<string, object>;
            Assert.NotNull(innerParms);
            Assert.True(innerParms!.ContainsKey("ViewModel"));
            Assert.IsType<SetupProfileViewModel>(innerParms["ViewModel"]);
        }
    }
}
