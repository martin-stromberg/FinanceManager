using FinanceManager.Application;
using FinanceManager.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SetupSecurityTxtViewModel"/>'s error handling: recoverable API failures
/// (<see cref="HttpRequestException"/>) are caught and surfaced via <c>Error</c>/<c>SaveError</c> using the
/// API's <c>LastError</c> message, while unexpected exceptions (<see cref="InvalidOperationException"/>)
/// propagate rather than being swallowed; also covers the client-side validation that rejects an
/// unparsable "expires" date before it ever reaches the save API.
/// </summary>
public sealed class SetupSecurityTxtViewModelTests_ErrorHandling
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    /// <summary>
    /// Verifies that a failing load call is caught and surfaced as <c>Error</c> (taken from the API's
    /// <c>LastError</c>) with <c>Busy</c> reset to false, rather than throwing out of the load pipeline.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenHttpRequestFails_SetsError()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetSecurityTxtSettingsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("request failed"));
        apiMock.Setup(a => a.LastError).Returns("Security.txt settings are unavailable.");
        var vm = new SetupSecurityTxtViewModel(CreateSp(apiMock.Object));

        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.Busy.Should().BeFalse();
        vm.Error.Should().Be("Security.txt settings are unavailable.");
    }

    /// <summary>
    /// Verifies that a failing save call is caught and surfaced as <c>SaveError</c> (taken from the API's
    /// <c>LastError</c>) with <c>Busy</c> reset to false, rather than throwing out of the save pipeline.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenHttpRequestFails_SetsSaveError()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetSecurityTxtSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityTxtSettingsDto
            {
                Contact = "mailto:security@example.com",
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        apiMock.Setup(a => a.UpdateSecurityTxtSettingsAsync(It.IsAny<SecurityTxtSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("save failed"));
        apiMock.Setup(a => a.LastError).Returns("Security.txt settings could not be saved.");
        var vm = new SetupSecurityTxtViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.Model.Contact = "mailto:updated@example.com";
        vm.OnChanged();

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        vm.Busy.Should().BeFalse();
        vm.SaveError.Should().Be("Security.txt settings could not be saved.");
    }

    /// <summary>
    /// Verifies that an exception type other than <see cref="HttpRequestException"/> is not swallowed
    /// into <c>Error</c> but propagates out of <c>LoadAsync</c>, so genuine coding bugs are not silently
    /// hidden behind the "failed to load" state.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenInvalidOperationOccurs_Rethrows()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetSecurityTxtSettingsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broken payload"));
        var vm = new SetupSecurityTxtViewModel(CreateSp(apiMock.Object));

        var act = () => vm.LoadAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that an exception type other than <see cref="HttpRequestException"/> is not swallowed
    /// into <c>SaveError</c> but propagates out of <c>SaveAsync</c>, so genuine coding bugs are not
    /// silently hidden behind the "failed to save" state.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenInvalidOperationOccurs_Rethrows()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetSecurityTxtSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityTxtSettingsDto
            {
                Contact = "mailto:security@example.com",
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        apiMock.Setup(a => a.UpdateSecurityTxtSettingsAsync(It.IsAny<SecurityTxtSettingsUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broken state"));
        var vm = new SetupSecurityTxtViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.Model.Contact = "mailto:updated@example.com";
        vm.OnChanged();

        var act = () => vm.SaveAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that an unparsable "expires" date entered by the user (e.g. "not-a-date") is caught by
    /// client-side validation before saving: the update API is never called, <c>SavedOk</c> stays false,
    /// and a non-empty <c>SaveError</c> is set, preventing an invalid security.txt file from being
    /// generated on the server.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenExpiresTextInvalid_DoesNotPersistAndSetsSaveError()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetSecurityTxtSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityTxtSettingsDto
            {
                Contact = "mailto:security@example.com",
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        var vm = new SetupSecurityTxtViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.Model.Contact = "mailto:updated@example.com";
        vm.ExpiresText = "not-a-date";

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        vm.SaveError.Should().NotBeNullOrWhiteSpace();
        vm.SavedOk.Should().BeFalse();
        apiMock.Verify(a => a.UpdateSecurityTxtSettingsAsync(It.IsAny<SecurityTxtSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IServiceProvider CreateSp(IApiClient api)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(api);
        return services.BuildServiceProvider();
    }
}
