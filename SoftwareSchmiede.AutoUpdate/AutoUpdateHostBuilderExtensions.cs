using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Registers the auto-update subsystem on an <see cref="IHostApplicationBuilder"/>. This is the only supported
/// entry point for consuming the library.
/// </summary>
public static class AutoUpdateHostBuilderExtensions
{
    /// <summary>
    /// The default configuration section name <see cref="AutoUpdateOptions"/> are bound from, unless overridden
    /// via <see cref="AutoUpdateBuilder.BindConfiguration"/>.
    /// </summary>
    public const string DefaultConfigurationSectionName = "AutoUpdate";

    private const string DefaultSourceDirectoryName = "source";

    /// <summary>
    /// Configures and registers the auto-update subsystem. Works with any host implementing
    /// <see cref="IHostApplicationBuilder"/> (web, worker or console), since no ASP.NET Core dependency is used.
    /// </summary>
    /// <param name="builder">The host application builder to register services on.</param>
    /// <param name="configure">An optional delegate configuring the auto-update options via <see cref="AutoUpdateBuilder"/>.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="OptionsValidationException">Thrown when the resulting <see cref="AutoUpdateOptions"/> are invalid.</exception>
    /// <remarks>
    /// Configuration is bound after <paramref name="configure"/> runs, so that values only present in configuration
    /// (e.g. <see cref="AutoUpdateOptions.Enabled"/>, which has no fluent setter) are filled in. Values explicitly
    /// set via the fluent <see cref="AutoUpdateBuilder"/> methods (<see cref="AutoUpdateBuilder.EnableAutomaticDownload"/>,
    /// <see cref="AutoUpdateBuilder.EnableAutomaticInstallation"/>, <see cref="AutoUpdateBuilder.WithSourceCheck"/>) take
    /// precedence and are re-applied after binding, so configuration cannot silently override explicit code configuration.
    /// </remarks>
    public static IHostApplicationBuilder UseAutoUpdate(this IHostApplicationBuilder builder, Action<AutoUpdateBuilder>? configure = null)
    {
        var autoUpdateBuilder = new AutoUpdateBuilder();
        configure?.Invoke(autoUpdateBuilder);

        var options = BuildOptions(builder, autoUpdateBuilder);

        builder.Services.AddSingleton(options);
        builder.Services.AddHttpClient();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<IAutoUpdateEnvironment, HostAutoUpdateEnvironment>();
        builder.Services.TryAddSingleton<IAutoUpdateEventAggregator, AutoUpdateEvents>();
        builder.Services.TryAddSingleton<IAutoUpdatePackageStore, FileSystemAutoUpdatePackageStore>();
        builder.Services.TryAddSingleton<IAutoUpdateStateStore, FileSystemAutoUpdateStateStore>();
        builder.Services.TryAddSingleton<IAutoUpdatePackageValidator, AutoUpdatePackageValidator>();
        builder.Services.TryAddSingleton<IInstalledVersionProvider, ReleaseMetadataInstalledVersionProvider>();
        builder.Services.TryAddSingleton<IAutoUpdatePlatformResolver, AutoUpdatePlatformResolver>();
        builder.Services.TryAddSingleton<IAutoUpdateServiceProbe, DefaultAutoUpdateServiceProbe>();
        builder.Services.TryAddSingleton<IAutoUpdateServiceResolver, AutoUpdateServiceResolver>();
        builder.Services.TryAddSingleton<IAutoUpdateScriptGenerator, AutoUpdateScriptGenerator>();
        builder.Services.TryAddSingleton<IAutoUpdateProcessRunner, DefaultAutoUpdateProcessRunner>();
        builder.Services.TryAddSingleton<IAutoUpdateHostTerminator, DefaultAutoUpdateHostTerminator>();
        builder.Services.TryAddSingleton<IAutoUpdateInstaller, AutoUpdateInstaller>();
        builder.Services.TryAddSingleton<AutoUpdateStatusService>();
        builder.Services.TryAddSingleton<IAutoUpdateStatusProvider>(sp => sp.GetRequiredService<AutoUpdateStatusService>());
        builder.Services.TryAddSingleton<SourceCheckWindowEvaluator>();
        builder.Services.TryAddSingleton<ScheduledInstallEvaluator>();
        builder.Services.TryAddSingleton<IAutoUpdateOrchestrator, AutoUpdateOrchestrator>();
        builder.Services.TryAddSingleton<IAutoUpdateCommandHandler, AutoUpdateCommandService>();

        if (options.HostedServicesEnabled)
        {
            builder.Services.AddHostedService<AutoUpdateCheckerService>();
            builder.Services.AddHostedService<AutoUpdateSchedulerService>();
        }

        return builder;
    }

    /// <summary>
    /// Builds and validates the effective <see cref="AutoUpdateOptions"/> for <paramref name="builder"/>: binds
    /// configuration, re-applies explicit fluent values, clamps <see cref="AutoUpdateOptions.HealthTimeoutSeconds"/>,
    /// derives the default source when none was configured and validates the result.
    /// </summary>
    /// <param name="builder">The host application builder configuration is read from.</param>
    /// <param name="autoUpdateBuilder">The builder holding the fluent configuration applied by the caller's configure delegate.</param>
    /// <returns>The validated, effective options.</returns>
    /// <exception cref="OptionsValidationException">Thrown when the resulting options are invalid.</exception>
    private static AutoUpdateOptions BuildOptions(IHostApplicationBuilder builder, AutoUpdateBuilder autoUpdateBuilder)
    {
        builder.Configuration.GetSection(autoUpdateBuilder.ConfigurationSectionName).Bind(autoUpdateBuilder.Options);

        var options = autoUpdateBuilder.Options;
        ReapplyExplicitValues(autoUpdateBuilder, options);
        options.HealthTimeoutSeconds = Math.Clamp(options.HealthTimeoutSeconds, AutoUpdateOptions.MinHealthTimeoutSeconds, AutoUpdateOptions.MaxHealthTimeoutSeconds);

        if (options.Source is null)
        {
            var downloadRoot = Path.IsPathRooted(options.DownloadPath)
                ? options.DownloadPath
                : Path.Combine(builder.Environment.ContentRootPath, options.DownloadPath);
            options.Source = new AutoUpdateLocalFolderSource(Path.Combine(downloadRoot, DefaultSourceDirectoryName));
        }

        var validationResult = new AutoUpdateOptionsValidator().Validate(autoUpdateBuilder.ConfigurationSectionName, options);
        if (validationResult.Failed)
        {
            throw new OptionsValidationException(autoUpdateBuilder.ConfigurationSectionName, typeof(AutoUpdateOptions), validationResult.Failures);
        }

        return options;
    }

    /// <summary>
    /// Re-applies the fluent <see cref="AutoUpdateBuilder"/> values onto <paramref name="options"/> after
    /// configuration binding, so that explicit code configuration is not silently discarded by a matching
    /// configuration key.
    /// </summary>
    /// <param name="autoUpdateBuilder">The builder holding the explicitly-set values.</param>
    /// <param name="options">The options instance configuration was just bound onto.</param>
    private static void ReapplyExplicitValues(AutoUpdateBuilder autoUpdateBuilder, AutoUpdateOptions options)
    {
        if (autoUpdateBuilder.ExplicitEnableAutomaticDownload)
        {
            options.EnableAutomaticDownload = true;
        }

        if (autoUpdateBuilder.ExplicitEnableAutomaticInstallation)
        {
            options.EnableAutomaticInstallation = true;
        }

        if (autoUpdateBuilder.ExplicitDownloadPath is { } explicitDownloadPath)
        {
            options.DownloadPath = explicitDownloadPath;
        }

        if (autoUpdateBuilder.ExplicitSourceCheckInterval is { } explicitInterval)
        {
            options.SourceCheck.Interval = explicitInterval;
        }

        if (autoUpdateBuilder.ExplicitSourceCheckTimeRanges is { } explicitTimeRanges)
        {
            options.SourceCheck.TimeRanges = explicitTimeRanges.ToList();
        }

        if (autoUpdateBuilder.ExplicitUpdateUnitName is { } explicitUpdateUnitName)
        {
            options.UpdateUnitName = explicitUpdateUnitName;
        }
    }
}
