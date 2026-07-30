using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateProcessRunner"/> implementation, starting the installation script as a
/// detached process via PowerShell on Windows and <c>systemd-run</c> on Linux.
/// </summary>
public sealed class DefaultAutoUpdateProcessRunner : IAutoUpdateProcessRunner
{
    private const int SystemctlTimeoutMs = 10000;

    private readonly ILogger<DefaultAutoUpdateProcessRunner> _logger;
    private readonly AutoUpdateOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAutoUpdateProcessRunner"/> class.
    /// </summary>
    /// <param name="logger">Used to log process start outcomes.</param>
    /// <param name="options">The runtime-mutable auto-update options, used for the configured systemd unit name.</param>
    public DefaultAutoUpdateProcessRunner(ILogger<DefaultAutoUpdateProcessRunner> logger, AutoUpdateOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public void EnsureUpdateUnitAvailable(string scriptPath)
    {
        if (IsPowerShellScript(scriptPath))
        {
            return;
        }

        var unitName = _options.UpdateUnitName;
        var loadState = ReadUnitProperty(unitName, "LoadState");
        var activeState = ReadUnitProperty(unitName, "ActiveState");

        var unitRunning = activeState == "active";
        var unitFailedOrHanging = loadState != "not-found" && activeState != "active";

        if (unitFailedOrHanging)
        {
            ProcessOutputReader.Read("systemctl", $"reset-failed {unitName}.service", timeoutMs: SystemctlTimeoutMs, throwOnNonZeroExitCode: true);
        }

        if (unitRunning)
        {
            throw new InvalidOperationException("An update is already running.");
        }
    }

    /// <inheritdoc />
    public void StartScript(string scriptPath)
    {
        var startInfo = IsPowerShellScript(scriptPath)
            ? CreatePowerShellStartInfo(scriptPath)
            : CreateSystemdRunStartInfo(scriptPath);

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        if (Process.Start(startInfo) is null)
        {
            _logger.LogError("Update script process could not be started for script: {ScriptPath}", scriptPath);
            throw new InvalidOperationException("Update script process could not be started.");
        }

        _logger.LogInformation("Update script process started for script: {ScriptPath}", scriptPath);
    }

    private static ProcessStartInfo CreatePowerShellStartInfo(string scriptPath)
    {
        var startInfo = new ProcessStartInfo("powershell.exe");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        return startInfo;
    }

    private ProcessStartInfo CreateSystemdRunStartInfo(string scriptPath)
    {
        var startInfo = new ProcessStartInfo("systemd-run");
        startInfo.ArgumentList.Add($"--unit={_options.UpdateUnitName}");
        startInfo.ArgumentList.Add("--service-type=exec");
        startInfo.ArgumentList.Add("/bin/bash");
        startInfo.ArgumentList.Add(scriptPath);
        return startInfo;
    }

    private static bool IsPowerShellScript(string scriptPath)
        => Path.GetExtension(scriptPath).Equals(".ps1", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a single systemd unit property via <c>systemctl show</c>. Returns an empty string when the output
    /// is empty or does not contain the expected <c>key=value</c> format, instead of throwing.
    /// </summary>
    /// <param name="unitName">The systemd unit name, without the <c>.service</c> suffix.</param>
    /// <param name="property">The property to read, e.g. <c>LoadState</c>.</param>
    /// <returns>The property value, or an empty string when it could not be determined.</returns>
    private string ReadUnitProperty(string unitName, string property)
    {
        var output = ProcessOutputReader.Read("systemctl", $"show {unitName}.service --property={property}", timeoutMs: SystemctlTimeoutMs, logger: _logger);
        var parts = output.Split('=', 2);
        return parts.Length < 2 ? string.Empty : parts[1].Trim();
    }
}
