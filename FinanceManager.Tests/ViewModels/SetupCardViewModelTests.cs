using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;
using FinanceManager.Web.ViewModels.Setup;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Shared;

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
            sc.AddSingleton<IApiClient>(new Mock<IApiClient>().Object);
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

            received.Should().NotBeNull();
            received!.Action.Should().Be("EmbeddedPanel");
            received.PayloadObject.Should().BeOfType<BaseViewModel.EmbeddedPanelSpec>();

            var spec = (BaseViewModel.EmbeddedPanelSpec)received.PayloadObject!;
            spec.ComponentType.Should().Be(typeof(FinanceManager.Web.Components.Shared.SetupPanel));
            spec.Position.Should().Be(EmbeddedPanelPosition.AfterRibbon);

            // Outer parameters should include InnerComponentType and InnerParameters
            var parms = spec.Parameters as IDictionary<string, object>;
            parms.Should().NotBeNull();
            parms!.Should().ContainKey("InnerComponentType");
            parms.Should().ContainKey("InnerParameters");
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

        [Fact]
        public async Task GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);
            await vm.LoadAsync(Guid.Empty);

            var localizerMock = new Mock<IStringLocalizer>();
            localizerMock.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));

            var registers = vm.GetRibbonRegisters(localizerMock.Object);

            Assert.NotNull(registers);
            var allActionIds = registers!
                .SelectMany(r => r.Tabs ?? new System.Collections.Generic.List<UiRibbonTab>())
                .SelectMany(t => t.Items)
                .Select(a => a.Id)
                .ToList();

            using (var scope = new AssertionScope())
            {
                allActionIds.Should().Contain("CreateBackup");
                allActionIds.Should().Contain("UploadBackup");
                allActionIds.Should().Contain("SaveNotifications");
                allActionIds.Should().Contain("ResetNotifications");
                allActionIds.Should().Contain("Save");
                allActionIds.Should().Contain("Reset");
                allActionIds.Should().Contain("DetectTimezone");
                allActionIds.Should().Contain("SaveImportSplit");
                allActionIds.Should().Contain("ResetImportSplit");
            }
        }

        [Fact]
        public async Task CreateSectionViewModel_AfterLoad_ReturnsCachedInstance()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);
            await vm.LoadAsync(Guid.Empty);

            var sectionVm = vm.CreateSectionViewModel("backup", sp);
            Assert.NotNull(sectionVm);
            Assert.IsType<SetupBackupsViewModel>(sectionVm);

            var sectionVm2 = vm.CreateSectionViewModel("backup", sp);
            Assert.Same(sectionVm, sectionVm2);
        }
    }
}
