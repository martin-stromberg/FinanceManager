#pragma warning disable CS1591
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FinanceManager.Web.Services.Updates;

public sealed class DefaultUpdateServiceCatalog : IUpdateServiceCatalog
{
    public async Task<IReadOnlyList<string>> ListServiceNamesAsync(string? query, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);

        try
        {
            IReadOnlyList<string> names;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var output = await RunAsync("sc.exe", new[] { "query", "type=", "service", "state=", "all" }, ct);
                names = ParseWindowsServiceNames(output);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var output = await RunAsync("systemctl", new[] { "list-units", "--type=service", "--all", "--no-legend", "--no-pager" }, ct);
                names = ParseLinuxServiceNames(output);
            }
            else
            {
                return Array.Empty<string>();
            }

            return Filter(names, query, take);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static IReadOnlyList<string> ParseWindowsServiceNames(string output)
        => output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["SERVICE_NAME:".Length..].Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<string> ParseLinuxServiceNames(string output)
        => output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(name => !string.IsNullOrWhiteSpace(name) && name.EndsWith(".service", StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<string> Filter(IReadOnlyList<string> names, string? query, int take)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var filtered = names.AsEnumerable();
        if (normalizedQuery is not null)
        {
            filtered = filtered.Where(name => name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.Take(take).ToArray();
    }

    private static async Task<string> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            return await outputTask;
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
#pragma warning restore CS1591
