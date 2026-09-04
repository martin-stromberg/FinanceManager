using System.Security.Cryptography;
using System.Text;
using FinanceManager.Web.Services.Help;

namespace FinanceManager.HelpSearchIndexGenerator;

/// <summary>
/// Entry point logic for the help-search-index build tool: it turns the markdown help documentation under
/// <c>docs/help</c> into per-language <c>search-index.json</c> files consumed by the in-app help search, and
/// optionally emits a SHA-256 manifest of the generated/static help assets so packaging or update steps can
/// verify nothing was corrupted or left stale. Kept as testable logic separate from a thin console
/// <c>Main</c> so the CLI argument handling and file generation can be exercised directly from the test suite.
/// </summary>
public static class HelpSearchIndexGeneratorRunner
{
    /// <summary>
    /// Runs the generator: validates the CLI arguments, builds a search index JSON file per requested language
    /// under <paramref name="args"/>[1], and - when a fourth argument (source help root) is supplied - writes
    /// a SHA-256 manifest of the generated and static help assets. Errors and usage information are written to
    /// <paramref name="error"/> rather than thrown, so the process exit code (0 success, 1 generation failure,
    /// 2 bad usage) is the caller's primary signal.
    /// </summary>
    /// <param name="args">Command-line arguments: docs/help source path, output help root, semicolon/comma-separated language codes, and an optional source help root for manifest generation.</param>
    /// <param name="error">Writer that receives usage and error messages.</param>
    /// <returns>0 on success, 1 if generation failed for a language or the docs path is missing, 2 on invalid arguments.</returns>
    public static int Run(string[] args, TextWriter error)
    {
        if (args.Length is not 3 and not 4)
        {
            error.WriteLine("Usage: FinanceManager.HelpSearchIndexGenerator <docs-help-path> <output-help-root> <languages> [source-help-root]");
            return 2;
        }

        var docsPath = Path.GetFullPath(args[0]);
        var outputRoot = Path.GetFullPath(args[1]);
        var languages = args[2]
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(language => language.ToLowerInvariant())
            .ToArray();

        if (!Directory.Exists(docsPath))
        {
            error.WriteLine($"Docs/help source directory does not exist: {docsPath}");
            return 1;
        }

        using var outputLock = CreateOutputLock(outputRoot);
        outputLock.WaitOne();
        try
        {
            foreach (var language in languages)
            {
                if (!HelpLanguages.TryNormalize(language, out var normalizedLanguage))
                {
                    error.WriteLine($"Unsupported help language: {language}");
                    return 1;
                }

                var outputPath = Path.Combine(outputRoot, normalizedLanguage, "search-index.json");
                var documentCount = HelpSearchIndexBuilder.WriteToFile(docsPath, normalizedLanguage, outputPath);
                if (documentCount == 0)
                {
                    File.Delete(outputPath);
                    error.WriteLine($"No help search documents generated for language: {normalizedLanguage}");
                    return 1;
                }
            }

            if (args.Length == 4)
            {
                WriteManifest(docsPath, outputRoot, Path.GetFullPath(args[3]));
            }
        }
        finally
        {
            outputLock.ReleaseMutex();
        }

        return 0;
    }

    private static void WriteManifest(string docsPath, string outputRoot, string sourceHelpRoot)
    {
        var webProjectRoot = Directory.GetParent(Directory.GetParent(sourceHelpRoot)!.FullName)!.FullName;
        var manifestPath = Path.Combine(outputRoot, "help-assets.sha256");
        var entries = EnumerateManifestEntries(docsPath, outputRoot, sourceHelpRoot, webProjectRoot)
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{entry.Key}|{ComputeSha256(entry.FullPath)}");

        Directory.CreateDirectory(outputRoot);
        File.WriteAllLines(manifestPath, entries);
    }

    private static IEnumerable<(string Key, string FullPath)> EnumerateManifestEntries(
        string docsPath,
        string outputRoot,
        string sourceHelpRoot,
        string webProjectRoot)
    {
        if (Directory.Exists(sourceHelpRoot))
        {
            foreach (var file in Directory.EnumerateFiles(sourceHelpRoot, "*.*", SearchOption.AllDirectories)
                .Where(IsSourceStaticHelpAsset))
            {
                yield return ($"wwwroot/help/{NormalizePath(Path.GetRelativePath(sourceHelpRoot, file))}", file);
            }
        }

        if (Directory.Exists(outputRoot))
        {
            foreach (var file in Directory.EnumerateFiles(outputRoot, "*.json", SearchOption.AllDirectories))
            {
                yield return ($"wwwroot/help/{NormalizePath(Path.GetRelativePath(outputRoot, file))}", file);
            }
        }

        foreach (var file in Directory.EnumerateFiles(docsPath, "*.md", SearchOption.AllDirectories))
        {
            yield return (NormalizePath(Path.GetRelativePath(webProjectRoot, file)), file);
        }
    }

    private static bool IsSourceStaticHelpAsset(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".html", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static Mutex CreateOutputLock(string outputRoot)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(outputRoot.ToUpperInvariant())));
        return new Mutex(initiallyOwned: false, $"FinanceManager.HelpSearchIndexGenerator.{hash}");
    }
}
