using System.Net;
using Bunit;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

public sealed class BackgroundTaskStatusPanelTests : BunitContext
{
    [Fact]
    public void DoesNotLoadTasks_WhenUserIsNotAuthenticated()
    {
        var apiMock = new Mock<IApiClient>();
        RegisterServices(apiMock, isAuthenticated: false, jsAuthenticated: false);

        Render<BackgroundTaskStatusPanel>();

        apiMock.Verify(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void LoadsTasks_WhenCircuitAuthFallbackIsAuthenticated()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTask(BackgroundTaskStatus.Running) });
        RegisterServices(apiMock, isAuthenticated: false, jsAuthenticated: true);

        var cut = Render<BackgroundTaskStatusPanel>(parameters => parameters.Add(p => p.PollInterval, 10_000));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(cut.Find(".bgt-panel"));
        });
    }

    [Fact]
    public void LoadsAndRendersTasks_WhenUserIsAuthenticated()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTask(BackgroundTaskStatus.Running) });
        RegisterServices(apiMock, isAuthenticated: true);

        var cut = Render<BackgroundTaskStatusPanel>(parameters => parameters.Add(p => p.PollInterval, 10_000));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(cut.Find(".bgt-panel"));
        });
    }

    [Fact]
    public void StopsPolling_WhenActiveTasksRequestReturnsUnauthorized()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
        RegisterServices(apiMock, isAuthenticated: true);

        var cut = Render<BackgroundTaskStatusPanel>(parameters => parameters.Add(p => p.PollInterval, 10));

        cut.WaitForAssertion(() =>
            apiMock.Verify(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once));

        Task.Delay(75).GetAwaiter().GetResult();

        apiMock.Verify(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void KeepsPolling_WhenActiveTasksRequestFailsTransiently()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.SetupSequence(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Temporary failure"))
            .ReturnsAsync(new[] { CreateTask(BackgroundTaskStatus.Queued) });
        RegisterServices(apiMock, isAuthenticated: true);

        var cut = Render<BackgroundTaskStatusPanel>(parameters => parameters.Add(p => p.PollInterval, 10));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(x => x.BackgroundTasks_GetActiveAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
            Assert.NotNull(cut.Find(".bgt-panel"));
        });
    }

    private void RegisterServices(Mock<IApiClient> apiMock, bool isAuthenticated, bool? jsAuthenticated = null)
    {
        Services.AddSingleton(apiMock.Object);
        Services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = isAuthenticated });
        Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));

        if (jsAuthenticated.HasValue)
        {
            JSInterop.Setup<bool>("fmAuthIsAuthenticated").SetResult(jsAuthenticated.Value);
        }
    }

    private static BackgroundTaskInfo CreateTask(BackgroundTaskStatus status)
        => new(
            Guid.NewGuid(),
            BackgroundTaskType.ClassifyAllDrafts,
            Guid.NewGuid(),
            DateTime.UtcNow,
            status,
            1,
            2,
            "Processing",
            0,
            0,
            null,
            status == BackgroundTaskStatus.Running ? DateTime.UtcNow : null,
            null,
            null,
            null,
            null,
            null);

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
