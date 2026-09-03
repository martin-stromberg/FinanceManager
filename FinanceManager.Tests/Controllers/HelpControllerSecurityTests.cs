using System.Reflection;
using System.Security.Cryptography;
using FinanceManager.Web.Controllers;
using FinanceManager.Web.Services.Help;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Security-focused tests for <see cref="HelpController"/>: markdown/legacy-HTML rendering is sanitized against
/// script injection and unsafe links, help content is only served when it passes the
/// <see cref="IHelpAssetIntegrityValidator"/> check against a manifest (guarding against tampered on-disk help
/// files), and the search-index endpoint drops malformed or unsafe entries instead of failing outright.
/// Uses a temporary on-disk content/web root per test to exercise the real file-reading code paths.
/// </summary>
public sealed class HelpControllerSecurityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fm-help-{Guid.NewGuid():N}");
    private readonly string _contentRoot;
    private readonly string _webRoot;

    /// <summary>
    /// Creates the temporary content/web root directory structure used by each test.
    /// </summary>
    public HelpControllerSecurityTests()
    {
        _contentRoot = Path.Combine(_root, "app");
        _webRoot = Path.Combine(_contentRoot, "wwwroot");
        Directory.CreateDirectory(_webRoot);
    }

    /// <summary>
    /// Verifies that rendering a help document strips an embedded <c>&lt;script&gt;</c> tag, a
    /// <c>javascript:</c> link, and the raw front-matter block, while still rendering the legitimate heading.
    /// </summary>
    [Fact]
    public async Task GetMarkdown_ReturnsSanitizedHtml()
    {
        var docsPath = Path.Combine(_root, "Docs", "help", "konten-und-buchungen");
        Directory.CreateDirectory(docsPath);
        await File.WriteAllTextAsync(Path.Combine(docsPath, "beschreibung.md"), """
            ---
            title: Test
            ---
            # Hilfe

            <script>alert(1)</script>
            [bad](javascript:alert(1))
            """, TestContext.Current.CancellationToken);

        var result = await CreateController().GetMarkdown("de", "konten-und-buchungen");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.Contains("<h1", content.Content);
        Assert.DoesNotContain("<script", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("title: Test", content.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a route segment naming a specific file within a topic folder (e.g.
    /// <c>budgetplanung/beschreibung</c>) resolves to that nested document rather than only the topic's
    /// default page.
    /// </summary>
    [Fact]
    public async Task GetMarkdown_ReturnsNestedDocumentForCatchAllHelpPath()
    {
        var docsPath = Path.Combine(_root, "Docs", "help", "budgetplanung");
        Directory.CreateDirectory(docsPath);
        await File.WriteAllTextAsync(Path.Combine(docsPath, "beschreibung.md"), "# Beschreibung", TestContext.Current.CancellationToken);

        var result = await CreateController().GetMarkdown("de", "budgetplanung/beschreibung");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Contains("Beschreibung", content.Content);
    }

    /// <summary>
    /// Verifies that once a markdown file's hash has been recorded via the manifest and served successfully,
    /// modifying the file on disk afterwards causes subsequent requests to be blocked with 404 by the real
    /// <see cref="HelpAssetIntegrityValidator"/> — protection against a compromised or corrupted help file
    /// being served silently.
    /// </summary>
    [Fact]
    public async Task GetMarkdown_WithRealValidatorBlocksManipulatedMarkdown()
    {
        var markdownPath = Path.Combine(_root, "Docs", "help", "budgetplanung", "beschreibung.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        await File.WriteAllTextAsync(markdownPath, "# Budgetplanung", TestContext.Current.CancellationToken);
        await WriteManifestAsync(("../Docs/help/budgetplanung/beschreibung.md", markdownPath));

        var controller = CreateControllerWithRealValidator();
        var initialResult = await controller.GetMarkdown("de", "budgetplanung");

        Assert.IsType<ContentResult>(initialResult);

        await File.WriteAllTextAsync(markdownPath, "# Manipuliert", TestContext.Current.CancellationToken);

        var manipulatedResult = await controller.GetMarkdown("de", "budgetplanung");

        Assert.IsType<NotFoundObjectResult>(manipulatedResult);
    }

    /// <summary>
    /// Verifies that the real integrity validator fails closed: with no manifest file present at all, a
    /// help document is not served even though the file itself exists and is unmodified.
    /// </summary>
    [Fact]
    public async Task GetMarkdown_WithRealValidatorBlocksWhenManifestIsMissing()
    {
        var markdownPath = Path.Combine(_root, "Docs", "help", "budgetplanung", "beschreibung.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        await File.WriteAllTextAsync(markdownPath, "# Budgetplanung", TestContext.Current.CancellationToken);

        var result = await CreateControllerWithRealValidator().GetMarkdown("de", "budgetplanung");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Verifies that, using the real validator and renderer together, a document combining tables, inline
    /// code, a relative internal link, and unsafe content (a <c>javascript:</c> link, an <c>onerror</c> image,
    /// and a fenced-code script sample) renders the safe structural elements and internal link correctly while
    /// the unsafe content is either stripped or safely escaped.
    /// </summary>
    [Fact]
    public async Task GetMarkdown_WithRealValidatorSanitizesNestedTablesCodeAndLinks()
    {
        var markdownPath = Path.Combine(_root, "Docs", "help", "budgetplanung", "beschreibung.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        await File.WriteAllTextAsync(markdownPath, """
            # Budgetplanung

            | Link | Code |
            | - | - |
            | [intern](beschreibung.md) | `value` |
            | [bad](javascript:alert(1)) | <img src=x onerror=alert(1)> |

            ```html
            <script>alert(1)</script>
            ```
            """, TestContext.Current.CancellationToken);
        await WriteManifestAsync(("../Docs/help/budgetplanung/beschreibung.md", markdownPath));

        var result = await CreateControllerWithRealValidator().GetMarkdown("de", "budgetplanung");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Contains("<table", content.Content);
        Assert.Contains("<code", content.Content);
        Assert.Contains("href=\"/help/view/budgetplanung/beschreibung\"", content.Content);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", content.Content);
        Assert.DoesNotContain("javascript:", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", content.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a document marked as technical-only (e.g. an internal <c>api.md</c>) is not exposed
    /// through the public help endpoint even though the file exists on disk, keeping implementation-detail
    /// docs out of user-facing help.
    /// </summary>
    [Fact]
    public async Task GetMarkdown_ReturnsNotFoundForTechnicalOnlyDocument()
    {
        var docsPath = Path.Combine(_root, "Docs", "help", "budgetplanung");
        Directory.CreateDirectory(docsPath);
        await File.WriteAllTextAsync(Path.Combine(docsPath, "beschreibung.md"), "# Budgetplanung", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(docsPath, "api.md"), "# API", TestContext.Current.CancellationToken);

        var result = await CreateController().GetMarkdown("de", "budgetplanung/api");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Verifies that requesting the search index for a language with no pre-built <c>search-index.json</c>
    /// returns 404 rather than an empty or error response, for both supported languages.
    /// </summary>
    /// <param name="language">The help language code to request the search index for.</param>
    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public async Task GetSearchIndex_ReturnsNotFoundWhenStaticIndexIsMissing(string language)
    {
        var result = await CreateController().GetSearchIndex(language);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>
    /// Verifies that the legacy (pre-markdown) HTML help-page path strips an inline <c>onclick</c> handler
    /// and a <c>&lt;script&gt;</c> tag while keeping the legitimate content, mirroring the sanitization applied
    /// to the newer markdown-based help pages.
    /// </summary>
    [Fact]
    public async Task GetHelpPage_SanitizesLegacyHtml()
    {
        var helpPath = Path.Combine(_webRoot, "help", "de");
        Directory.CreateDirectory(helpPath);
        await File.WriteAllTextAsync(Path.Combine(helpPath, "f001.html"), """
            <h1 onclick="alert(1)">Hilfe</h1>
            <script>alert(1)</script>
            """, TestContext.Current.CancellationToken);

        var result = await CreateController().GetHelpPage("de", "f001");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.Contains("Hilfe", content.Content);
        Assert.DoesNotContain("onclick", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", content.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that, like markdown documents, a legacy HTML help page modified after its hash was recorded in
    /// the manifest is blocked by the real integrity validator on subsequent requests.
    /// </summary>
    [Fact]
    public async Task GetHelpPage_WithRealValidatorBlocksManipulatedLegacyHtml()
    {
        var helpPagePath = Path.Combine(_webRoot, "help", "de", "f001.html");
        Directory.CreateDirectory(Path.GetDirectoryName(helpPagePath)!);
        await File.WriteAllTextAsync(helpPagePath, "<h1>Hilfe</h1>", TestContext.Current.CancellationToken);
        await WriteManifestAsync(("wwwroot/help/de/f001.html", helpPagePath));

        var controller = CreateControllerWithRealValidator();
        var initialResult = await controller.GetHelpPage("de", "f001");

        Assert.IsType<ContentResult>(initialResult);

        await File.WriteAllTextAsync(helpPagePath, "<h1>Manipuliert</h1>", TestContext.Current.CancellationToken);

        var manipulatedResult = await controller.GetHelpPage("de", "f001");

        Assert.IsType<NotFoundObjectResult>(manipulatedResult);
    }

    /// <summary>
    /// Verifies that search-index entries with an unsafe id (a <c>javascript:</c> URI) or an unsafe title
    /// (containing an <c>&lt;img&gt;</c> tag) are silently dropped from the response rather than being exposed
    /// to the client, leaving only the well-formed, safe entry.
    /// </summary>
    [Fact]
    public async Task GetSearchIndex_DropsInvalidDocuments()
    {
        var helpPath = Path.Combine(_webRoot, "help", "de");
        Directory.CreateDirectory(helpPath);
        await File.WriteAllTextAsync(Path.Combine(helpPath, "search-index.json"), """
            {
              "documents": [
                { "id": "konten-und-buchungen", "title": "Konten", "excerpt": "Sicher", "keywords": ["konto"] },
                { "id": "javascript:alert(1)", "title": "Bad", "excerpt": "Bad", "keywords": [] },
                { "id": "f002", "title": "<img src=x>", "excerpt": "Text", "keywords": ["x"] }
              ]
            }
            """, TestContext.Current.CancellationToken);

        var result = await CreateController().GetSearchIndex("de");
        var ok = Assert.IsType<OkObjectResult>(result);
        var documents = GetDocuments(ok.Value);

        var document = Assert.Single(documents);
        Assert.Equal("konten-und-buchungen", GetProperty<string>(document, "Id"));
        Assert.Equal("Konten", GetProperty<string>(document, "Title"));
    }

    /// <summary>
    /// Verifies that search-index entries missing a required field (id, title, or excerpt) or with a field of
    /// the wrong JSON type (keywords as a string instead of an array) are dropped rather than causing a parse
    /// failure of the whole index or being passed through with null/invalid data.
    /// </summary>
    [Fact]
    public async Task GetSearchIndex_DropsDocumentsWithMissingRequiredFields()
    {
        var helpPath = Path.Combine(_webRoot, "help", "de");
        Directory.CreateDirectory(helpPath);
        await File.WriteAllTextAsync(Path.Combine(helpPath, "search-index.json"), """
            {
              "documents": [
                { "id": "budgetplanung", "title": "Budgetplanung", "excerpt": "Sicher", "keywords": ["budget"] },
                { "title": "Ohne ID", "excerpt": "Text", "keywords": [] },
                { "id": "kontakte", "excerpt": "Ohne Titel", "keywords": [] },
                { "id": "anhaenge", "title": "Ohne Auszug", "keywords": [] },
                { "id": "berichte", "title": "Berichte", "excerpt": "Text", "keywords": "bericht" }
              ]
            }
            """, TestContext.Current.CancellationToken);

        var result = await CreateController().GetSearchIndex("de");
        var ok = Assert.IsType<OkObjectResult>(result);
        var documents = GetDocuments(ok.Value);

        var document = Assert.Single(documents);
        Assert.Equal("budgetplanung", GetProperty<string>(document, "Id"));
    }

    /// <summary>
    /// Verifies that the search index JSON is also covered by the real integrity validator: once served
    /// successfully, an on-disk modification to <c>search-index.json</c> causes subsequent requests to be
    /// blocked with 404.
    /// </summary>
    [Fact]
    public async Task GetSearchIndex_WithRealValidatorBlocksManipulatedJson()
    {
        var indexPath = Path.Combine(_webRoot, "help", "de", "search-index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        await File.WriteAllTextAsync(indexPath, """
            {
              "documents": [
                { "id": "budgetplanung", "title": "Budgetplanung", "excerpt": "Sicher", "keywords": ["budget"] }
              ]
            }
            """, TestContext.Current.CancellationToken);
        await WriteManifestAsync(("wwwroot/help/de/search-index.json", indexPath));

        var controller = CreateControllerWithRealValidator();
        var initialResult = await controller.GetSearchIndex("de");

        Assert.IsType<OkObjectResult>(initialResult);

        await File.WriteAllTextAsync(indexPath, """
            {
              "documents": [
                { "id": "budgetplanung", "title": "Manipuliert", "excerpt": "Sicher", "keywords": ["budget"] }
              ]
            }
            """, TestContext.Current.CancellationToken);

        var manipulatedResult = await controller.GetSearchIndex("de");

        Assert.IsType<NotFoundObjectResult>(manipulatedResult);
    }

    /// <summary>
    /// Verifies that a search index JSON file missing the expected <c>documents</c> array (e.g. using a
    /// different top-level property) is rejected with 400 rather than throwing or returning an empty result.
    /// </summary>
    [Fact]
    public async Task GetSearchIndex_RejectsIndexWithoutDocumentsArray()
    {
        var helpPath = Path.Combine(_webRoot, "help", "de");
        Directory.CreateDirectory(helpPath);
        await File.WriteAllTextAsync(Path.Combine(helpPath, "search-index.json"), """
            { "items": [] }
            """, TestContext.Current.CancellationToken);

        var result = await CreateController().GetSearchIndex("de");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that the client-side help search script avoids <c>innerHTML</c> and inline event handlers
    /// (e.g. <c>onclick</c>) in favor of <c>textContent</c> and <c>addEventListener</c> — a static safeguard
    /// against DOM-based XSS when rendering search results built from user-supplied query text.
    /// </summary>
    [Fact]
    public void HelpSearchScript_DoesNotUseHtmlInterpolationOrInlineHandlers()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "FinanceManager.Web",
            "wwwroot",
            "help",
            "js",
            "help-search.js"));

        var script = File.ReadAllText(scriptPath);

        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("textContent", script, StringComparison.Ordinal);
        Assert.Contains("addEventListener", script, StringComparison.Ordinal);
    }

    private HelpController CreateController()
    {
        return new HelpController(
            new TestWebHostEnvironment(_contentRoot, _webRoot),
            NullLogger<HelpController>.Instance,
            new HelpContentRenderer(),
            new TrustAllHelpAssetIntegrityValidator());
    }

    private HelpController CreateControllerWithRealValidator()
    {
        var environment = new TestWebHostEnvironment(_contentRoot, _webRoot);
        return new HelpController(
            environment,
            NullLogger<HelpController>.Instance,
            new HelpContentRenderer(),
            new HelpAssetIntegrityValidator(environment, NullLogger<HelpAssetIntegrityValidator>.Instance));
    }

    private async Task WriteManifestAsync(params (string RelativePath, string FullPath)[] entries)
    {
        var lines = entries.Select(entry => $"{entry.RelativePath}|{ComputeSha256(entry.FullPath)}");
        var manifestPath = Path.Combine(_webRoot, "help", "help-assets.sha256");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllLinesAsync(manifestPath, lines);
    }

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static IReadOnlyList<object> GetDocuments(object? value)
    {
        Assert.NotNull(value);
        var documents = value.GetType().GetProperty("Documents", BindingFlags.Instance | BindingFlags.Public)!.GetValue(value);
        return ((System.Collections.IEnumerable)documents!).Cast<object>().ToList();
    }

    private static T GetProperty<T>(object value, string propertyName)
    {
        return (T)value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.GetValue(value)!;
    }

    /// <summary>
    /// Removes the temporary content/web root directory tree created for the test.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TrustAllHelpAssetIntegrityValidator : IHelpAssetIntegrityValidator
    {
        public bool IsTrustedHelpFile(string fullPath) => File.Exists(fullPath);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath, string webRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = webRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootFileProvider = new PhysicalFileProvider(webRootPath);
        }

        public string ApplicationName { get; set; } = "FinanceManager.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Development";

        public IFileProvider WebRootFileProvider { get; set; }

        public string WebRootPath { get; set; }
    }
}
