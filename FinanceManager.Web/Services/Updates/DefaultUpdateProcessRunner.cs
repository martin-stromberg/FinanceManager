#pragma warning disable CS1591
using System.Diagnostics;

namespace FinanceManager.Web.Services.Updates;

public sealed class DefaultUpdateProcessRunner : IUpdateProcessRunner
{
    private readonly ILogger<DefaultUpdateProcessRunner> _logger;

    public DefaultUpdateProcessRunner(ILogger<DefaultUpdateProcessRunner> logger)
    {
        _logger = logger;
    }

    private static string Run(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.Start();

        // Output + Error einlesen
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        // Fehlerbehandlung
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command '{fileName} {arguments}' failed with exit code {process.ExitCode}. " +
                $"Stdout: {stdout}  Stderr: {stderr}");
        }

        return stdout.Trim();
    }


    public void StartPrepareEnvironment(string scriptPath)
    {
        var extension = Path.GetExtension(scriptPath);
        var isWindows = extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);
        if (isWindows) return;        

        string loadState = Run("systemctl", "show FinanceManagerUpdate.service --property=LoadState")
            .Split('=')[1].Trim();

        string activeState = Run("systemctl", "show FinanceManagerUpdate.service --property=ActiveState")
            .Split('=')[1].Trim();

        bool unitExists = loadState != "not-found";
        bool unitRunning = activeState == "active";
        bool unitFailedOrHanging = unitExists && activeState != "active";

        if (unitFailedOrHanging)
        {
            Run("systemctl", "reset-failed FinanceManagerUpdate.service");
        }

        if (unitRunning)
        {
            throw new InvalidOperationException("Update läuft bereits.");
        }
    }

    public void StartScript(string scriptPath)
    {
        var extension = Path.GetExtension(scriptPath);
        var isWindows = extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);        
        var startInfo = isWindows
            ? new ProcessStartInfo("powershell.exe", $"-ExecutionPolicy Bypass -File \"{scriptPath}\"")
            : new ProcessStartInfo("systemd-run", $"--unit=FinanceManagerUpdate --service-type=exec /bin/bash {scriptPath}");

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        if (Process.Start(startInfo) is null)
        {
            _logger.LogError("Update script process could not be started for script: {ScriptPath}", scriptPath);
            throw new InvalidOperationException("Update script process could not be started.");
        }
        _logger.LogInformation("Update script process started for script: {ScriptPath}", scriptPath);
    }
}
#pragma warning restore CS1591
