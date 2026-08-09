using FinanceManager.Application;
using FinanceManager.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

public sealed class SetupSecurityTxtViewModelTests_ErrorHandling
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = true;
    }

    [Fact]
    public async Task LoadAsync_WhenHttpRequestFails_SetsError()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.GetSecurityTxtSettingsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("request failed"));
        apiMock.Setup(a => a.LastError).Returns("Security.txt settings are unavailable.");
        var vm = new SetupSecurityTxtViewModel(CreateSp(apiMock.Object));

        await vm.LoadAsync();

        vm.Busy.Should().BeFalse();
        vm.Error.Should().Be("Security.txt settings are unavailable.");
    }

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
        await vm.LoadAsync();
        vm.Model.Contact = "mailto:updated@example.com";
        vm.OnChanged();

        await vm.SaveAsync();

        vm.Busy.Should().BeFalse();
        vm.SaveError.Should().Be("Security.txt settings could not be saved.");
    }

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
        await vm.LoadAsync();
        vm.Model.Contact = "mailto:updated@example.com";
        vm.OnChanged();

        var act = () => vm.SaveAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static IServiceProvider CreateSp(IApiClient api)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(api);
        return services.BuildServiceProvider();
    }
}
