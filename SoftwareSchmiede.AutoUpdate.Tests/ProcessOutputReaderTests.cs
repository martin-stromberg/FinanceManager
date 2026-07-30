using FluentAssertions;
using SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class ProcessOutputReaderTests
{
    [Fact]
    public async Task Read_OnTimeout_KillsChildProcessInsteadOfLeavingItRunning()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            Assert.Skip("Windows- or Linux-only test.");
        }

        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var markerPath = Path.Combine(dir.FullName, "marker.txt");
            var (fileName, arguments) = OperatingSystem.IsWindows()
                ? ("powershell.exe", $"-NoProfile -Command \"Start-Sleep -Seconds 3; Set-Content -Path '{markerPath}' -Value done\"")
                : ("/bin/sh", $"-c \"sleep 3 && touch '{markerPath}'\"");

            var act = () => ProcessOutputReader.Read(fileName, arguments, timeoutMs: 200);

            act.Should().Throw<TimeoutException>();

            // If the process had not been killed, it would still write the marker file ~3s after being started.
            await Task.Delay(3500);
            File.Exists(markerPath).Should().BeFalse("the timed-out child process should have been killed instead of left running to completion");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Read_WithoutThrowOnNonZeroExitCode_LogsWarningWithStderr()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            Assert.Skip("Windows- or Linux-only test.");
        }

        var logger = new RecordingLogger();
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", "/c \"echo process-output-reader-test-stderr 1>&2 & exit 3\"")
            : ("/bin/sh", "-c \"echo process-output-reader-test-stderr >&2; exit 3\"");

        var output = ProcessOutputReader.Read(fileName, arguments, timeoutMs: 5000, throwOnNonZeroExitCode: false, logger: logger);

        output.Should().NotBeNull();
        logger.Messages.Should().Contain(message => message.Contains("process-output-reader-test-stderr", StringComparison.Ordinal));
    }
}
