using FluentAssertions;
using FinanceManager.Infrastructure;
using FinanceManager.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

public class ApiClientUserSettingsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiClientUserSettingsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private async Task<string> EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
        return username;
    }

    [Fact]
    public async Task UserSettings_GetProfile_Returns_Defaults()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var profile = await api.UserSettings_GetProfileAsync();
        profile.Should().NotBeNull();
        // defaults: no language, no timezone, no API key
        profile!.HasAlphaVantageApiKey.Should().BeFalse();
        profile.ShareAlphaVantageApiKey.Should().BeFalse();
    }

    [Fact]
    public async Task UserSettings_UpdateProfile_Sets_Language_And_Timezone()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var ok = await api.UserSettings_UpdateProfileAsync(new UserProfileSettingsUpdateRequest(
            PreferredLanguage: "de",
            TimeZoneId: "Europe/Berlin",
            AlphaVantageApiKey: null,
            ClearAlphaVantageApiKey: null,
            ShareAlphaVantageApiKey: null));
        ok.Should().BeTrue();

        var profile = await api.UserSettings_GetProfileAsync();
        profile.Should().NotBeNull();
        profile!.PreferredLanguage.Should().Be("de");
        profile.TimeZoneId.Should().Be("Europe/Berlin");
    }

    [Fact]
    public async Task UserSettings_UpdateProfile_Stores_Protected_AlphaVantageApiKey()
    {
        var api = CreateClient();
        var username = await EnsureAuthenticatedAsync(api);
        const string plaintext = "ALPHAVANTAGE-SECRET";

        var ok = await api.UserSettings_UpdateProfileAsync(new UserProfileSettingsUpdateRequest(
            PreferredLanguage: null,
            TimeZoneId: null,
            AlphaVantageApiKey: plaintext,
            ClearAlphaVantageApiKey: null,
            ShareAlphaVantageApiKey: null));

        ok.Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IAlphaVantageSecretProtector>();
        var stored = await db.Users
            .Where(u => u.UserName == username)
            .Select(u => u.AlphaVantageApiKey)
            .SingleAsync();
        stored.Should().NotBeNull();
        stored.Should().NotBe(plaintext);
        stored.Should().StartWith(DataProtectionAlphaVantageSecretProtector.ProtectedPrefix);
        protector.Unprotect(stored).Should().Be(plaintext);

        var profile = await api.UserSettings_GetProfileAsync();
        profile!.HasAlphaVantageApiKey.Should().BeTrue();
    }

    [Fact]
    public async Task UserSettings_UpdateProfile_ClearAlphaVantageApiKey_RemovesStoredValue()
    {
        var api = CreateClient();
        var username = await EnsureAuthenticatedAsync(api);
        await api.UserSettings_UpdateProfileAsync(new UserProfileSettingsUpdateRequest(
            PreferredLanguage: null,
            TimeZoneId: null,
            AlphaVantageApiKey: "ALPHAVANTAGE-SECRET",
            ClearAlphaVantageApiKey: null,
            ShareAlphaVantageApiKey: null));

        var ok = await api.UserSettings_UpdateProfileAsync(new UserProfileSettingsUpdateRequest(
            PreferredLanguage: null,
            TimeZoneId: null,
            AlphaVantageApiKey: null,
            ClearAlphaVantageApiKey: true,
            ShareAlphaVantageApiKey: null));

        ok.Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Users
            .Where(u => u.UserName == username)
            .Select(u => u.AlphaVantageApiKey)
            .SingleAsync();
        stored.Should().BeNull();
    }

    [Fact]
    public async Task UserSettings_GetNotifications_Returns_Defaults()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var notifications = await api.User_GetNotificationSettingsAsync();
        notifications.Should().NotBeNull();
        notifications!.MonthlyReminderEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UserSettings_UpdateNotifications_Works()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var ok = await api.User_UpdateNotificationSettingsAsync(
            monthlyEnabled: true,
            hour: 10,
            minute: 30,
            provider: "Memory",
            country: null,
            subdivision: null);
        ok.Should().BeTrue();

        var notifications = await api.User_GetNotificationSettingsAsync();
        notifications.Should().NotBeNull();
        notifications!.MonthlyReminderEnabled.Should().BeTrue();
        notifications.MonthlyReminderHour.Should().Be(10);
        notifications.MonthlyReminderMinute.Should().Be(30);
    }

    [Fact]
    public async Task UserSettings_GetImportSplit_Returns_Defaults()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var split = await api.UserSettings_GetImportSplitAsync();
        split.Should().NotBeNull();
        split!.Mode.Should().Be(ImportSplitMode.MonthlyOrFixed);
        split.MaxEntriesPerDraft.Should().Be(250);
        split.MassImportDialogPolicy.Should().Be(MassImportDialogPolicy.OnMissingInformation);
    }

    [Fact]
    public async Task UserSettings_UpdateImportSplit_Works()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var ok = await api.UserSettings_UpdateImportSplitAsync(new ImportSplitSettingsUpdateRequest(
            Mode: ImportSplitMode.FixedSize,
            MaxEntriesPerDraft: 100,
            MonthlySplitThreshold: null,
            MinEntriesPerDraft: 5,
            MassImportDialogPolicy: MassImportDialogPolicy.AlwaysConfirm));
        ok.Should().BeTrue();

        var split = await api.UserSettings_GetImportSplitAsync();
        split.Should().NotBeNull();
        split!.Mode.Should().Be(ImportSplitMode.FixedSize);
        split.MaxEntriesPerDraft.Should().Be(100);
        split.MinEntriesPerDraft.Should().Be(5);
        split.MassImportDialogPolicy.Should().Be(MassImportDialogPolicy.AlwaysConfirm);
    }
}
