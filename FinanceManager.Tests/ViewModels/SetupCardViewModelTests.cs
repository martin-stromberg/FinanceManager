using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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

            found.Should().BeTrue();
            componentType.Should().NotBeNull();
            componentType.Should().Be(typeof(FinanceManager.Web.Components.Pages.Setup.SetupProfileTab));
        }

        [Fact]
        public void CreateSectionViewModel_Profile_CreatesExpectedViewModel()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);

            var sectionVm = vm.CreateSectionViewModel("profile", sp);

            sectionVm.Should().NotBeNull();
            sectionVm.Should().BeOfType<SetupProfileViewModel>();
        }

        [Fact]
        public async Task GetRibbonRegisters_AfterLoad_IncludesBackupSectionActions()
        {
            var allActionIds = await GetAllActionIdsAfterLoad();

            allActionIds.Should().Contain("CreateBackup");
            allActionIds.Should().Contain("UploadBackup");
        }

        [Fact]
        public async Task GetRibbonRegisters_AfterLoad_IncludesNotificationsSectionActions()
        {
            var allActionIds = await GetAllActionIdsAfterLoad();

            allActionIds.Should().Contain("SaveNotifications");
            allActionIds.Should().Contain("ResetNotifications");
        }

        [Fact]
        public async Task GetRibbonRegisters_AfterLoad_IncludesProfileSectionActions()
        {
            var allActionIds = await GetAllActionIdsAfterLoad();

            allActionIds.Should().Contain("Save");
            allActionIds.Should().Contain("Reset");
            allActionIds.Should().Contain("DetectTimezone");
        }

        [Fact]
        public async Task GetRibbonRegisters_AfterLoad_IncludesStatementsSectionActions()
        {
            var allActionIds = await GetAllActionIdsAfterLoad();

            allActionIds.Should().Contain("SaveImportSplit");
            allActionIds.Should().Contain("ResetImportSplit");
        }

        private static async Task<List<string>> GetAllActionIdsAfterLoad()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);
            await vm.LoadAsync(Guid.Empty);

            var localizerMock = new Mock<IStringLocalizer>();
            localizerMock.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));

            var registers = vm.GetRibbonRegisters(localizerMock.Object);

            registers.Should().NotBeNull();
            return registers!
                .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
                .SelectMany(t => t.Items)
                .Select(a => a.Id)
                .ToList();
        }

        [Fact]
        public async Task CreateSectionViewModel_AfterLoad_ReturnsCachedInstance()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);
            await vm.LoadAsync(Guid.Empty);

            var sectionVm = vm.CreateSectionViewModel("backup", sp);
            sectionVm.Should().NotBeNull();
            sectionVm.Should().BeOfType<SetupBackupsViewModel>();

            var sectionVm2 = vm.CreateSectionViewModel("backup", sp);
            sectionVm2.Should().BeSameAs(sectionVm);
        }
    }
}
