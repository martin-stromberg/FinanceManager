using System.Security.Cryptography;
using System.Xml.Linq;
using FinanceManager.HelpSearchIndexGenerator;
using FinanceManager.Web.Services.Help;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Web.Help;

public sealed class HelpAssetIntegrityValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fm-help-integrity-{Guid.NewGuid():N}");
    private readonly string _contentRoot;

    public HelpAssetIntegrityValidatorTests()
    {
        _contentRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(Path.Combine(_contentRoot, "wwwroot", "help"));
    }

    [Fact]
    public async Task IsTrustedHelpFile_ReturnsFalseWhenManifestIsMissing()
    {
        var assetPath = Path.Combine(_contentRoot, "wwwroot", "help", "js", "help-search.js");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "console.log('help');");

        var validator = CreateValidator();

        Assert.False(validator.IsTrustedHelpFile(assetPath));
    }

    [Fact]
    public async Task IsTrustedHelpFile_ReturnsFalseWhenAssetIsNotListed()
    {
        var listedPath = Path.Combine(_contentRoot, "wwwroot", "help", "js", "help-search.js");
        var unlistedPath = Path.Combine(_contentRoot, "wwwroot", "help", "payload.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(listedPath)!);
        await File.WriteAllTextAsync(listedPath, "console.log('help');");
        await File.WriteAllTextAsync(unlistedPath, "<svg />");
        await WriteManifestAsync(("wwwroot/help/js/help-search.js", listedPath));

        var validator = CreateValidator();

        Assert.False(validator.IsTrustedHelpFile(unlistedPath));
    }

    [Fact]
    public async Task IsTrustedHelpFile_ReturnsFalseWhenHashDiffers()
    {
        var assetPath = Path.Combine(_contentRoot, "wwwroot", "help", "css", "help-page.css");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "body{}");
        await WriteManifestLineAsync("wwwroot/help/css/help-page.css|000000");

        var validator = CreateValidator();

        Assert.False(validator.IsTrustedHelpFile(assetPath));
    }

    [Fact]
    public async Task IsTrustedHelpFile_RehashesAfterSuccessfulValidation()
    {
        var assetPath = Path.Combine(_contentRoot, "wwwroot", "help", "css", "help-page.css");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "body{}");
        await WriteManifestAsync(("wwwroot/help/css/help-page.css", assetPath));

        var validator = CreateValidator();

        Assert.True(validator.IsTrustedHelpFile(assetPath));

        await File.WriteAllTextAsync(assetPath, "body{color:red}");

        Assert.False(validator.IsTrustedHelpFile(assetPath));
    }

    [Fact]
    public async Task IsTrustedHelpFile_TrustsDocsHelpPathFromBuildManifest()
    {
        var markdownPath = Path.Combine(_root, "Docs", "help", "budgetplanung", "index.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        await File.WriteAllTextAsync(markdownPath, "# Budgetplanung");
        await WriteManifestAsync(("../Docs/help/budgetplanung/index.md", markdownPath));

        var validator = CreateValidator();

        Assert.True(validator.IsTrustedHelpFile(markdownPath));
    }

    [Fact]
    public void BuildManifest_CoversAndHashesAllDeliveredHelpAssets()
    {
        var repoRoot = GetRepoRoot();
        var webProjectRoot = Path.Combine(repoRoot, "FinanceManager.Web");
        var buildOutputRoot = GetWebBuildOutputRoot(webProjectRoot);
        var buildOutputHelpRoot = Path.Combine(buildOutputRoot, "wwwroot", "help");
        var requiredSearchIndexes = HelpLanguages.Supported
            .Select(language => Path.Combine(buildOutputHelpRoot, language, "search-index.json"))
            .ToArray();
        foreach (var searchIndex in requiredSearchIndexes)
        {
            Assert.True(File.Exists(searchIndex), $"Required help search index is missing: {searchIndex}");
        }

        var manifestPath = Path.Combine(buildOutputHelpRoot, "help-assets.sha256");
        var manifest = File.ReadAllLines(manifestPath)
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => NormalizeManifestPath(parts[0]), parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var expectedFiles = Directory.EnumerateFiles(buildOutputHelpRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => IsManifestedStaticHelpAsset(path) && !path.EndsWith("help-assets.sha256", StringComparison.OrdinalIgnoreCase))
            .Concat(Directory.EnumerateFiles(Path.Combine(repoRoot, "Docs", "help"), "*.md", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expectedKeys = expectedFiles
            .Select(path => ToManifestKey(webProjectRoot, buildOutputRoot, path))
            .ToArray();

        Assert.Empty(expectedKeys.Except(manifest.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(manifest.Keys.Except(expectedKeys, StringComparer.OrdinalIgnoreCase));

        foreach (var file in expectedFiles)
        {
            var key = ToManifestKey(webProjectRoot, buildOutputRoot, file);
            Assert.Equal(ComputeSha256(file), manifest[key], ignoreCase: true);
        }

        foreach (var searchIndex in requiredSearchIndexes)
        {
            var key = ToManifestKey(webProjectRoot, buildOutputRoot, searchIndex);
            Assert.True(manifest.ContainsKey(key), $"Required help search index is missing from manifest: {key}");
            Assert.Equal(ComputeSha256(searchIndex), manifest[key], ignoreCase: true);
        }
    }

    [Fact]
    public void HelpBuildContract_UsesSharedLanguagesAndOutputManifestPaths()
    {
        var repoRoot = GetRepoRoot();
        var webProjectRoot = Path.Combine(repoRoot, "FinanceManager.Web");
        var props = XDocument.Load(Path.Combine(webProjectRoot, "Services", "Help", "HelpLanguages.props"));
        var supportedLanguages = props.Descendants("HelpSupportedLanguages").Single().Value;
        var defaultLanguage = props.Descendants("HelpDefaultLanguage").Single().Value;

        Assert.Equal(
            supportedLanguages.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            HelpLanguages.Supported);
        Assert.Equal(defaultLanguage, HelpLanguages.DefaultLanguage);

        var project = XDocument.Load(Path.Combine(webProjectRoot, "FinanceManager.Web.csproj"));
        var generateIndexes = project.Descendants("Target").Single(target => (string?)target.Attribute("Name") == "GenerateHelpSearchIndexes");
        var generateManifest = project.Descendants("Target").Single(target => (string?)target.Attribute("Name") == "GenerateHelpAssetManifest");
        var includePublishAssets = project.Descendants("Target").Single(target => (string?)target.Attribute("Name") == "IncludeGeneratedHelpAssetsInPublish");
        var indexCommand = generateIndexes.Descendants("Exec").Single().Attribute("Command")!.Value;

        Assert.Contains("ResolveProjectStaticWebAssets", (string?)generateIndexes.Attribute("BeforeTargets"), StringComparison.Ordinal);
        Assert.Contains("ComputeFilesToPublish", (string?)generateIndexes.Attribute("BeforeTargets"), StringComparison.Ordinal);
        Assert.Contains("$(TargetDir)wwwroot\\help", indexCommand, StringComparison.Ordinal);
        Assert.Contains("$(MSBuildProjectDirectory)\\wwwroot\\help", indexCommand, StringComparison.Ordinal);
        Assert.Equal("Build", (string?)generateManifest.Attribute("AfterTargets"));
        Assert.Empty(generateManifest.Descendants("WriteLinesToFile"));
        Assert.Equal("GenerateHelpSearchIndexes", (string?)includePublishAssets.Attribute("DependsOnTargets"));
        Assert.Equal("ComputeFilesToPublish", (string?)includePublishAssets.Attribute("BeforeTargets"));
        Assert.Contains("_GeneratedHelpPublishAsset.RecursiveDir", includePublishAssets.ToString(), StringComparison.Ordinal);
        Assert.Contains("help-assets.sha256", includePublishAssets.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void HelpSearchIndexGenerator_ReturnsFailureWhenDocsSourceIsMissing()
    {
        using var error = new StringWriter();
        var exitCode = HelpSearchIndexGeneratorRunner.Run(
            [Path.Combine(_root, "missing-docs"), Path.Combine(_root, "out"), string.Join(';', HelpLanguages.Supported)],
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("source directory does not exist", error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root, "out")));
    }

    [Fact]
    public void HelpSearchIndexGenerator_ReturnsFailureAndRemovesEmptyLanguageIndex()
    {
        var docsPath = Path.Combine(_root, "Docs", "help");
        var outputRoot = Path.Combine(_root, "out");
        Directory.CreateDirectory(Path.Combine(docsPath, "budgetplanung"));

        using var error = new StringWriter();
        var exitCode = HelpSearchIndexGeneratorRunner.Run([docsPath, outputRoot, "de"], error);

        Assert.Equal(1, exitCode);
        Assert.Contains("No help search documents generated for language: de", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(outputRoot, "de", "search-index.json")));
    }

    private HelpAssetIntegrityValidator CreateValidator()
    {
        return new HelpAssetIntegrityValidator(
            new TestWebHostEnvironment(_contentRoot),
            NullLogger<HelpAssetIntegrityValidator>.Instance);
    }

    private async Task WriteManifestAsync(params (string RelativePath, string FullPath)[] entries)
    {
        var lines = entries.Select(entry => $"{entry.RelativePath}|{ComputeSha256(entry.FullPath)}");
        await WriteManifestLineAsync(lines.ToArray());
    }

    private async Task WriteManifestLineAsync(params string[] lines)
    {
        var manifestPath = Path.Combine(_contentRoot, "wwwroot", "help", "help-assets.sha256");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllLinesAsync(manifestPath, lines);
    }

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static bool IsManifestedStaticHelpAsset(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".html", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeManifestPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string ToManifestKey(string webProjectRoot, string buildOutputRoot, string path)
    {
        if (Path.GetRelativePath(buildOutputRoot, path) is { } outputRelativePath
            && !outputRelativePath.Equals("..", StringComparison.Ordinal)
            && !outputRelativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !outputRelativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(outputRelativePath))
        {
            return NormalizeManifestPath(outputRelativePath);
        }

        return NormalizeManifestPath(Path.GetRelativePath(webProjectRoot, path));
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 4; i++)
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Unable to resolve repository root.");
        }

        return dir.FullName;
    }

    private static string GetWebBuildOutputRoot(string webProjectRoot)
    {
        var candidates = new[]
        {
            Path.Combine(webProjectRoot, "bin", "FromFinanceManagerTests", "Debug", "net10.0"),
            Path.Combine(webProjectRoot, "bin", "Debug", "net10.0")
        };

        return candidates.First(candidate => File.Exists(Path.Combine(candidate, "wwwroot", "help", "help-assets.sha256")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = Path.Combine(contentRootPath, "wwwroot");
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
        }

        public string ApplicationName { get; set; } = "FinanceManager.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Development";

        public IFileProvider WebRootFileProvider { get; set; }

        public string WebRootPath { get; set; }
    }
}
