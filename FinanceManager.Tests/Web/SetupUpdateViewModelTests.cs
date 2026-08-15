using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Application;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Web;

public sealed class SetupUpdateViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    [Fact]
    public async Task StartInstallAsync_WhenApiReportsNotReady_DoesNotSetInstalling()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock
            .Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("not ready"));
        apiMock.Setup(a => a.LastErrorCode).Returns("Err_Update_NotReady");
        apiMock.Setup(a => a.LastError).Returns("No ready update package is available.");
        var vm = CreateVm(apiMock.Object);

        await vm.StartInstallAsync(confirmDowntime: true);

        vm.Installing.Should().BeFalse();
        vm.Busy.Should().BeFalse();
        vm.LastErrorCode.Should().Be("Err_Update_NotReady");
        vm.LastError.Should().Be("No ready update package is available.");
    }

    [Fact]
    public async Task ResetLockAsync_WhenApiReportsSpecificError_SetsError()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock
            .Setup(a => a.Updates_ResetLockAsync(It.IsAny<UpdateLockResetRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("reset failed"));
        apiMock.Setup(a => a.LastErrorCode).Returns("Err_Update_Reset_NoLock");
        apiMock.Setup(a => a.LastError).Returns("No active update lock exists.");
        var vm = CreateVm(apiMock.Object);

        await vm.ResetLockAsync();

        vm.Busy.Should().BeFalse();
        vm.LastErrorCode.Should().Be("Err_Update_Reset_NoLock");
        vm.LastError.Should().Be("No active update lock exists.");
        apiMock.Verify(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetLockAsync_WhenSuccessful_ReloadsStatus()
    {
        var unlocked = Status(UpdateStatusKind.NoUpdate, isLocked: false);
        var apiMock = new Mock<IApiClient>();
        apiMock
            .Setup(a => a.Updates_ResetLockAsync(It.IsAny<UpdateLockResetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        apiMock
            .Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(unlocked);
        var vm = CreateVm(apiMock.Object);

        await vm.ResetLockAsync();

        vm.Status.Should().Be(unlocked);
        vm.Status!.IsLocked.Should().BeFalse();
        apiMock.Verify(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task StartInstallWithConfirmationAsync_WhenNoConfirmationCallback_DoesNotStartInstall()
    {
        var apiMock = new Mock<IApiClient>();
        var vm = CreateVm(apiMock.Object);

        await vm.StartInstallWithConfirmationAsync();

        apiMock.Verify(
            a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        vm.Installing.Should().BeFalse();
        vm.Busy.Should().BeFalse();
        vm.LastErrorCode.Should().Be("Err_Update_ConfirmationRequired");
    }

    [Fact]
    public async Task GetRibbonRegisters_WhenReadyButNoConfirmationCallback_DisablesInstallAction()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.Ready));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();

        GetAction(vm, "UpdateInstall").Disabled.Should().BeTrue();

        vm.ConfirmInstallAsync = () => ValueTask.FromResult(true);

        GetAction(vm, "UpdateInstall").Disabled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_PopulatesSettingsAndStatus()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var ready = Status(UpdateStatusKind.Ready);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ready);
        var vm = CreateVm(apiMock.Object);

        await vm.LoadAsync();

        vm.Settings.Should().BeEquivalentTo(settings);
        vm.Status!.Status.Should().Be(UpdateStatusKind.Ready);
    }

    [Fact]
    public async Task SaveAsync_PersistsUpdatedSettings()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var ready = Status(UpdateStatusKind.Ready);
        UpdateSettingsUpdateRequest? sentRequest = null;
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ready);
        apiMock.Setup(a => a.Updates_UpdateSettingsAsync(It.IsAny<UpdateSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSettingsUpdateRequest, CancellationToken>((request, _) => sentRequest = request)
            .ReturnsAsync(settings with { Enabled = true, IncludePrereleases = true });
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();
        vm.UpdateSettings(settings with { Enabled = true, IncludePrereleases = true });

        await vm.SaveAsync();

        vm.Settings!.Enabled.Should().BeTrue();
        vm.Settings.IncludePrereleases.Should().BeTrue();
        sentRequest!.IncludePrereleases.Should().BeTrue();
        sentRequest.SourceCheckStartTime.Should().Be(new TimeOnly(20, 0));
        sentRequest.SourceCheckEndTime.Should().Be(new TimeOnly(6, 0));
        vm.Dirty.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenSettingsAreDirty_SavesSettingsBeforeChecking()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var savedSettings = settings with { IncludePrereleases = true };
        var ready = Status(UpdateStatusKind.Ready) with { AvailableVersion = "1.21.0-RC.2" };
        var calls = new List<string>();
        UpdateSettingsUpdateRequest? sentRequest = null;
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        apiMock.Setup(a => a.Updates_UpdateSettingsAsync(It.IsAny<UpdateSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSettingsUpdateRequest, CancellationToken>((request, _) =>
            {
                calls.Add("save");
                sentRequest = request;
            })
            .ReturnsAsync(savedSettings);
        apiMock.Setup(a => a.Updates_CheckAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("check"))
            .ReturnsAsync(new UpdateCheckResultDto(true, ready, "Update package is ready to install."));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();
        vm.UpdateSettings(settings with { IncludePrereleases = true });

        await vm.CheckAsync();

        calls.Should().Equal("save", "check");
        sentRequest!.IncludePrereleases.Should().BeTrue();
        vm.Dirty.Should().BeFalse();
        vm.Settings.Should().Be(savedSettings);
        vm.Status.Should().Be(ready);
    }

    [Fact]
    public async Task UpdateSettings_WhenEditableValueChanges_SetsDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();

        vm.UpdateSettings(settings with { ServiceName = "FinanceManagerService" });

        vm.Dirty.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSettings_WhenIncludePrereleasesChanges_SetsDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();

        vm.UpdateSettings(settings with { IncludePrereleases = true });

        vm.Dirty.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSettings_WhenSourceCheckWindowChanges_SetsDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();

        vm.UpdateSettings(settings with { SourceCheckStartTime = new TimeOnly(21, 0) });

        vm.Dirty.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSettings_WhenRemovedValueChanges_DoesNotSetDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();

        vm.UpdateSettings(settings with { RepositoryOwner = "other", WorkingDirectory = "custom-updates", HealthTimeoutSeconds = 30 });

        vm.Dirty.Should().BeFalse();
    }

    [Fact]
    public async Task Reset_RestoresLoadedSettings()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync();
        vm.UpdateSettings(settings with { IncludePrereleases = true });

        vm.Reset();

        vm.Settings.Should().BeEquivalentTo(settings);
        vm.Dirty.Should().BeFalse();
    }

    [Fact]
    public async Task LoadServiceSuggestionsAsync_UsesApiClient()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetServiceNamesAsync("fin", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "financemanager.service" });
        var vm = CreateVm(apiMock.Object);

        await vm.LoadServiceSuggestionsAsync("fin");

        vm.ServiceSuggestions.Should().ContainSingle().Which.Should().Be("financemanager.service");
    }

    [Fact]
    public async Task StartInstallAsync_WhenReady_SetsInstallingState()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var installing = Status(UpdateStatusKind.Installing);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(installing);
        var vm = CreateVm(apiMock.Object);

        await vm.StartInstallAsync(confirmDowntime: true);

        vm.Status!.Status.Should().Be(UpdateStatusKind.Installing);
        vm.Installing.Should().BeTrue();
    }

    [Fact]
    public void SetInstallPhase_TransitionsFromInstallingToWaiting()
    {
        var apiMock = new Mock<IApiClient>();
        var vm = CreateVm(apiMock.Object);
        var stateChangedCount = 0;
        vm.StateChanged += (_, _) => stateChangedCount++;

        vm.SetInstallPhase("Msg_Update_Installing");
        vm.InstallPhase.Should().Be("Msg_Update_Installing");

        vm.SetInstallPhase("Msg_Update_WaitingForRestart");
        vm.InstallPhase.Should().Be("Msg_Update_WaitingForRestart");

        stateChangedCount.Should().Be(2);
    }

    private static SetupUpdateViewModel CreateVm(IApiClient api)
    {
        var services = new ServiceCollection()
            .AddSingleton(api)
            .AddSingleton<ICurrentUserService>(new TestCurrentUserService())
            .BuildServiceProvider();
        return new SetupUpdateViewModel(services);
    }

    private static UiRibbonAction GetAction(SetupUpdateViewModel vm, string id)
    {
        var localizerMock = new Mock<IStringLocalizer>();
        localizerMock.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        return vm.GetRibbonRegisters(localizerMock.Object)!
            .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(t => t.Items)
            .First(a => a.Id == id);
    }

    private static UpdateStatusDto Status(UpdateStatusKind kind, bool? isLocked = null)
        => new(kind, "1.0.0", null, null, "win-x64", null, null, null, isLocked ?? kind == UpdateStatusKind.Installing, null, null, null);
}
