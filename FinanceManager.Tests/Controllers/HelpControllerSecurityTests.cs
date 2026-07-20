using System.Reflection;
using System.Security.Cryptography;
using FinanceManager.Web.Controllers;
using FinanceManager.Web.Services.Help;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Controllers;

public sealed class HelpControllerSecurityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fm-help-{Guid.NewGuid():N}");
    private readonly string _contentRoot;
    private readonly string _webRoot;

    public HelpControllerSecurityTests()
    {
        _contentRoot = Path.Combine(_root, "app");
        _webRoot = Path.Combine(_contentRoot, "wwwroot");
        Directory.CreateDirectory(_webRoot);
    }

    [Fact]
    public async Task GetMarkdown_ReturnsSanitizedHtml()
    {
        var docsPath = Path.Combine(_root, "Docs", "help", "konten-und-buchungen");
        Directory.CreateDirectory(docsPath);
        await File.WriteAllTextAsync(Path.Combine(docsPath, "index.md"), """
            ---
            title: Test
            ---
            # Hilfe

            <script>alert(1)</script>
            [bad](javascript:alert(1))
            """);

        var result = await CreateController().GetMarkdown("de", "konten-und-buchungen");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.Contains("<h1", content.Content);
        Assert.DoesNotContain("<script", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("title: Test", content.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMarkdown_ReturnsNestedDocumentForCatchAllHelpPath()
    {
        var docsPath = Path.Combine(_root, "Docs", "help", "budgetplanung");
        Directory.CreateDirectory(docsPath);
        await File.WriteAllTextAsync(Path.Combine(docsPath, "beschreibung.md"), "# Beschreibung");

        var result = await CreateController().GetMarkdown("de", "budgetplanung/beschreibung");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Contains("Beschreibung", content.Content);
    }

    [Fact]
    public async Task GetMarkdown_WithRealValidatorBlocksManipulatedMarkdown()
    {
        var markdownPath = Path.Combine(_root, "Docs", "help", "budgetplanung", "index.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        await File.WriteAllTextAsync(markdownPath, "# Budgetplanung");
        await WriteManifestAsync(("../Docs/help/budgetplanung/index.md", markdownPath));

        var controller = CreateControllerWithRealValidator();
        var initialResult = await controller.GetMarkdown("de", "budgetplanung");

        Assert.IsType<ContentResult>(initialResult);

        await File.WriteAllTextAsync(markdownPath, "# Manipuliert");

        var manipulatedResult = await controller.GetMarkdown("de", "budgetplanung");

        Assert.IsType<NotFoundObjectResult>(manipulatedResult);
    }

    [Fact]
    public async Task GetMarkdown_WithRealValidatorBlocksWhenManifestIsMissing()
    {
        var markdownPath = Path.Combine(_root, "Docs", "help", "budgetplanung", "index.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        await File.WriteAllTextAsync(markdownPath, "# Budgetplanung");

        var result = await CreateControllerWithRealValidator().GetMarkdown("de", "budgetplanung");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetMarkdown_WithRealValidatorSanitizesNestedTablesCodeAndLinks()
    {
        var markdownPath = Path.Combine(_root, "Docs", "help", "budgetplanung", "index.md");
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
            """);
        await WriteManifestAsync(("../Docs/help/budgetplanung/index.md", markdownPath));

        var result = await CreateControllerWithRealValidator().GetMarkdown("de", "budgetplanung");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Contains("<table", content.Content);
        Assert.Contains("<code", content.Content);
        Assert.Contains("href=\"/help/view/budgetplanung/beschreibung\"", content.Content);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", content.Content);
        Assert.DoesNotContain("javascript:", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", content.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSearchIndex_GeneratesDocumentsFromDocsHelpWhenStaticIndexIsMissing()
    {
        var docsPath = Path.Combine(_root, "Docs", "help", "budgetplanung");
        Directory.CreateDirectory(docsPath);
        await File.WriteAllTextAsync(Path.Combine(docsPath, "index.md"), """
            # Budgetplanung

            Budgets planen und Auswertungen vorbereiten.
            """);

        var result = await CreateController().GetSearchIndex("de");
        var ok = Assert.IsType<OkObjectResult>(result);
        var documents = GetDocuments(ok.Value);

        var document = Assert.Single(documents);
        Assert.Equal("budgetplanung", GetProperty<string>(document, "Id"));
        Assert.Equal("Budgetplanung", GetProperty<string>(document, "Title"));
        Assert.Equal("Budgets planen und Auswertungen vorbereiten.", GetProperty<string>(document, "Excerpt"));
    }

    [Fact]
    public async Task GetHelpPage_SanitizesLegacyHtml()
    {
        var helpPath = Path.Combine(_webRoot, "help", "de");
        Directory.CreateDirectory(helpPath);
        await File.WriteAllTextAsync(Path.Combine(helpPath, "f001.html"), """
            <h1 onclick="alert(1)">Hilfe</h1>
            <script>alert(1)</script>
            """);

        var result = await CreateController().GetHelpPage("de", "f001");
        var content = Assert.IsType<ContentResult>(result);

        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.Contains("Hilfe", content.Content);
        Assert.DoesNotContain("onclick", content.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", content.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHelpPage_WithRealValidatorBlocksManipulatedLegacyHtml()
    {
        var helpPagePath = Path.Combine(_webRoot, "help", "de", "f001.html");
        Directory.CreateDirectory(Path.GetDirectoryName(helpPagePath)!);
        await File.WriteAllTextAsync(helpPagePath, "<h1>Hilfe</h1>");
        await WriteManifestAsync(("wwwroot/help/de/f001.html", helpPagePath));

        var controller = CreateControllerWithRealValidator();
        var initialResult = await controller.GetHelpPage("de", "f001");

        Assert.IsType<ContentResult>(initialResult);

        await File.WriteAllTextAsync(helpPagePath, "<h1>Manipuliert</h1>");

        var manipulatedResult = await controller.GetHelpPage("de", "f001");

        Assert.IsType<NotFoundObjectResult>(manipulatedResult);
    }

    [Fact]
    public async Task GetSearchIndex_DropsInvalidDocuments()
    {
        var helpPath = Path.Combine(_webRoot, "help", "de");
        Directory.CreateDirectory(helpPath);
        await File.WriteAllTextAsync(Path.Combine(helpPath, "search-index.json"), """
            {
              "documents": [
                { "id": "f001", "title": "Konten", "excerpt": "Sicher", "keywords": ["konto"] },
                { "id": "javascript:alert(1)", "title": "Bad", "excerpt": "Bad", "keywords": [] },
                { "id": "f002", "title": "<img src=x>", "excerpt": "Text", "keywords": ["x"] }
              ]
            }
            """);

        var result = await CreateController().GetSearchIndex("de");
        var ok = Assert.IsType<OkObjectResult>(result);
        var documents = GetDocuments(ok.Value);

        var document = Assert.Single(documents);
        Assert.Equal("f001", GetProperty<string>(document, "Id"));
        Assert.Equal("Konten", GetProperty<string>(document, "Title"));
    }

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
            """);

        var result = await CreateController().GetSearchIndex("de");
        var ok = Assert.IsType<OkObjectResult>(result);
        var documents = GetDocuments(ok.Value);

        var document = Assert.Single(documents);
        Assert.Equal("budgetplanung", GetProperty<string>(document, "Id"));
    }

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
            """);
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
            """);

        var manipulatedResult = await controller.GetSearchIndex("de");

        Assert.IsType<NotFoundObjectResult>(manipulatedResult);
    }

    [Fact]
    public async Task GetSearchIndex_RejectsIndexWithoutDocumentsArray()
    {
        var helpPath = Path.Combine(_webRoot, "help", "de");
        Directory.CreateDirectory(helpPath);
        await File.WriteAllTextAsync(Path.Combine(helpPath, "search-index.json"), """
            { "items": [] }
            """);

        var result = await CreateController().GetSearchIndex("de");

        Assert.IsType<BadRequestObjectResult>(result);
    }

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
