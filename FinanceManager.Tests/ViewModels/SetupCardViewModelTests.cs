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
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;

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
            sc.AddLogging();
            sc.AddSingleton<IApiClient>(new Mock<IApiClient>().Object);
            return sc.BuildServiceProvider();
        }

        private static IServiceProvider BuildServices(IApiClient apiClient)
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<FinanceManager.Application.ICurrentUserService>(new TestCurrentUserService(Guid.NewGuid()));
            sc.AddLogging();
            sc.AddSingleton(apiClient);
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
        public async Task GetRibbonRegisters_AfterLoad_ExposesSingleGlobalSaveAndReset()
        {
            var allActionIds = await GetAllActionIdsAfterLoad();

            allActionIds.Count(id => id == "Save").Should().Be(1);
            allActionIds.Count(id => id == "Reset").Should().Be(1);
            allActionIds.Should().Contain("DetectTimezone");
            allActionIds.Should().NotContain("SaveNotifications");
            allActionIds.Should().NotContain("ResetNotifications");
            allActionIds.Should().NotContain("SaveImportSplit");
            allActionIds.Should().NotContain("ResetImportSplit");
        }

        private static async Task<List<string>> GetAllActionIdsAfterLoad()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);
            await vm.LoadAsync(Guid.Empty);
            return GetAllActionIds(vm);
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

        [Fact]
        public async Task LoadAsync_InitializesCoreSections_WhenSectionWasCreatedBeforeLoad()
        {
            var sp = BuildServices();
            var vm = new SetupCardViewModel(sp);

            vm.CreateSectionViewModel("profile", sp);

            await vm.LoadAsync(Guid.Empty);

            var allActionIds = GetAllActionIds(vm);

            allActionIds.Should().Contain("Save");
            allActionIds.Should().Contain("Reset");
            allActionIds.Should().Contain("DetectTimezone");
        }

        [Fact]
        public async Task GlobalSave_InvokesDirtySetupSectionSaves()
        {
            var apiMock = new Mock<IApiClient>();
            apiMock.Setup(a => a.UserSettings_GetProfileAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserProfileSettingsDto { PreferredLanguage = "de", TimeZoneId = "Europe/Berlin" });
            apiMock.Setup(a => a.UserSettings_UpdateProfileAsync(It.IsAny<UserProfileSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            apiMock.Setup(a => a.User_GetNotificationSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NotificationSettingsDto { HolidayProvider = "Memory" });
            apiMock.Setup(a => a.User_UpdateNotificationSettingsAsync(It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            apiMock.Setup(a => a.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImportSplitSettingsDto { Mode = ImportSplitMode.FixedSize, MaxEntriesPerDraft = 20, MinEntriesPerDraft = 1 });
            apiMock.Setup(a => a.UserSettings_UpdateImportSplitAsync(It.IsAny<ImportSplitSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            apiMock.Setup(a => a.Securities_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SecurityDto>());
            apiMock.Setup(a => a.Securities_GetReturnAnalysisSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReturnAnalysisSettingsResponse(null, null, false, 0m));
            apiMock.Setup(a => a.Securities_UpdateReturnAnalysisSettingsAsync(It.IsAny<ReturnAnalysisSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var sp = BuildServices(apiMock.Object);
            var vm = new SetupCardViewModel(sp);

            await vm.LoadAsync(Guid.Empty);

            var profileVm = (SetupProfileViewModel)vm.CreateSectionViewModel("profile", sp)!;
            await profileVm.LoadAsync();
            profileVm.Model.PreferredLanguage = "en";
            profileVm.OnChanged();

            var notificationsVm = (SetupNotificationsViewModel)vm.CreateSectionViewModel("notifications", sp)!;
            await notificationsVm.LoadAsync();
            notificationsVm.Model.MonthlyReminderEnabled = true;
            notificationsVm.OnChanged();

            var statementsVm = (SetupStatementsViewModel)vm.CreateSectionViewModel("statements", sp)!;
            await statementsVm.LoadAsync();
            statementsVm.Model!.MaxEntriesPerDraft = 25;
            statementsVm.Validate();

            var returnAnalysisVm = (SetupReturnAnalysisViewModel)vm.CreateSectionViewModel("returnanalysis", sp)!;
            await returnAnalysisVm.LoadAsync();
            returnAnalysisVm.ShowSharpeRatio = true;
            returnAnalysisVm.OnChanged();

            var localizerMock = new Mock<IStringLocalizer>();
            localizerMock.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));

            var saveAction = vm.GetRibbonRegisters(localizerMock.Object)!
                .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
                .SelectMany(t => t.Items)
                .First(a => a.Id == "Save");

            await saveAction.Callback!();

            apiMock.Verify(a => a.UserSettings_UpdateProfileAsync(It.IsAny<UserProfileSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            apiMock.Verify(a => a.User_UpdateNotificationSettingsAsync(It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            apiMock.Verify(a => a.UserSettings_UpdateImportSplitAsync(It.IsAny<ImportSplitSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            apiMock.Verify(a => a.Securities_UpdateReturnAnalysisSettingsAsync(It.IsAny<ReturnAnalysisSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static List<string> GetAllActionIds(SetupCardViewModel vm)
        {
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
    }
}
