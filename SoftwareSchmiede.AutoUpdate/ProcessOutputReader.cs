using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Shared helper for running a process and capturing its standard output, used by
/// <see cref="DefaultAutoUpdateProcessRunner"/> and <see cref="DefaultAutoUpdateServiceProbe"/>.
/// </summary>
internal static class ProcessOutputReader
{
    /// <summary>
    /// Starts <paramref name="fileName"/> with <paramref name="arguments"/>, waits for it to exit and returns its
    /// captured standard output.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">The command-line arguments to pass.</param>
    /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the process to exit.</param>
    /// <param name="throwOnNonZeroExitCode">When <see langword="true"/>, throws an <see cref="InvalidOperationException"/> if the process exits with a non-zero exit code.</param>
    /// <param name="logger">When <paramref name="throwOnNonZeroExitCode"/> is <see langword="false"/>, used to log a warning with the captured stderr if the process still exits with a non-zero exit code.</param>
    /// <returns>The captured standard output.</returns>
    public static string Read(string fileName, string arguments, int timeoutMs, bool throwOnNonZeroExitCode = false, ILogger? logger = null)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");

        // Read stdout/stderr concurrently before blocking on exit: reading them sequentially can deadlock if the
        // child process fills the other stream's buffer while this thread is still draining the first one.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            TryKill(process);
            ObserveExceptions(stdoutTask);
            ObserveExceptions(stderrTask);
            throw new TimeoutException($"Command '{fileName} {arguments}' did not exit within {timeoutMs}ms.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            if (throwOnNonZeroExitCode)
            {
                throw new InvalidOperationException(
                    $"Command '{fileName} {arguments}' failed with exit code {process.ExitCode}. Stdout: {stdout} Stderr: {stderr}");
            }

            logger?.LogWarning(
                "Command '{FileName} {Arguments}' exited with code {ExitCode}. Stderr: {Stderr}",
                fileName, arguments, process.ExitCode, stderr);
        }

        return stdout.Trim();
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited concurrently with the timeout check.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Best-effort only; the process could not be terminated.
        }
    }

    private static void ObserveExceptions(Task<string> task)
        => _ = task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
}
