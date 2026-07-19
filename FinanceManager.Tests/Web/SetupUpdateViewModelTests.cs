using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.ViewModels.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.Web;

public sealed class SetupUpdateViewModelTests
{
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
    public async Task LoadSaveAndInstallFlows_UpdateViewModelState()
    {
        var settings = new UpdateSettingsDto(false, 60, "owner", "repo", "update.json", null, null, null, "updates", 120);
        var ready = Status(UpdateStatusKind.Ready);
        var installing = Status(UpdateStatusKind.Installing);
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Updates_GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        apiMock.Setup(a => a.Updates_GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ready);
        apiMock.Setup(a => a.Updates_UpdateSettingsAsync(It.IsAny<UpdateSettingsUpdateRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(settings with { Enabled = true });
        apiMock.Setup(a => a.Updates_StartInstallAsync(It.IsAny<UpdateStartRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(installing);
        var vm = CreateVm(apiMock.Object);

        await vm.LoadAsync();
        vm.UpdateSettings(settings with { Enabled = true });
        await vm.SaveAsync();
        await vm.StartInstallAsync(confirmDowntime: true);

        vm.Settings!.Enabled.Should().BeTrue();
        vm.Status!.Status.Should().Be(UpdateStatusKind.Installing);
        vm.Installing.Should().BeTrue();
    }

    private static SetupUpdateViewModel CreateVm(IApiClient api)
    {
        var services = new ServiceCollection()
            .AddSingleton(api)
            .BuildServiceProvider();
        return new SetupUpdateViewModel(services);
    }

    private static UpdateStatusDto Status(UpdateStatusKind kind)
        => new(kind, "1.0.0", null, null, "win-x64", null, null, null, kind == UpdateStatusKind.Installing, null, null, null);
}
