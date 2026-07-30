using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateBuilderTests
{
    [Fact]
    public void Builder_FluentChain_SetsAllOptions()
    {
        var source = new FakeAutoUpdateSource();
        var builder = Host.CreateApplicationBuilder();
        builder.UseAutoUpdate(cfg => cfg
            .EnableAutomaticDownload("custom-downloads")
            .EnableAutomaticInstallation()
            .UseSource(source)
            .WithSourceCheck(42, new[] { new SourceCheckTimeRange { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) } })
            .DisableHostedServices());
        using var host = builder.Build();

        var options = host.Services.GetRequiredService<AutoUpdateOptions>();

        options.EnableAutomaticDownload.Should().BeTrue();
        options.DownloadPath.Should().Be("custom-downloads");
        options.EnableAutomaticInstallation.Should().BeTrue();
        options.Source.Should().BeSameAs(source);
        options.SourceCheck.Interval.Should().Be(42);
        options.SourceCheck.TimeRanges.Should().ContainSingle(range => range.DayOfWeek == DayOfWeek.Monday);
        options.HostedServicesEnabled.Should().BeFalse();
    }

    [Fact]
    public void Builder_UseGithubSource_CreatesGithubSource()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.UseAutoUpdate(cfg => cfg.UseGithubSource("Owner", "Repo").DisableHostedServices());
        using var host = builder.Build();

        var options = host.Services.GetRequiredService<AutoUpdateOptions>();

        options.Source.Should().BeOfType<AutoUpdateGithubSource>();
    }

    [Fact]
    public void Builder_BindConfiguration_ReadsSection()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CustomUpdates:MaxAssetBytes"] = "12345",
                ["CustomUpdates:HostedServicesEnabled"] = "false"
            });

            builder.UseAutoUpdate(cfg => cfg.BindConfiguration("CustomUpdates").UseLocalFolderSource(dir.FullName));
            using var host = builder.Build();

            var options = host.Services.GetRequiredService<AutoUpdateOptions>();

            options.MaxAssetBytes.Should().Be(12345);
            options.HostedServicesEnabled.Should().BeFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
