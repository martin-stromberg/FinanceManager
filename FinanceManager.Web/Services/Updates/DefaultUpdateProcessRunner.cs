#pragma warning disable CS1591
using System.Diagnostics;

namespace FinanceManager.Web.Services.Updates;

public sealed class DefaultUpdateProcessRunner : IUpdateProcessRunner
{
    public void StartScript(string scriptPath)
    {
        var extension = Path.GetExtension(scriptPath);
        var startInfo = extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo("powershell.exe", $"-ExecutionPolicy Bypass -File \"{scriptPath}\"")
            : new ProcessStartInfo("/usr/bin/env", $"bash \"{scriptPath}\"");

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException("Update script process could not be started.");
        }
    }
}
#pragma warning restore CS1591
