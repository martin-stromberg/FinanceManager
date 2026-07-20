using System.Text.Json;
using System.Text.RegularExpressions;
using FinanceManager.Web.Services.Help;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Controller for serving help documentation pages and content.
/// Provides static HTML files and markdown content for help documentation.
/// </summary>
[ApiController]
[Route("api/help")]
public partial class HelpController : ControllerBase
{
    private const int SearchExcerptMaxLength = 240;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HelpController> _logger;
    private readonly IHelpContentRenderer _renderer;
    private readonly IHelpAssetIntegrityValidator _assetIntegrityValidator;

    /// <summary>
    /// Initializes a new instance of HelpController.
    /// </summary>
    public HelpController(
        IWebHostEnvironment env,
        ILogger<HelpController> logger,
        IHelpContentRenderer renderer,
        IHelpAssetIntegrityValidator assetIntegrityValidator)
    {
        _env = env;
        _logger = logger;
        _renderer = renderer;
        _assetIntegrityValidator = assetIntegrityValidator;
    }

    /// <summary>
    /// Gets a help documentation page by language and feature ID (legacy HTML endpoint).
    /// </summary>
    [HttpGet("{language}/{featureId}.html")]
    [Produces("text/html")]
    public async Task<IActionResult> GetHelpPage(string language, string featureId)
    {
        try
        {
            if (!TryNormalizeLanguage(language, out var normalizedLanguage)
                || !TryNormalizeFeatureId(featureId, out var normalizedFeatureId))
            {
                return BadRequest("Invalid help request");
            }

            var filePath = Path.Combine(_env.WebRootPath, "help", normalizedLanguage, $"{normalizedFeatureId}.html");

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Help page not found: {FilePath}", filePath);
                return NotFound("Help page not found");
            }

            if (!_assetIntegrityValidator.IsTrustedHelpFile(filePath))
            {
                _logger.LogWarning("Blocked untrusted help page: {FilePath}", filePath);
                return NotFound("Help page not found");
            }

            var content = await System.IO.File.ReadAllTextAsync(filePath);
            return Content(_renderer.SanitizeHtml(content), "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving help page: {Language}/{FeatureId}", language, featureId);
            return StatusCode(500, "Error retrieving help page");
        }
    }

    /// <summary>
    /// Gets the markdown content for a feature by language and feature ID.
    /// Used by the Blazor help page view component.
    /// </summary>
    [HttpGet("markdown/{language}/{**helpPath}")]
    [Produces("text/html")]
    public async Task<IActionResult> GetMarkdown(string language, string helpPath)
    {
        try
        {
            if (!TryNormalizeLanguage(language, out var normalizedLanguage)
                || !HelpDocumentPathResolver.TryNormalizeHelpPath(helpPath, out var normalizedHelpPath))
            {
                return BadRequest("Invalid help request");
            }

            var docsPath = HelpDocumentPathResolver.GetHelpSourcePath(_env);

            _logger.LogInformation("Looking for markdown in: {DocsPath}", docsPath);
            _logger.LogInformation("Searching for help path: {HelpPath}, Language: {Language}", normalizedHelpPath, normalizedLanguage);

            if (!Directory.Exists(docsPath))
            {
                _logger.LogError("Docs directory not found: {DocsPath}", docsPath);
                return StatusCode(500, "Docs directory not found");
            }

            var selectedFile = HelpDocumentPathResolver.FindMarkdownFile(docsPath, normalizedLanguage, normalizedHelpPath);
            if (selectedFile is null)
            {
                _logger.LogWarning("No markdown files found for: {HelpPath}", normalizedHelpPath);
                return NotFound("Documentation not found");
            }

            _logger.LogInformation("Selected file: {FileName}", Path.GetFileName(selectedFile));

            if (!System.IO.File.Exists(selectedFile))
            {
                _logger.LogError("Selected file does not exist: {FilePath}", selectedFile);
                return StatusCode(500, "Selected documentation not found");
            }

            if (!_assetIntegrityValidator.IsTrustedHelpFile(selectedFile))
            {
                _logger.LogWarning("Blocked untrusted markdown help file: {FilePath}", selectedFile);
                return NotFound("Documentation not found");
            }

            var content = await System.IO.File.ReadAllTextAsync(selectedFile, System.Text.Encoding.UTF8);
            var relativeDocumentPath = Path.GetRelativePath(docsPath, selectedFile).Replace('\\', '/');
            var html = _renderer.RenderMarkdownToHtml(content, relativeDocumentPath);

            _logger.LogInformation("Successfully loaded markdown content ({Size} bytes)", content.Length);

            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving markdown: {Language}/{HelpPath}", language, helpPath);
            return StatusCode(500, "Error retrieving markdown");
        }
    }

    /// <summary>
    /// Gets the search index for help pages by language.
    /// </summary>
    [HttpGet("search-index/{language}.json")]
    [Produces("application/json")]
    public async Task<IActionResult> GetSearchIndex(string language)
    {
        try
        {
            var languageForLog = (language ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

            if (!TryNormalizeLanguage(language, out var normalizedLanguage))
            {
                return BadRequest("Invalid language parameter");
            }

            var filePath = Path.Combine(_env.WebRootPath, "help", normalizedLanguage, "search-index.json");

            if (!System.IO.File.Exists(filePath))
            {
                var generatedIndex = GenerateSearchIndex(normalizedLanguage);
                return Ok(generatedIndex);
            }

            if (!_assetIntegrityValidator.IsTrustedHelpFile(filePath))
            {
                _logger.LogWarning("Blocked untrusted search index: {FilePath}", filePath);
                return NotFound("Search index not found");
            }

            var content = await System.IO.File.ReadAllTextAsync(filePath);
            var searchIndex = ParseSearchIndex(content);
            return Ok(searchIndex);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid search index for language: {Language}", languageForLog);
            return BadRequest("Invalid search index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving search index for language: {Language}", languageForLog);
            return StatusCode(500, "Error retrieving search index");
        }
    }

    private static HelpSearchIndexDto ParseSearchIndex(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("documents", out var documentsElement)
            || documentsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Help search index must contain a documents array.");
        }

        var documents = new List<HelpSearchDocumentDto>();
        foreach (var item in documentsElement.EnumerateArray())
        {
            if (TryReadSearchDocument(item, out var searchDocument))
            {
                documents.Add(searchDocument);
            }
        }

        return new HelpSearchIndexDto(documents);
    }

    private HelpSearchIndexDto GenerateSearchIndex(string language)
    {
        var docsPath = HelpDocumentPathResolver.GetHelpSourcePath(_env);
        if (!Directory.Exists(docsPath))
        {
            _logger.LogWarning("Cannot generate search index because Docs/help does not exist: {DocsPath}", docsPath);
            return new HelpSearchIndexDto([]);
        }

        var documents = new List<HelpSearchDocumentDto>();
        foreach (var directory in Directory.EnumerateDirectories(docsPath).OrderBy(Path.GetFileName))
        {
            var featureId = Path.GetFileName(directory).ToLowerInvariant();
            if (!TryNormalizeFeatureId(featureId, out var normalizedFeatureId))
            {
                continue;
            }

            var indexFile = HelpDocumentPathResolver.FindMarkdownFile(docsPath, language, normalizedFeatureId);
            if (indexFile is null || !_assetIntegrityValidator.IsTrustedHelpFile(indexFile))
            {
                continue;
            }

            var markdown = System.IO.File.ReadAllText(indexFile);
            var title = ExtractTitle(markdown, normalizedFeatureId);
            var excerpt = ExtractExcerpt(markdown);
            documents.Add(new HelpSearchDocumentDto(
                normalizedFeatureId,
                title,
                excerpt,
                BuildKeywords(normalizedFeatureId, title)));
        }

        return new HelpSearchIndexDto(documents);
    }

    private static string ExtractTitle(string markdown, string featureId)
    {
        var content = RemoveMarkdownFrontmatter(markdown);
        foreach (var line in ReadMarkdownLines(content))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return NormalizeSearchText(trimmed[2..], 200);
            }
        }

        return NormalizeSearchText(featureId.Replace('-', ' '), 200);
    }

    private static string ExtractExcerpt(string markdown)
    {
        var content = RemoveMarkdownFrontmatter(markdown);
        foreach (var line in ReadMarkdownLines(content))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('|'))
            {
                continue;
            }

            return NormalizeSearchText(trimmed, SearchExcerptMaxLength);
        }

        return "Dokumentation";
    }

    private static IReadOnlyList<string> BuildKeywords(string featureId, string title)
    {
        return featureId
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(keyword => NormalizeSearchText(keyword, 80).ToLowerInvariant())
            .Where(keyword => keyword.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static string NormalizeSearchText(string value, int maxLength)
    {
        var text = MarkdownSyntaxRegex().Replace(value, string.Empty).Trim();
        text = WhitespaceRegex().Replace(text, " ");
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd();
    }

    private static string RemoveMarkdownFrontmatter(string markdown)
    {
        return FrontmatterRegex().Replace(markdown, string.Empty);
    }

    private static IEnumerable<string> ReadMarkdownLines(string markdown)
    {
        using var reader = new StringReader(markdown);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static bool TryReadSearchDocument(JsonElement item, out HelpSearchDocumentDto document)
    {
        document = default!;

        if (!TryGetSafeString(item, "id", 64, out var id)
            || !TryNormalizeFeatureId(id, out var normalizedId)
            || !TryGetSafeString(item, "title", 200, out var title)
            || !TryGetSafeString(item, "excerpt", 1000, out var excerpt))
        {
            return false;
        }

        var keywords = new List<string>();
        if (item.TryGetProperty("keywords", out var keywordsElement))
        {
            if (keywordsElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var keywordElement in keywordsElement.EnumerateArray().Take(20))
            {
                if (keywordElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var keyword = keywordElement.GetString();
                if (!IsSafeText(keyword, 80))
                {
                    return false;
                }

                keywords.Add(keyword!.Trim());
            }
        }

        document = new HelpSearchDocumentDto(normalizedId, title.Trim(), excerpt.Trim(), keywords);
        return true;
    }

    private static bool TryGetSafeString(JsonElement item, string propertyName, int maxLength, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return IsSafeText(value, maxLength);
    }

    private static bool IsSafeText(string? value, int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= maxLength
            && !value.Any(char.IsControl)
            && !value.Contains('<', StringComparison.Ordinal)
            && !value.Contains('>', StringComparison.Ordinal);
    }

    private static bool TryNormalizeLanguage(string? language, out string normalizedLanguage)
    {
        normalizedLanguage = (language ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedLanguage is "de" or "en";
    }

    private static bool TryNormalizeFeatureId(string? featureId, out string normalizedFeatureId)
    {
        normalizedFeatureId = (featureId ?? string.Empty).Trim().ToLowerInvariant();
        return FeatureIdRegex().IsMatch(normalizedFeatureId);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}$", RegexOptions.Compiled)]
    private static partial Regex FeatureIdRegex();

    [GeneratedRegex(@"^---\s*[\r\n][\s\S]*?[\r\n]---\s*[\r\n]?", RegexOptions.Compiled)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"[`*_>#\[\]\(\)]", RegexOptions.Compiled)]
    private static partial Regex MarkdownSyntaxRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    private sealed record HelpSearchIndexDto(IReadOnlyList<HelpSearchDocumentDto> Documents);

    private sealed record HelpSearchDocumentDto(string Id, string Title, string Excerpt, IReadOnlyList<string> Keywords);
}
