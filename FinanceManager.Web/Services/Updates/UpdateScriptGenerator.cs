#pragma warning disable CS1591
using System.Runtime.InteropServices;
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateScriptGenerator : IUpdateScriptGenerator
{
    private readonly IWebHostEnvironment _environment;
    private readonly IUpdateFileStore _fileStore;

    public UpdateScriptGenerator(IWebHostEnvironment environment, IUpdateFileStore fileStore)
    {
        _environment = environment;
        _fileStore = fileStore;
    }

    public async Task<string> GenerateAsync(UpdateAssetDto asset, string zipPath, UpdateSettingsDto settings, UpdateInstallationTarget target, CancellationToken ct = default)
    {
        await _fileStore.EnsureAsync(ct);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await GenerateWindowsAsync(zipPath, target, ct);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await GenerateLinuxAsync(zipPath, target, ct);
        }

        throw new InvalidOperationException("Unsupported platform for self update.");
    }

    private async Task<string> GenerateWindowsAsync(string zipPath, UpdateInstallationTarget target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target.ServiceName) && string.IsNullOrWhiteSpace(target.ExecutablePath))
        {
            throw new InvalidOperationException("Validated Windows service or executable target is required.");
        }

        var appDir = _environment.ContentRootPath;
        var staging = _fileStore.StagingDirectory;
        var script = _fileStore.ScriptPath("ps1");
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
$lock = {{Ps(_fileStore.LockPath)}}
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

    private async Task<string> GenerateLinuxAsync(string zipPath, UpdateInstallationTarget target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target.ServiceName))
        {
            throw new InvalidOperationException("Validated Linux systemd service target is required.");
        }

        var appDir = _environment.ContentRootPath;
        var staging = _fileStore.StagingDirectory;
        var script = _fileStore.ScriptPath("sh");
        var content = $$"""
#!/usr/bin/env bash
set -euo pipefail
zip={{Sh(zipPath)}}
app={{Sh(appDir)}}
staging={{Sh(staging)}}
lock={{Sh(_fileStore.LockPath)}}
sleep 3
systemctl stop {{Sh(target.ServiceName)}}
rm -rf "$staging"
mkdir -p "$staging"
unzip -o "$zip" -d "$staging"
cp -a "$staging"/. "$app"/
rm -f "$lock"
systemctl start {{Sh(target.ServiceName)}}
""";
        await File.WriteAllTextAsync(script, content, ct);
        try
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch
        {
        }

        return script;
    }

    private static string Ps(string value) => $"'{value.Replace("'", "''")}'";
    private static string Sh(string value) => $"'{value.Replace("'", "'\"'\"'")}'";
}
#pragma warning restore CS1591
