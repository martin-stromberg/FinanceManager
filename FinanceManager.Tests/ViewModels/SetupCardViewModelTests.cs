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
        public void TryGetSectionComponentType_Profile_ReturnsExpectedComponent()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);

            var found = vm.TryGetSectionComponentType("profile", out var componentType);

            Assert.True(found);
            Assert.NotNull(componentType);
            Assert.Equal(typeof(FinanceManager.Web.Components.Pages.Setup.SetupProfileTab), componentType);
        }

        [Fact]
        public void CreateSectionViewModel_Profile_CreatesExpectedViewModel()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);

            var sectionVm = vm.CreateSectionViewModel("profile", sp);

            Assert.NotNull(sectionVm);
            Assert.IsType<SetupProfileViewModel>(sectionVm);
        }
    }
}
