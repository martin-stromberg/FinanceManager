using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

public sealed class SetupProfileViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static IServiceProvider CreateSp(IApiClient api) => CreateSp(api, Mock.Of<IKpiLocalStorageCache>());

    private static IServiceProvider CreateSp(IApiClient api, IKpiLocalStorageCache kpiCache)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(kpiCache);
        services.AddSingleton(api);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Initialize_Loads_Profile()
    {
        var dto = new UserProfileSettingsDto { PreferredLanguage = "de", TimeZoneId = "Europe/Berlin", HasAlphaVantageApiKey = true, ShareAlphaVantageApiKey = true };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var vm = new SetupProfileViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync();

        Assert.False(vm.Loading);
        Assert.Equal("de", vm.Model.PreferredLanguage);
        Assert.True(vm.HasKey);
        Assert.True(vm.ShareKey);
        Assert.False(vm.Dirty);
    }

    [Fact]
    public async Task Save_Updates_State_And_Resets_Flags_On_Success()
    {
        var dto = new UserProfileSettingsDto { PreferredLanguage = "de", TimeZoneId = "Europe/Berlin", HasAlphaVantageApiKey = false, ShareAlphaVantageApiKey = false };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.UserSettings_UpdateProfileAsync(It.IsAny<UserProfileSettingsUpdateRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = new SetupProfileViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync();

        vm.Model.PreferredLanguage = "en";
        vm.KeyInput = "abc";
        vm.ShareKey = true;
        vm.OnChanged();
        Assert.True(vm.Dirty);

        await vm.SaveAsync();
        apiMock.Verify(a => a.UserSettings_UpdateProfileAsync(It.IsAny<UserProfileSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(vm.SavedOk);
        Assert.False(vm.Dirty);
        Assert.Equal(string.Empty, vm.KeyInput);
    }

    [Fact]
    public async Task ClearKey_Sets_Dirty_And_Save_Sends_ClearFlag()
    {
        var dto = new UserProfileSettingsDto { PreferredLanguage = "de", TimeZoneId = "Europe/Berlin", HasAlphaVantageApiKey = true, ShareAlphaVantageApiKey = false };
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.UserSettings_UpdateProfileAsync(It.IsAny<UserProfileSettingsUpdateRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = new SetupProfileViewModel(CreateSp(apiMock.Object));
        await vm.LoadAsync();

        vm.ClearKey();
        Assert.True(vm.Dirty);

        await vm.SaveAsync();
        apiMock.Verify(a => a.UserSettings_UpdateProfileAsync(
            It.Is<UserProfileSettingsUpdateRequest>(r => r.ClearAlphaVantageApiKey == true),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(vm.SavedOk);
        Assert.False(vm.Dirty);
    }

    [Fact]
    public void SetDetected_Updates_Model_And_Dirty()
    {
        var apiMock = new Mock<IApiClient>();
        var vm = new SetupProfileViewModel(CreateSp(apiMock.Object));
        vm.SetDetectedTimezone("de-DE", "Europe/Berlin");
        Assert.Equal("de-DE", vm.Model.PreferredLanguage);
        Assert.Equal("Europe/Berlin", vm.Model.TimeZoneId);
        Assert.True(vm.Dirty);
    }

    [Fact]
    public async Task Save_WithCacheKpisDisabled_ClearsKpiCache()
    {
        var dto = new UserProfileSettingsDto
        {
            PreferredLanguage = "de",
            TimeZoneId = "Europe/Berlin",
            HasAlphaVantageApiKey = true,
            ShareAlphaVantageApiKey = false,
            CacheKpisInLocalStorage = true
        };

        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.UserSettings_GetProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.UserSettings_UpdateProfileAsync(It.IsAny<UserProfileSettingsUpdateRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var kpiCacheMock = new Mock<IKpiLocalStorageCache>();
        var vm = new SetupProfileViewModel(CreateSp(apiMock.Object, kpiCacheMock.Object));
        await vm.LoadAsync();

        vm.Model.CacheKpisInLocalStorage = false;
        vm.OnChanged();

        await vm.SaveAsync();

        Assert.True(vm.SavedOk);
        kpiCacheMock.Verify(k => k.RemoveAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
