namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateScriptGenerator"/> implementation, generating a PowerShell script on Windows
/// and a POSIX shell script on Linux. macOS and other platforms are not supported.
/// </summary>
public sealed class AutoUpdateScriptGenerator : IAutoUpdateScriptGenerator
{
    private readonly IAutoUpdateEnvironment _environment;
    private readonly IAutoUpdatePackageStore _packageStore;
    private readonly IAutoUpdatePlatformResolver _platformResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateScriptGenerator"/> class.
    /// </summary>
    /// <param name="environment">Provides the application's root directory the package is extracted into.</param>
    /// <param name="packageStore">Provides the staging directory and script/lock paths.</param>
    /// <param name="platformResolver">Used to determine the current platform. Defaults to a new <see cref="AutoUpdatePlatformResolver"/>.</param>
    public AutoUpdateScriptGenerator(IAutoUpdateEnvironment environment, IAutoUpdatePackageStore packageStore, IAutoUpdatePlatformResolver? platformResolver = null)
    {
        _environment = environment;
        _packageStore = packageStore;
        _platformResolver = platformResolver ?? new AutoUpdatePlatformResolver();
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(AutoUpdatePackageDescriptor package, string zipPath, AutoUpdateInstallationTarget target, CancellationToken ct = default)
    {
        await _packageStore.EnsureAsync(ct);
        if (_platformResolver.CurrentPlatform == AutoUpdatePlatformResolver.WindowsPlatform)
        {
            return await GenerateWindowsAsync(zipPath, target, ct);
        }

        if (_platformResolver.CurrentPlatform == AutoUpdatePlatformResolver.LinuxPlatform)
        {
            return await GenerateLinuxAsync(zipPath, target, ct);
        }

        throw new InvalidOperationException("Unsupported platform for self update.");
    }

    private async Task<string> GenerateWindowsAsync(string zipPath, AutoUpdateInstallationTarget target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target.ServiceName) && string.IsNullOrWhiteSpace(target.ExecutablePath))
        {
            throw new InvalidOperationException("Validated Windows service or executable target is required.");
        }

        var appDir = _environment.ApplicationDirectory;
        var staging = _packageStore.StagingDirectory;
        var script = _packageStore.ScriptPath("ps1");
        var stop = string.IsNullOrWhiteSpace(target.ServiceName)
            ? ""
            : $"Stop-Service -Name {Ps(target.ServiceName)} -ErrorAction SilentlyContinue\n";
        var start = !string.IsNullOrWhiteSpace(target.ServiceName)
            ? $"Start-Service -Name {Ps(target.ServiceName)}\n"
            : $"Start-Process -FilePath {Ps(target.ExecutablePath!)} -WorkingDirectory {Ps(appDir)}\n";

        var content = $$"""
$ErrorActionPreference = "Stop"
$zip = {{Ps(zipPath)}}
$app = {{Ps(appDir)}}
$staging = {{Ps(staging)}}
$lock = {{Ps(_packageStore.LockPath)}}
Start-Sleep -Seconds 3
{{stop}}if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Expand-Archive -LiteralPath $zip -DestinationPath $staging -Force
Get-ChildItem -LiteralPath $staging -Force | Copy-Item -Destination $app -Recurse -Force
if (Test-Path -LiteralPath $lock) { Remove-Item -LiteralPath $lock -Force }
{{start}}
""";
        await File.WriteAllTextAsync(script, content, ct);
        return script;
    }

    private async Task<string> GenerateLinuxAsync(string zipPath, AutoUpdateInstallationTarget target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target.ServiceName))
        {
            throw new InvalidOperationException("Validated Linux systemd service target is required.");
        }

        var appDir = _environment.ApplicationDirectory;
        var staging = _packageStore.StagingDirectory;
        var script = _packageStore.ScriptPath("sh");
        var log = _packageStore.LogPath;

        var content = $$"""
#!/usr/bin/env bash
set -euo pipefail

log={{Sh(log)}}
zip={{Sh(zipPath)}}
app={{Sh(appDir)}}
staging={{Sh(staging)}}
lock={{Sh(_packageStore.LockPath)}}

service={{Sh(target.ServiceName)}}

log_msg() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$log"
}
rm -f "$log"
log_msg "Update started."

sleep 3

log_msg "Stopping service $service..."
systemctl stop "$service" 2>&1 | tee -a "$log"

log_msg "Cleaning staging directory..."
rm -rf "$staging" 2>&1 | tee -a "$log"
mkdir -p "$staging" 2>&1 | tee -a "$log"

log_msg "Extracting archive: $zip"
unzip -o "$zip" -d "$staging" 2>&1 | tee -a "$log"

log_msg "Copying files to the live directory..."
cp -r "$staging"/. "$app"/ 2>&1 | tee -a "$log"

log_msg "Removing lock file..."
rm -f "$lock" 2>&1 | tee -a "$log"

log_msg "Starting service $service..."
systemctl start "$service" 2>&1 | tee -a "$log"

log_msg "Update completed successfully."
""";
        content = content
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
        await File.WriteAllTextAsync(script, content, ct);

        try
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch
        {
            // Best-effort only; some file systems do not support POSIX permissions.
        }

        return script;
    }

    private static string Ps(string value) => $"'{value.Replace("'", "''")}'";
    private static string Sh(string value) => $"'{value.Replace("'", "'\"'\"'")}'";
}
