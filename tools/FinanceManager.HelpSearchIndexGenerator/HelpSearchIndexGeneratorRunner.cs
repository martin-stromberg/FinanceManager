using System.Security.Cryptography;
using System.Text;
using FinanceManager.Web.Services.Help;

namespace FinanceManager.HelpSearchIndexGenerator;

public static class HelpSearchIndexGeneratorRunner
{
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
