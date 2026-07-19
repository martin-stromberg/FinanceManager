#pragma warning disable CS1591
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateServiceResolver : IUpdateServiceResolver
{
    private readonly IWebHostEnvironment _environment;
    private readonly IUpdateServiceProbe _probe;

    public UpdateServiceResolver(IWebHostEnvironment environment, IUpdateServiceProbe probe)
    {
        _environment = environment;
        _probe = probe;
    }

    public UpdateInstallationTarget Resolve(UpdateSettingsDto settings)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ResolveWindows(settings);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ResolveLinux(settings);
        }

        throw new InvalidOperationException("Unsupported platform for self update.");
    }

    private UpdateInstallationTarget ResolveWindows(UpdateSettingsDto settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ServiceName))
        {
            return new UpdateInstallationTarget("windows", ValidateServiceName(settings.ServiceName, "Service name"), null);
        }

        if (!string.IsNullOrWhiteSpace(settings.ExecutablePath))
        {
            var executable = ValidateExecutablePath(settings.ExecutablePath);
            return new UpdateInstallationTarget("windows", null, executable);
        }

        var detected = Distinct(_probe.FindWindowsServicesForCurrentProcess());
        if (detected.Count == 1)
        {
            return new UpdateInstallationTarget("windows", detected[0], null);
        }

        if (detected.Count > 1)
        {
            throw new InvalidOperationException($"Multiple Windows services match the current process ({string.Join(", ", detected)}). Configure the service name explicitly before starting installation.");
        }

        throw new InvalidOperationException("Configure a service name or executable path before starting installation.");
    }

    private UpdateInstallationTarget ResolveLinux(UpdateSettingsDto settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ServiceName))
        {
            return new UpdateInstallationTarget("linux", ValidateServiceName(settings.ServiceName, "Service name"), null);
        }

        var detected = Distinct(_probe.FindLinuxServicesForCurrentProcess());
        if (detected.Count == 1)
        {
            return new UpdateInstallationTarget("linux", detected[0], null);
        }

        if (detected.Count > 1)
        {
            throw new InvalidOperationException($"Multiple Linux systemd services match the current process ({string.Join(", ", detected)}). Configure the service name explicitly before starting installation.");
        }

        throw new InvalidOperationException("Configure a service name before starting installation.");
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

        var appRoot = Path.GetFullPath(_environment.ContentRootPath);
        if (!fullPath.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Executable path must point to the current application directory.");
        }

        return fullPath;
    }

    private static string ValidateServiceName(string value, string label)
    {
        var serviceName = value.Trim();
        if (serviceName.Length == 0 ||
            serviceName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            serviceName.Contains('/') ||
            serviceName.Contains('\\'))
        {
            throw new InvalidOperationException($"{label} is invalid.");
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

public sealed partial class DefaultUpdateServiceProbe : IUpdateServiceProbe
{
    public IReadOnlyList<string> FindWindowsServicesForCurrentProcess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Array.Empty<string>();
        }

        try
        {
            var output = Run("sc.exe", "queryex type= service state= all");
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
        catch
        {
            return Array.Empty<string>();
        }
    }

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
            var output = Run("systemctl", $"status {Environment.ProcessId}");
            var matches = SystemdServiceRegex().Matches(output)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return matches;
        }
        catch
        {
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

    private static string Run(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(3000);
        return output;
    }

    [GeneratedRegex(@"[A-Za-z0-9_.@-]+\.service", RegexOptions.CultureInvariant)]
    private static partial Regex SystemdServiceRegex();
}
#pragma warning restore CS1591
