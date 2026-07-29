using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Tests.Updates;

public sealed class AutoUpdateOptionsMapperTests
{
    [Fact]
    public void ApplySettings_CopiesRuntimeRelevantFieldsOntoOptions()
    {
        var options = new AutoUpdateOptions();
        var settings = new UpdateSettingsDto(true, 45, "owner", "repo", "update.json", new TimeOnly(3, 30), "svc", "/path/exe", "custom-updates", 90);

        AutoUpdateOptionsMapper.ApplySettings(options, settings);

        options.Enabled.Should().BeTrue();
        options.SourceCheck.Interval.Should().Be(45);
        options.ServiceName.Should().Be("svc");
        options.ExecutablePath.Should().Be("/path/exe");
        options.DownloadPath.Should().Be("custom-updates");
        options.HealthTimeoutSeconds.Should().Be(90);
        options.ScheduledInstallTime.Should().Be(new TimeOnly(3, 30));
    }

    [Fact]
    public void ToSettingsDto_ReflectsCurrentOptionsState()
    {
        var options = new AutoUpdateOptions
        {
            Enabled = true,
            ServiceName = "svc",
            ExecutablePath = "/path/exe",
            DownloadPath = "custom-updates",
            HealthTimeoutSeconds = 90,
            ScheduledInstallTime = new TimeOnly(3, 30),
        };
        options.SourceCheck.Interval = 45;

        var dto = AutoUpdateOptionsMapper.ToSettingsDto(options, "owner", "repo", "update.json");

        dto.Enabled.Should().BeTrue();
        dto.CheckIntervalMinutes.Should().Be(45);
        dto.RepositoryOwner.Should().Be("owner");
        dto.RepositoryName.Should().Be("repo");
        dto.ManifestAssetName.Should().Be("update.json");
        dto.ServiceName.Should().Be("svc");
        dto.ExecutablePath.Should().Be("/path/exe");
        dto.WorkingDirectory.Should().Be("custom-updates");
        dto.HealthTimeoutSeconds.Should().Be(90);
        dto.ScheduledInstallTime.Should().Be(new TimeOnly(3, 30));
    }

    [Fact]
    public void ApplySettings_ThenToSettingsDto_RoundTripsRuntimeRelevantFields()
    {
        var options = new AutoUpdateOptions();
        var original = new UpdateSettingsDto(false, 20, "owner", "repo", "update.json", null, null, null, "updates", 30);

        AutoUpdateOptionsMapper.ApplySettings(options, original);
        var roundTripped = AutoUpdateOptionsMapper.ToSettingsDto(options, original.RepositoryOwner, original.RepositoryName, original.ManifestAssetName);

        roundTripped.Should().Be(original);
    }
}
