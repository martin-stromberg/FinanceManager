using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateServiceProbe"/> implementation, detecting Windows services via <c>sc.exe</c>
/// and Linux systemd units via the process cgroup or <c>systemctl</c>.
/// </summary>
public sealed partial class DefaultAutoUpdateServiceProbe : IAutoUpdateServiceProbe
{
    private const int ServiceProbeTimeoutMs = 3000;

    private readonly ILogger<DefaultAutoUpdateServiceProbe> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAutoUpdateServiceProbe"/> class.
    /// </summary>
    /// <param name="logger">Used to log the underlying command when service detection fails.</param>
    public DefaultAutoUpdateServiceProbe(ILogger<DefaultAutoUpdateServiceProbe> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FindWindowsServicesForCurrentProcess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Array.Empty<string>();
        }

        try
        {
            var output = ProcessOutputReader.Read("sc.exe", "queryex type= service state= all", timeoutMs: ServiceProbeTimeoutMs, logger: _logger);
            var services = new List<string>();
            string? currentService = null;
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase))
                {
                    currentService = trimmed["SERVICE_NAME:".Length..].Trim();
                    continue;
                }

                if (currentService is not null &&
                    trimmed.StartsWith("PID", StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains($": {Environment.ProcessId}", StringComparison.Ordinal))
                {
                    services.Add(currentService);
                }
            }

            return services;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Windows service detection via 'sc.exe queryex' failed.");
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FindLinuxServicesForCurrentProcess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Array.Empty<string>();
        }

        var fromCgroup = TryReadSystemdServiceFromCgroup();
        if (!string.IsNullOrWhiteSpace(fromCgroup))
        {
            return new[] { fromCgroup };
        }

        try
        {
            var output = ProcessOutputReader.Read("systemctl", $"status {Environment.ProcessId}", timeoutMs: ServiceProbeTimeoutMs, logger: _logger);
            var matches = SystemdServiceRegex().Matches(output)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Linux service detection via 'systemctl status {ProcessId}' failed.", Environment.ProcessId);
            return Array.Empty<string>();
        }
    }

    private static string? TryReadSystemdServiceFromCgroup()
    {
        const string cgroupPath = "/proc/self/cgroup";
        if (!File.Exists(cgroupPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(cgroupPath))
        {
            var match = SystemdServiceRegex().Match(line);
            if (match.Success)
            {
                return Uri.UnescapeDataString(match.Value);
            }
        }

        return null;
    }

    [GeneratedRegex(@"[A-Za-z0-9_.@-]+\.service", RegexOptions.CultureInvariant)]
    private static partial Regex SystemdServiceRegex();
}
