using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// End-to-end test for the security hardening around the static, pre-rendered help system: the
/// restrictive Content-Security-Policy applied to every help route, that the help UI never relies on
/// inline scripts under that policy, and that the file-integrity manifest (<c>help-assets.sha256</c>)
/// blocks serving a help asset whenever it is missing or has been tampered with on disk.
/// </summary>
public sealed partial class HelpSecurityMiddlewareTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly SemaphoreSlim HelpAssetMutationLock = new(1, 1);
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HelpSecurityMiddlewareTests"/> class.
    /// </summary>
    /// <param name="factory">Shared web application factory providing the in-memory test server.</param>
    public HelpSecurityMiddlewareTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifies that every kind of help route - page, static asset, and API endpoint alike - responds with
    /// a restrictive Content-Security-Policy header that forbids inline scripts, so a single route added
    /// without the middleware applied cannot silently reopen an XSS surface.
    /// </summary>
    /// <param name="path">The help route path under test.</param>
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

    /// <summary>
    /// Verifies that the rendered help UI markup itself never relies on inline scripts or the Blazor Web
    /// import map/script - both of which the restrictive CSP would block - so the help pages keep working
    /// under that policy instead of silently failing to execute their scripts in a real browser.
    /// </summary>
    /// <param name="path">The help route path under test.</param>
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

    /// <summary>
    /// Verifies that catalog document links in a rendered help page point at internal <c>/help/view/...</c>
    /// routes rather than raw <c>.md</c> filenames or the internal-only <c>/api</c> namespace, so readers
    /// clicking a related-document link land on a working page instead of a 404 or an unrouted API path.
    /// </summary>
    [Fact]
    public async Task HelpView_RendersCatalogDocumentLinksAsInternalRoutes()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/help/view/konten-und-buchungen", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("href=\"/help/view/konten-und-buchungen\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/help/view/konten-und-buchungen/vorlaeufige-buchungen\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"beschreibung.md\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/help/view/konten-und-buchungen/api\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a file with an extension not on the help static-file allowlist (e.g. an uploaded
    /// <c>.svg</c>) is rejected before ASP.NET Core's static file middleware would otherwise serve it, so
    /// an attacker cannot smuggle an unexpected file type onto a help route and have it served verbatim.
    /// </summary>
    [Fact]
    public async Task UnknownHelpFileExtension_IsBlockedBeforeStaticFiles()
    {
        using var factory = new TestWebApplicationFactory();
        var payloadPath = GetWebHelpPath(factory, "payload.svg");

        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        await File.WriteAllTextAsync(payloadPath, "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", TestContext.Current.CancellationToken);

        try
        {
            using var client = factory.CreateClient();

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

    /// <summary>
    /// Verifies that if the file-integrity manifest (<c>help-assets.sha256</c>) itself is missing from
    /// disk, requests for help static assets fail closed with a 404 rather than falling back to serving
    /// the asset unverified - the manifest's absence must never be treated as "nothing to check".
    /// </summary>
    [Fact]
    public async Task StaticHelpAssetRequest_IsBlockedWhenManifestIsMissing()
    {
        using var factory = new TestWebApplicationFactory();
        var manifestPath = GetWebHelpPath(factory, "help-assets.sha256");
        var backupPath = $"{manifestPath}.{Guid.NewGuid():N}.bak";

        await HelpAssetMutationLock.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            File.Move(manifestPath, backupPath);

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

    /// <summary>
    /// Verifies that when an on-disk help asset's content no longer matches the hash recorded for it in
    /// the integrity manifest - simulating tampering after deployment - the request is blocked with a 404
    /// across CSS, JS, and both localized search-index asset kinds, rather than serving the manipulated
    /// content to the client.
    /// </summary>
    /// <param name="relativeAssetPath">Path of the asset relative to the help web root, used to locate and mutate it on disk.</param>
    /// <param name="requestPath">The HTTP request path that should be blocked once the asset is manipulated.</param>
    /// <param name="manipulatedContent">The tampered content written to the asset file to make its hash mismatch the manifest.</param>
    [Theory]
    [InlineData("css/help-page.css", "/help/css/help-page.css", "body{outline:999px solid red}")]
    [InlineData("js/help-search.js", "/help/js/help-search.js", "console.log('manipulated');")]
    [InlineData("de/search-index.json", "/api/help/search-index/de.json", """{ "documents": [{ "id": "budgetplanung", "title": "Manipuliert", "excerpt": "Text", "keywords": [] }] }""")]
    [InlineData("en/search-index.json", "/api/help/search-index/en.json", """{ "documents": [{ "id": "budgetplanung", "title": "Manipulated", "excerpt": "Text", "keywords": [] }] }""")]
    public async Task HelpAssetHttpRequest_IsBlockedWhenManifestedFileIsManipulated(string relativeAssetPath, string requestPath, string manipulatedContent)
    {
        using var factory = new TestWebApplicationFactory();
        var assetPath = GetWebHelpPath(factory, relativeAssetPath);
        var manifestPath = GetWebHelpPath(factory, "help-assets.sha256");
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

    /// <summary>
    /// Verifies that requesting the search index for a language whose pre-built <c>search-index.json</c>
    /// file is missing from disk returns a 404 (for both supported languages), rather than throwing an
    /// unhandled exception or serving stale/empty content.
    /// </summary>
    /// <param name="language">The help UI language code whose search index file is removed for the test.</param>
    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public async Task HelpSearchIndexHttpRequest_IsNotFoundWhenStaticIndexIsMissing(string language)
    {
        using var factory = new TestWebApplicationFactory();
        var assetPath = GetWebHelpPath(factory, language, "search-index.json");
        var backupPath = $"{assetPath}.{Guid.NewGuid():N}.bak";

        await HelpAssetMutationLock.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            File.Move(assetPath, backupPath);

            using var client = factory.CreateClient();

            using var response = await client.GetAsync($"/api/help/search-index/{language}.json", TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
            Assert.Contains(values, value => value.Contains("default-src 'self'", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(backupPath))
            {
                File.Move(backupPath, assetPath, overwrite: true);
            }

            HelpAssetMutationLock.Release();
        }
    }

    [GeneratedRegex("<script\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex InlineScriptRegex();

    private static string GetWebHelpPath(TestWebApplicationFactory factory, params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(
            new[]
            {
                factory.HelpWebRootPath,
                "help"
            }.Concat(segments).ToArray()));
    }

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
