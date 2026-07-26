using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace FinanceManager.Tests.Integration;

public sealed partial class HelpSecurityMiddlewareTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly SemaphoreSlim HelpAssetMutationLock = new(1, 1);
    private readonly TestWebApplicationFactory _factory;

    public HelpSecurityMiddlewareTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/help/view/konten-und-buchungen")]
    [InlineData("/help/js/help-search.js")]
    [InlineData("/api/help/search-index/de.json")]
    [InlineData("/api/help/markdown/de/konten-und-buchungen")]
    [InlineData("/api/help/de/f001.html")]
    public async Task HelpRoutes_IncludeRestrictiveContentSecurityPolicy(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
        var csp = string.Join("; ", values);
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/help/view/budgetplanung")]
    [InlineData("/help/view/budgetplanung/beschreibung")]
    public async Task HelpUi_RendersWithoutInlineScriptsUnderRestrictiveCsp(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));

        var csp = string.Join("; ", values);
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("<script type=\"importmap\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_framework/blazor.web.js", html, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(InlineScriptRegex().Matches(html).Where(match => !match.Value.Contains(" src=", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task HelpView_RendersRealRelativeLinksAsInternalRoutes()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/help/view/budgetplanung", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("href=\"/help/view/budgetplanung/beschreibung\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/help/view/budgetplanung/api\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"beschreibung.md\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownHelpFileExtension_IsBlockedBeforeStaticFiles()
    {
        var payloadPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "FinanceManager.Web",
            "wwwroot",
            "help",
            "payload.svg"));

        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        await File.WriteAllTextAsync(payloadPath, "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", TestContext.Current.CancellationToken);

        try
        {
            using var client = _factory.CreateClient();

            using var response = await client.GetAsync("/help/payload.svg", TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
            Assert.Contains(values, value => value.Contains("default-src 'self'", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    [Fact]
    public async Task StaticHelpAssetRequest_IsBlockedWhenManifestIsMissing()
    {
        var manifestPath = GetWebHelpPath("help-assets.sha256");
        var backupPath = $"{manifestPath}.{Guid.NewGuid():N}.bak";

        await HelpAssetMutationLock.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            File.Move(manifestPath, backupPath);

            using var factory = new TestWebApplicationFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/help/css/help-page.css", TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
            Assert.Contains(values, value => value.Contains("default-src 'self'", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Move(backupPath, manifestPath, overwrite: true);
            }

            HelpAssetMutationLock.Release();
        }
    }

    [Theory]
    [InlineData("css/help-page.css", "/help/css/help-page.css", "body{outline:999px solid red}")]
    [InlineData("js/help-search.js", "/help/js/help-search.js", "console.log('manipulated');")]
    [InlineData("de/search-index.json", "/api/help/search-index/de.json", """{ "documents": [{ "id": "budgetplanung", "title": "Manipuliert", "excerpt": "Text", "keywords": [] }] }""")]
    public async Task HelpAssetHttpRequest_IsBlockedWhenManifestedFileIsManipulated(string relativeAssetPath, string requestPath, string manipulatedContent)
    {
        var assetPath = GetWebHelpPath(relativeAssetPath);
        var manifestPath = GetWebHelpPath("help-assets.sha256");
        string? originalContent = null;
        string? originalManifest = null;
        var assetExisted = false;

        await HelpAssetMutationLock.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            assetExisted = File.Exists(assetPath);
            originalContent = assetExisted
                ? await File.ReadAllTextAsync(assetPath, TestContext.Current.CancellationToken)
                : null;
            originalManifest = await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken);

            if (originalContent is null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
                originalContent = """{ "documents": [{ "id": "budgetplanung", "title": "Budgetplanung", "excerpt": "Text", "keywords": ["budget"] }] }""";
                await File.WriteAllTextAsync(assetPath, originalContent, TestContext.Current.CancellationToken);
                await File.AppendAllTextAsync(
                    manifestPath,
                    $"{Environment.NewLine}wwwroot/help/{relativeAssetPath.Replace('\\', '/')}|{ComputeSha256(assetPath)}",
                    TestContext.Current.CancellationToken);
            }

            await File.WriteAllTextAsync(assetPath, manipulatedContent, TestContext.Current.CancellationToken);

            using var factory = new TestWebApplicationFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(requestPath, TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
            Assert.Contains(values, value => value.Contains("default-src 'self'", StringComparison.Ordinal));
        }
        finally
        {
            if (originalManifest is not null)
            {
                await File.WriteAllTextAsync(manifestPath, originalManifest, TestContext.Current.CancellationToken);
            }

            if (assetExisted && originalContent is not null)
            {
                await File.WriteAllTextAsync(assetPath, originalContent, TestContext.Current.CancellationToken);
            }
            else if (File.Exists(assetPath))
            {
                File.Delete(assetPath);
            }

            HelpAssetMutationLock.Release();
        }
    }

    [GeneratedRegex("<script\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex InlineScriptRegex();

    private static string GetWebHelpPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(
            new[]
            {
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "FinanceManager.Web",
                "wwwroot",
                "help"
            }.Concat(segments).ToArray()));
    }

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
