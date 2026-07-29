using System.Runtime.InteropServices;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateServiceResolver"/> implementation, using the configured service name or
/// executable path and falling back to auto-detection via <see cref="IAutoUpdateServiceProbe"/>.
/// </summary>
public sealed class AutoUpdateServiceResolver : IAutoUpdateServiceResolver
{
    private readonly IAutoUpdateEnvironment _environment;
    private readonly IAutoUpdateServiceProbe _probe;
    private readonly AutoUpdateOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateServiceResolver"/> class.
    /// </summary>
    /// <param name="environment">Provides the application's root directory, used to validate the executable path.</param>
    /// <param name="probe">Used to auto-detect the current service when none is configured explicitly.</param>
    /// <param name="options">The runtime-mutable auto-update options.</param>
    public AutoUpdateServiceResolver(IAutoUpdateEnvironment environment, IAutoUpdateServiceProbe probe, AutoUpdateOptions options)
    {
        _environment = environment;
        _probe = probe;
        _options = options;
    }

    /// <inheritdoc />
    public AutoUpdateInstallationTarget Resolve()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ResolveWindows();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ResolveLinux();
        }

        throw new InvalidOperationException("Unsupported platform for self update.");
    }

    private AutoUpdateInstallationTarget ResolveWindows()
    {
        if (!string.IsNullOrWhiteSpace(_options.ServiceName))
        {
            return new AutoUpdateInstallationTarget("windows", ValidateServiceName(_options.ServiceName), null);
        }

        if (!string.IsNullOrWhiteSpace(_options.ExecutablePath))
        {
            var executable = ValidateExecutablePath(_options.ExecutablePath);
            return new AutoUpdateInstallationTarget("windows", null, executable);
        }

        return ResolveFromProbe("windows", "Windows services", "a service name or executable path", _probe.FindWindowsServicesForCurrentProcess());
    }

    private AutoUpdateInstallationTarget ResolveLinux()
    {
        if (!string.IsNullOrWhiteSpace(_options.ServiceName))
        {
            return new AutoUpdateInstallationTarget("linux", ValidateServiceName(_options.ServiceName), null);
        }

        return ResolveFromProbe("linux", "Linux systemd services", "a service name", _probe.FindLinuxServicesForCurrentProcess());
    }

    private AutoUpdateInstallationTarget ResolveFromProbe(string platform, string candidateLabel, string configurationHint, IReadOnlyList<string> probed)
    {
        var detected = Distinct(probed);
        if (detected.Count == 1)
        {
            return new AutoUpdateInstallationTarget(platform, detected[0], null);
        }

        if (detected.Count > 1)
        {
            throw new InvalidOperationException($"Multiple {candidateLabel} match the current process ({string.Join(", ", detected)}). Configure the service name explicitly before starting installation.");
        }

        throw new InvalidOperationException($"Configure {configurationHint} before starting installation.");
    }

    private string ValidateExecutablePath(string value)
    {
        var path = value.Trim();
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("Executable path must be absolute before starting installation.");
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException("Executable path does not exist. Configure the executable path before starting installation.");
        }

        var appRoot = Path.GetFullPath(_environment.ApplicationDirectory);
        if (!fullPath.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Executable path must point to the current application directory.");
        }

        return fullPath;
    }

    private static string ValidateServiceName(string value)
    {
        var serviceName = value.Trim();
        if (serviceName.Length == 0 ||
            serviceName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            serviceName.Contains('/') ||
            serviceName.Contains('\\'))
        {
            throw new InvalidOperationException("Service name is invalid.");
        }

        return serviceName;
    }

    private static List<string> Distinct(IReadOnlyList<string> names)
        => names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
