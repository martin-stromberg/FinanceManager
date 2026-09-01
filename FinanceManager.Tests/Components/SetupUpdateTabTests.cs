using System.Net.Http;
using Bunit;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web;
using FinanceManager.Web.Components.Pages.Setup;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

public sealed class SetupUpdateTabTests : BunitContext
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The hanging handler should never complete a request.");
        }
    }

    private sealed class HangingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new HangingHandler());
    }

    [Fact]
    public void ShouldReloadAfterHealth_RequiresObservedOutage()
    {
        SetupUpdateTab.ShouldReloadAfterHealth(outageObserved: false, healthSuccessful: true).Should().BeFalse();
        SetupUpdateTab.ShouldReloadAfterHealth(outageObserved: true, healthSuccessful: false).Should().BeFalse();
        SetupUpdateTab.ShouldReloadAfterHealth(outageObserved: true, healthSuccessful: true).Should().BeTrue();
    }

    [Fact]
    public void Render_WhileLoading_ShowsLocalizedLoadingMessage()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).Returns(new TaskCompletionSource<UpdateSettingsDto>().Task);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).Returns(new TaskCompletionSource<UpdateStatusDto>().Task);
        var (vm, localizer) = CreateVmAndLocalizer(apiMock.Object);

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));

        render.Markup.Should().Contain(localizer["Msg_Loading"].Value);
    }

    [Fact]
    public async Task Render_WhileInstallingAndWaitingPhase_ShowsLocalizedWaitingMessage()
    {
        var settings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var status = new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.0.1", "win-x64", null, null, "release.zip", false, null, null, null);
        var installing = status with { Status = UpdateStatusKind.Installing };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        apiMock.Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(installing);
        var (vm, localizer) = CreateVmAndLocalizer(apiMock.Object);
        await vm.StartInstallAsync(confirmDowntime: true, ct: Xunit.TestContext.Current.CancellationToken);
        vm.SetInstallPhase("Msg_Update_WaitingForRestart");

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));

        render.Markup.Should().Contain(localizer["Msg_Update_WaitingForRestart"].Value);
    }

    [Fact]
    public void Render_WithLoadedSettings_HidesRemovedFieldsAndTabButtons()
    {
        var settings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, "FinanceManagerService", "app.exe", "updates", 120, false);
        var status = new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.0.1", "win-x64", null, null, "release.zip", false, null, null, null);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        var (vm, localizer) = CreateVmAndLocalizer(apiMock.Object);

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));

        render.WaitForAssertion(() =>
        {
            render.Markup.Should().NotContain(localizer["SetupUpdate_Lbl_ExecutablePath"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Lbl_RepositoryOwner"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Lbl_RepositoryName"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Lbl_ManifestAssetName"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Lbl_WorkingDirectory"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Lbl_HealthTimeout"].Value);
            render.Markup.Should().NotContain("type=\"number\"");
            render.Markup.Should().NotContain(localizer["SetupUpdate_Btn_SaveSettings"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Btn_CheckNow"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Btn_Install"].Value);
            render.Markup.Should().NotContain(localizer["SetupUpdate_Btn_ResetLock"].Value);
        });
    }

    [Fact]
    public void Render_WithLoadedSettings_ShowsIncludePrereleasesCheckboxAndUpdatesViewModel()
    {
        var settings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var status = new UpdateStatusDto(UpdateStatusKind.NoUpdate, "1.0.0", null, null, "win-x64", null, null, null, false, null, null, null);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        var (vm, localizer) = CreateVmAndLocalizer(apiMock.Object);

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));

        render.WaitForAssertion(() =>
        {
            render.Markup.Should().Contain(localizer["SetupUpdate_Lbl_IncludePrereleases"].Value);
            render.Markup.Should().Contain(localizer["SetupUpdate_Lbl_SourceCheckStartTime"].Value);
            render.Markup.Should().Contain(localizer["SetupUpdate_Lbl_SourceCheckEndTime"].Value);
            render.FindAll("input[type=checkbox]").Should().HaveCountGreaterThanOrEqualTo(2);
            render.FindAll("input[type=time]").Should().HaveCount(3);
        });

        render.FindAll("input[type=checkbox]")[1].Change(true);
        render.FindAll("input[type=time]")[0].Change("21:00");

        vm.Settings!.IncludePrereleases.Should().BeTrue();
        vm.Settings.SourceCheckStartTime.Should().Be(new TimeOnly(21, 0));
        vm.Dirty.Should().BeTrue();
    }

    [Fact]
    public void Render_WithLoadedStatus_ShowsLocalizedStatus()
    {
        var settings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var status = new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.0.1", "win-x64", null, null, "release.zip", false, null, null, null);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        var (vm, localizer) = CreateVmAndLocalizer(apiMock.Object);

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));

        render.WaitForAssertion(() =>
        {
            render.Markup.Should().Contain(localizer["UpdateStatusKind_Ready"].Value);
            render.Markup.Should().NotContain($">{UpdateStatusKind.Ready}<");
        });
    }

    [Fact]
    public async Task Ribbon_WhenBusyAndUpdateLockActive_DoesNotDisableResetLockAction()
    {
        var settings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 120, false);
        var status = new UpdateStatusDto(UpdateStatusKind.NoUpdate, "1.0.0", null, null, "win-x64", null, null, null, true, DateTimeOffset.UtcNow.AddHours(-2), null, null);
        var checkTask = new TaskCompletionSource<UpdateCheckResultDto>();
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        apiMock.Setup(a => a.Updates_CheckAsync(It.IsAny<CancellationToken>())).Returns(checkTask.Task);
        var (vm, localizer) = CreateVmAndLocalizer(apiMock.Object);
        await vm.LoadAsync(Xunit.TestContext.Current.CancellationToken);

        var busyTask = vm.CheckAsync(Xunit.TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!vm.Busy && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        }

        vm.Busy.Should().BeTrue();
        var resetLockAction = vm.GetRibbon(localizer)!
            .SelectMany(register => register.Items)
            .Single(action => action.Id == "UpdateResetLock");

        resetLockAction.Disabled.Should().BeFalse();

        checkTask.SetResult(new UpdateCheckResultDto(false, status, null));
        await busyTask;
    }

    private (SetupUpdateViewModel Vm, IStringLocalizer<Pages> Localizer) CreateVmAndLocalizer(IApiClient api, bool useHangingHttpClient = false)
    {
        Services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        Services.AddSingleton(api);
        if (useHangingHttpClient)
        {
            Services.AddSingleton<IHttpClientFactory>(new HangingHttpClientFactory());
        }
        else
        {
            Services.AddHttpClient();
        }
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        var sp = Services.BuildServiceProvider();
        var vm = new SetupUpdateViewModel(sp);
        return (vm, sp.GetRequiredService<IStringLocalizer<Pages>>());
    }

    [Fact]
    public async Task PollHealthAsync_WhenHealthCheckIsCancelledByTimeout_DoesNotTreatCancellationAsOutage()
    {
        var settings = new UpdateSettingsDto(true, "owner", "repo", "update.json", new TimeOnly(20, 0), new TimeOnly(6, 0), null, null, null, "updates", 3, false);
        var status = new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.0.1", "win-x64", null, null, "release.zip", false, null, null, null);
        var installing = status with { Status = UpdateStatusKind.Installing };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        apiMock.Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(installing);
        var (vm, _) = CreateVmAndLocalizer(apiMock.Object, useHangingHttpClient: true);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));
        await vm.StartInstallWithConfirmationAsync(Xunit.TestContext.Current.CancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (vm.LastErrorCode is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        }

        vm.LastErrorCode.Should().Be("Err_Update_HealthTimeout");
        vm.InstallPhase.Should().Be("Msg_Update_Installing");
    }
}
