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

/// <summary>
/// Tests for <see cref="SetupUpdateViewModel"/>, the self-update setup screen's view model: loading and
/// saving update settings, dirty-tracking edits so only actually-changed fields trigger a save prompt,
/// the check/install/reset-lock workflow against a mocked <see cref="IApiClient"/> (including surfacing
/// specific API error codes), the install-confirmation gate that requires a caller-supplied callback before
/// starting an install, and the ribbon action wiring that reflects update readiness and confirmation state.
/// </summary>
public sealed class SetupUpdateViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    /// <summary>
    /// Verifies that when the install API call fails because no update package is ready, the view model
    /// surfaces the API's specific error code/message and leaves <c>Installing</c>/<c>Busy</c> false, rather
    /// than getting stuck showing an in-progress install that never actually started.
    /// </summary>
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

        await vm.StartInstallAsync(confirmDowntime: true, ct: TestContext.Current.CancellationToken);

        vm.Installing.Should().BeFalse();
        vm.Busy.Should().BeFalse();
        vm.LastErrorCode.Should().Be("Err_Update_NotReady");
        vm.LastError.Should().Be("No ready update package is available.");
    }

    /// <summary>
    /// Verifies that when resetting the update lock fails because no lock actually exists, the view model
    /// surfaces that specific error and does not attempt to reload status afterward, since there is nothing
    /// meaningful to reload.
    /// </summary>
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

        await vm.ResetLockAsync(TestContext.Current.CancellationToken);

        vm.Busy.Should().BeFalse();
        vm.LastErrorCode.Should().Be("Err_Update_Reset_NoLock");
        vm.LastError.Should().Be("No active update lock exists.");
        apiMock.Verify(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a successful lock reset reloads the update status from the API afterward, so the UI
    /// reflects the now-unlocked state immediately rather than a stale cached status.
    /// </summary>
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

        await vm.ResetLockAsync(TestContext.Current.CancellationToken);

        vm.Status.Should().Be(unlocked);
        vm.Status!.IsLocked.Should().BeFalse();
        apiMock.Verify(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }


    /// <summary>
    /// Verifies that starting an install through the confirmation-gated entry point without a
    /// <c>ConfirmInstallAsync</c> callback set is refused with a specific error and never calls the install
    /// API — installs that cause application downtime must not be startable without an explicit user
    /// confirmation step being wired up.
    /// </summary>
    [Fact]
    public async Task StartInstallWithConfirmationAsync_WhenNoConfirmationCallback_DoesNotStartInstall()
    {
        var apiMock = new Mock<IApiClient>();
        var vm = CreateVm(apiMock.Object);

        await vm.StartInstallWithConfirmationAsync(TestContext.Current.CancellationToken);

        apiMock.Verify(
            a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        vm.Installing.Should().BeFalse();
        vm.Busy.Should().BeFalse();
        vm.LastErrorCode.Should().Be("Err_Update_ConfirmationRequired");
    }

    /// <summary>
    /// Verifies that the "Install" ribbon action is disabled while an update is ready but no confirmation
    /// callback has been supplied, and becomes enabled the moment a callback is assigned — the ribbon state
    /// reflects the same confirmation-gate rule enforced in
    /// <see cref="StartInstallWithConfirmationAsync_WhenNoConfirmationCallback_DoesNotStartInstall"/>.
    /// </summary>
    [Fact]
    public async Task GetRibbonRegisters_WhenReadyButNoConfirmationCallback_DisablesInstallAction()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.Ready));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        GetAction(vm, "UpdateInstall").Disabled.Should().BeTrue();

        vm.ConfirmInstallAsync = () => ValueTask.FromResult(true);

        GetAction(vm, "UpdateInstall").Disabled.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that loading the view model populates both the update settings and the current update status
    /// from the API.
    /// </summary>
    [Fact]
    public async Task LoadAsync_PopulatesSettingsAndStatus()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var ready = Status(UpdateStatusKind.Ready);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ready);
        var vm = CreateVm(apiMock.Object);

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.Settings.Should().BeEquivalentTo(settings);
        vm.Status!.Status.Should().Be(UpdateStatusKind.Ready);
    }

    /// <summary>
    /// Verifies that saving edited settings sends the full update request (including the source-check time
    /// window) to the API, applies the API's returned settings back onto the view model, and clears the dirty
    /// flag.
    /// </summary>
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
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.UpdateSettings(settings with { Enabled = true, IncludePrereleases = true });

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        vm.Settings!.Enabled.Should().BeTrue();
        vm.Settings.IncludePrereleases.Should().BeTrue();
        sentRequest!.IncludePrereleases.Should().BeTrue();
        sentRequest.SourceCheckStartTime.Should().Be(new TimeOnly(20, 0));
        sentRequest.SourceCheckEndTime.Should().Be(new TimeOnly(6, 0));
        vm.Dirty.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that triggering an update check while settings have unsaved edits saves them first and only
    /// then performs the check (asserted via call order), so a check never runs against stale, not-yet-saved
    /// settings such as an unsaved prerelease-inclusion toggle.
    /// </summary>
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
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.UpdateSettings(settings with { IncludePrereleases = true });

        await vm.CheckAsync(TestContext.Current.CancellationToken);

        calls.Should().Equal("save", "check");
        sentRequest!.IncludePrereleases.Should().BeTrue();
        vm.Dirty.Should().BeFalse();
        vm.Settings.Should().Be(savedSettings);
        vm.Status.Should().Be(ready);
    }

    /// <summary>
    /// Verifies that changing a user-editable settings field (service name) marks the view model dirty.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_WhenEditableValueChanges_SetsDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.UpdateSettings(settings with { ServiceName = "FinanceManagerService" });

        vm.Dirty.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that toggling the "include prereleases" setting marks the view model dirty.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_WhenIncludePrereleasesChanges_SetsDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.UpdateSettings(settings with { IncludePrereleases = true });

        vm.Dirty.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that changing the scheduled source-check time window marks the view model dirty.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_WhenSourceCheckWindowChanges_SetsDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.UpdateSettings(settings with { SourceCheckStartTime = new TimeOnly(21, 0) });

        vm.Dirty.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that changing fields the view model no longer considers user-editable (repository owner,
    /// working directory, health timeout) does not mark it dirty — a regression guard ensuring the dirty-check
    /// only tracks the fields actually exposed for editing in the current UI, not every property on the DTO.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_WhenRemovedValueChanges_DoesNotSetDirty()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.UpdateSettings(settings with { RepositoryOwner = "other", WorkingDirectory = "custom-updates", HealthTimeoutSeconds = 30 });

        vm.Dirty.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that calling <c>Reset</c> after making unsaved edits discards those edits and restores the
    /// settings as they were originally loaded, clearing the dirty flag.
    /// </summary>
    [Fact]
    public async Task Reset_RestoresLoadedSettings()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Status(UpdateStatusKind.NoUpdate));
        var vm = CreateVm(apiMock.Object);
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.UpdateSettings(settings with { IncludePrereleases = true });

        vm.Reset();

        vm.Settings.Should().BeEquivalentTo(settings);
        vm.Dirty.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that requesting service-name suggestions (for the systemd/Windows service autocomplete field)
    /// delegates to the API client with the given query and populates the results.
    /// </summary>
    [Fact]
    public async Task LoadServiceSuggestionsAsync_UsesApiClient()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetServiceNamesAsync("fin", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "financemanager.service" });
        var vm = CreateVm(apiMock.Object);

        await vm.LoadServiceSuggestionsAsync("fin", TestContext.Current.CancellationToken);

        vm.ServiceSuggestions.Should().ContainSingle().Which.Should().Be("financemanager.service");
    }

    /// <summary>
    /// Verifies that a successful install start updates the status to <c>Installing</c> and flips the
    /// view model's <c>Installing</c> flag.
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_WhenReady_SetsInstallingState()
    {
        var settings = new UpdateSettingsDto(false, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var installing = Status(UpdateStatusKind.Installing);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(installing);
        var vm = CreateVm(apiMock.Object);

        await vm.StartInstallAsync(confirmDowntime: true, ct: TestContext.Current.CancellationToken);

        vm.Status!.Status.Should().Be(UpdateStatusKind.Installing);
        vm.Installing.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that setting the install phase updates <c>InstallPhase</c> and raises <c>StateChanged</c> for
    /// each transition, so the UI can display live progress text as the install moves from "installing" to
    /// "waiting for restart".
    /// </summary>
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
