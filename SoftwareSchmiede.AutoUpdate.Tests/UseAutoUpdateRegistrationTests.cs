using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class UseAutoUpdateRegistrationTests
{
    [Fact]
    public void UseAutoUpdate_RegistersAllServices()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.UseAutoUpdate(cfg => cfg.UseLocalFolderSource(dir.FullName).DisableHostedServices());
            using var host = builder.Build();

            host.Services.GetRequiredService<AutoUpdateOptions>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateEnvironment>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateEventAggregator>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdatePackageStore>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateStateStore>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdatePackageValidator>().Should().NotBeNull();
            host.Services.GetRequiredService<IInstalledVersionProvider>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdatePlatformResolver>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateServiceProbe>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateServiceResolver>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateScriptGenerator>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateProcessRunner>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateHostTerminator>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateInstaller>().Should().NotBeNull();
            host.Services.GetRequiredService<AutoUpdateStatusService>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateStatusProvider>().Should().NotBeNull();
            host.Services.GetRequiredService<SourceCheckWindowEvaluator>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateOrchestrator>().Should().NotBeNull();
            host.Services.GetRequiredService<IAutoUpdateCommandHandler>().Should().NotBeNull();
            host.Services.GetRequiredService<TimeProvider>().Should().NotBeNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void UseAutoUpdate_WithoutSource_UsesLocalFolderSource()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.UseAutoUpdate(cfg => cfg.DisableHostedServices());
        using var host = builder.Build();

        var options = host.Services.GetRequiredService<AutoUpdateOptions>();

        options.Source.Should().BeOfType<AutoUpdateLocalFolderSource>();
    }

    [Fact]
    public void UseAutoUpdate_WhenHostedServicesDisabled_RegistersNoHostedService()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.UseAutoUpdate(cfg => cfg.UseLocalFolderSource(dir.FullName).DisableHostedServices());
            using var host = builder.Build();

            var hostedServices = host.Services.GetServices<IHostedService>();

            hostedServices.Should().NotContain(service => service is AutoUpdateCheckerService || service is AutoUpdateSchedulerService);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void UseAutoUpdate_ExplicitSourceCheckInterval_TakesPrecedenceOverConfiguration()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AutoUpdate:SourceCheck:Interval"] = "999",
            });
            builder.UseAutoUpdate(cfg => cfg.UseLocalFolderSource(dir.FullName).WithSourceCheck(30).DisableHostedServices());
            using var host = builder.Build();

            var options = host.Services.GetRequiredService<AutoUpdateOptions>();

            options.SourceCheck.Interval.Should().Be(30);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void UseAutoUpdate_ExplicitDownloadPath_TakesPrecedenceOverConfiguration()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AutoUpdate:DownloadPath"] = "from-config",
            });
            builder.UseAutoUpdate(cfg => cfg.EnableAutomaticDownload("from-code").UseLocalFolderSource(dir.FullName).DisableHostedServices());
            using var host = builder.Build();

            var options = host.Services.GetRequiredService<AutoUpdateOptions>();

            options.DownloadPath.Should().Be("from-code");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void UseAutoUpdate_WithoutFluentSourceCheck_UsesConfiguredInterval()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AutoUpdate:SourceCheck:Interval"] = "45",
            });
            builder.UseAutoUpdate(cfg => cfg.UseLocalFolderSource(dir.FullName).DisableHostedServices());
            using var host = builder.Build();

            var options = host.Services.GetRequiredService<AutoUpdateOptions>();

            options.SourceCheck.Interval.Should().Be(45);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void UseAutoUpdate_DoesNotOverrideExistingTimeProvider()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var builder = Host.CreateApplicationBuilder();
            var customTimeProvider = new FakeTimeProvider();
            builder.Services.AddSingleton<TimeProvider>(customTimeProvider);
            builder.UseAutoUpdate(cfg => cfg.UseLocalFolderSource(dir.FullName).DisableHostedServices());
            using var host = builder.Build();

            host.Services.GetRequiredService<TimeProvider>().Should().BeSameAs(customTimeProvider);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
