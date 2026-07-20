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
        var settings = new UpdateSettingsDto(true, 60, "owner", "repo", "update.json", null, null, null, "updates", 120);
        var status = new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.0.1", "win-x64", null, null, "release.zip", false, null, null, null);
        var installing = status with { Status = UpdateStatusKind.Installing };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        apiMock.Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(installing);
        var (vm, localizer) = CreateVmAndLocalizer(apiMock.Object);
        await vm.StartInstallAsync(confirmDowntime: true);
        vm.SetInstallPhase("Msg_Update_WaitingForRestart");

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));

        render.Markup.Should().Contain(localizer["Msg_Update_WaitingForRestart"].Value);
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
        var settings = new UpdateSettingsDto(true, 60, "owner", "repo", "update.json", null, null, null, "updates", 3);
        var status = new UpdateStatusDto(UpdateStatusKind.Ready, "1.0.0", null, "1.0.1", "win-x64", null, null, "release.zip", false, null, null, null);
        var installing = status with { Status = UpdateStatusKind.Installing };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status);
        apiMock.Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(installing);
        var (vm, _) = CreateVmAndLocalizer(apiMock.Object, useHangingHttpClient: true);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var render = Render<SetupUpdateTab>(parameters => parameters.Add(p => p.ViewModel, vm));
        render.Find("button.danger").Click();

        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (vm.LastErrorCode is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        vm.LastErrorCode.Should().Be("Err_Update_HealthTimeout");
        vm.InstallPhase.Should().Be("Msg_Update_Installing");
    }
}
