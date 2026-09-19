using FinanceManager.Application;
using FinanceManager.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SetupWellKnownViewModel"/>: loading the persisted settings into the editable
/// model, dirty tracking, the save round trip that sends the update request and clears the dirty
/// flag, and recoverable API failures surfaced via <c>Error</c>/<c>SaveError</c>.
/// </summary>
public sealed class SetupWellKnownViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    /// <summary>
    /// Verifies that <c>LoadAsync</c> populates <c>Model</c> from the API DTO and leaves the view
    /// model clean — no dirty flag, no error.
    /// </summary>
    [Fact]
    public async Task Load_PopulatesModel()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetWellKnownSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WellKnownSettingsDto { ChangePasswordUrl = "/account/password" });
        var vm = new SetupWellKnownViewModel(CreateSp(apiMock.Object));

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.Busy.Should().BeFalse();
        vm.Error.Should().BeNull();
        vm.Model.ChangePasswordUrl.Should().Be("/account/password");
        vm.Dirty.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a modified value marks the view model dirty and that <c>SaveAsync</c> forwards
    /// the new URL to the update API, then clears the dirty flag and reports success.
    /// </summary>
    [Fact]
    public async Task Save_SendsUpdateAndClearsDirty()
    {
        WellKnownSettingsUpdateRequest? captured = null;
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetWellKnownSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WellKnownSettingsDto { ChangePasswordUrl = "/change-password" });
        apiMock.Setup(a => a.UpdateWellKnownSettingsAsync(It.IsAny<WellKnownSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WellKnownSettingsUpdateRequest, CancellationToken>((request, _) => captured = request)
            .Returns(Task.CompletedTask);
        var vm = new SetupWellKnownViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.Model.ChangePasswordUrl = "https://idp.example.com/account/password";
        vm.OnChanged();
        vm.Dirty.Should().BeTrue();

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!.ChangePasswordUrl.Should().Be("https://idp.example.com/account/password");
        vm.Dirty.Should().BeFalse();
        vm.SavedOk.Should().BeTrue();
        vm.Busy.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a failing save call is caught and surfaced as <c>SaveError</c> (taken from the
    /// API's <c>LastError</c>) rather than throwing out of the save pipeline.
    /// </summary>
    [Fact]
    public async Task Save_SetsError_OnHttpFailure()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetWellKnownSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WellKnownSettingsDto { ChangePasswordUrl = "/change-password" });
        apiMock.Setup(a => a.UpdateWellKnownSettingsAsync(It.IsAny<WellKnownSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("save failed"));
        apiMock.Setup(a => a.LastError).Returns("Well-known settings could not be saved.");
        var vm = new SetupWellKnownViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.Model.ChangePasswordUrl = "/account/password";
        vm.OnChanged();

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        vm.Busy.Should().BeFalse();
        vm.SaveError.Should().Be("Well-known settings could not be saved.");
        vm.SavedOk.Should().BeFalse();
    }

    private static IServiceProvider CreateSp(IApiClient api)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(api);
        return services.BuildServiceProvider();
    }
}
