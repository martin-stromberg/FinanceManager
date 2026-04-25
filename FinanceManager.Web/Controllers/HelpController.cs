using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Controller for serving help documentation pages and content.
/// Provides static HTML files and markdown content for help documentation.
/// </summary>
[ApiController]
[Route("api/help")]
public class HelpController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HelpController> _logger;

    /// <summary>
    /// Initializes a new instance of HelpController.
    /// </summary>
    public HelpController(IWebHostEnvironment env, ILogger<HelpController> logger)
    {
        _env = env;
        _logger = logger;
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
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(featureId))
            {
                return BadRequest("Language and featureId are required");
            }

            language = language.ToLower();
            featureId = featureId.ToLower();

            if (language.Contains("..") || featureId.Contains("..") || 
                language.Contains("/") || featureId.Contains("/"))
            {
                return BadRequest("Invalid characters in parameters");
            }

            var filePath = Path.Combine(_env.WebRootPath, "help", language, $"{featureId}.html");

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Help page not found: {FilePath}", filePath);
                return NotFound($"Help page not found: {language}/{featureId}.html");
            }

            var content = await System.IO.File.ReadAllTextAsync(filePath);
            return Content(content, "text/html; charset=utf-8");
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
    [HttpGet("markdown/{language}/{featureId}")]
    [Produces("text/plain")]
    public async Task<IActionResult> GetMarkdown(string language, string featureId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(featureId))
            {
                return BadRequest("Language and featureId are required");
            }

            language = language.ToLower();
            featureId = featureId.ToLower();

            if (language.Contains("..") || featureId.Contains("..") || 
                language.Contains("/") || featureId.Contains("/"))
            {
                return BadRequest("Invalid characters in parameters");
            }

            // Build docs path - docs are in solution root, not in project root
            var docsPath = Path.Combine(_env.ContentRootPath, "..", "docs", "business", "features");
            docsPath = Path.GetFullPath(docsPath);

            _logger.LogInformation("Looking for markdown in: {DocsPath}", docsPath);
            _logger.LogInformation("Searching for feature: {FeatureId}, Language: {Language}", featureId, language);

            if (!Directory.Exists(docsPath))
            {
                _logger.LogError("Docs directory not found: {DocsPath}", docsPath);
                return StatusCode(500, $"Docs directory not found: {docsPath}");
            }

            // List all .md files for debugging
            var allFiles = Directory.GetFiles(docsPath, "*.md");
            _logger.LogInformation("Found {Count} markdown files in docs directory", allFiles.Length);

            // Look for files matching the feature ID
            var filePattern = $"{featureId}*.md";
            var matchingFiles = Directory.GetFiles(docsPath, filePattern);

            _logger.LogInformation("Found {Count} files matching pattern '{Pattern}'", matchingFiles.Length, filePattern);

            if (matchingFiles.Length == 0)
            {
                // Try case-insensitive search
                var filesInDir = Directory.GetFiles(docsPath, "*.md");
                matchingFiles = filesInDir
                    .Where(f => Path.GetFileName(f).ToLower().StartsWith(featureId.ToLower()))
                    .ToArray();

                _logger.LogInformation("Found {Count} files with case-insensitive search", matchingFiles.Length);
            }

            if (matchingFiles.Length == 0)
            {
                _logger.LogWarning("No markdown files found for: {FeatureId} (pattern: {Pattern})", featureId, filePattern);
                return NotFound($"Documentation not found for feature: {featureId}");
            }

            // Select the correct language file
            string? selectedFile = null;

            if (language == "en")
            {
                // First try English-specific file
                selectedFile = matchingFiles.FirstOrDefault(f => f.EndsWith($".en.md", StringComparison.OrdinalIgnoreCase));
                // Fallback to German
                if (selectedFile == null)
                {
                    selectedFile = matchingFiles.FirstOrDefault(f => 
                        !f.EndsWith(".en.md", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith("-domain.md", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith("-infrastructure.md", StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                // German - exclude English and domain/infrastructure files
                selectedFile = matchingFiles.FirstOrDefault(f =>
                    !f.EndsWith(".en.md", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith("-domain.md", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith("-infrastructure.md", StringComparison.OrdinalIgnoreCase));
            }

            // Fallback to first file found
            if (selectedFile == null)
            {
                selectedFile = matchingFiles[0];
            }

            _logger.LogInformation("Selected file: {FileName}", Path.GetFileName(selectedFile));

            if (!System.IO.File.Exists(selectedFile))
            {
                _logger.LogError("Selected file does not exist: {FilePath}", selectedFile);
                return StatusCode(500, $"Selected file not found: {selectedFile}");
            }

            var content = await System.IO.File.ReadAllTextAsync(selectedFile, System.Text.Encoding.UTF8);

            // Remove frontmatter (YAML) - everything between --- markers
            content = System.Text.RegularExpressions.Regex.Replace(content, @"^---[\s\S]*?---\n?", "", System.Text.RegularExpressions.RegexOptions.Multiline);

            _logger.LogInformation("Successfully loaded markdown content ({Size} bytes)", content.Length);

            return Content(content, "text/plain; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving markdown: {Language}/{FeatureId}", language, featureId);
            return StatusCode(500, $"Error retrieving markdown: {ex.Message}");
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
            language = language.ToLower();
            if (language.Contains("..") || language.Contains("/"))
            {
                return BadRequest("Invalid language parameter");
            }

            var filePath = Path.Combine(_env.WebRootPath, "help", language, "search-index.json");

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Search index not found: {FilePath}", filePath);
                return NotFound($"Search index not found for language: {language}");
            }

            var content = await System.IO.File.ReadAllTextAsync(filePath);
            return Content(content, "application/json; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving search index for language: {Language}", language);
            return StatusCode(500, "Error retrieving search index");
        }
    }
}
