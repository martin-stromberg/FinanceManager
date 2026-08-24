using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Builds deterministic help search index payloads from Docs/help markdown sources.
/// </summary>
public static partial class HelpSearchIndexBuilder
{
    private const int SearchExcerptMaxLength = 240;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Builds the search index for one supported language.
    /// </summary>
    /// <param name="docsPath">The absolute Docs/help source path.</param>
    /// <param name="language">The normalized help language.</param>
    /// <param name="includeMarkdown">Optional predicate used by runtime callers to enforce integrity checks.</param>
    /// <returns>The deterministic search index.</returns>
    public static HelpSearchIndexDto Build(string docsPath, string language, Func<string, bool>? includeMarkdown = null)
    {
        if (!HelpLanguages.TryNormalize(language, out var normalizedLanguage))
        {
            throw new ArgumentException($"Unsupported help language: {language}", nameof(language));
        }

        if (!Directory.Exists(docsPath))
        {
            return new HelpSearchIndexDto([]);
        }

        var documents = new List<HelpSearchDocumentDto>();
        foreach (var topic in HelpContentCatalog.Topics.OrderBy(topic => topic.Title, StringComparer.OrdinalIgnoreCase))
        {
            var primaryFile = HelpDocumentPathResolver.FindMarkdownFile(docsPath, normalizedLanguage, topic.Id);
            if (primaryFile is null || includeMarkdown?.Invoke(primaryFile) == false)
            {
                continue;
            }

            var markdown = File.ReadAllText(primaryFile, Encoding.UTF8);
            var title = topic.Title;
            var excerpt = string.IsNullOrWhiteSpace(topic.Description) ? ExtractExcerpt(markdown) : topic.Description;
            documents.Add(new HelpSearchDocumentDto(
                topic.Id,
                title,
                excerpt,
                BuildKeywords(topic, markdown)));
        }

        return new HelpSearchIndexDto(documents);
    }

    /// <summary>
    /// Builds and writes a UTF-8 JSON search index file.
    /// </summary>
    /// <param name="docsPath">The absolute Docs/help source path.</param>
    /// <param name="language">The normalized help language.</param>
    /// <param name="outputPath">The absolute output path.</param>
    /// <returns>The number of generated documents.</returns>
    public static int WriteToFile(string docsPath, string language, string outputPath)
    {
        var index = Build(docsPath, language);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(index, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return index.Documents.Count;
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

    private static IReadOnlyList<string> BuildKeywords(HelpTopic topic, string markdown)
    {
        var markdownTitle = ExtractTitle(markdown, topic.Id);

        return topic.Id
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(topic.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(topic.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(markdownTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(topic.Documents.SelectMany(document => document.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
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

    [GeneratedRegex(@"^---\s*[\r\n][\s\S]*?[\r\n]---\s*[\r\n]?", RegexOptions.Compiled)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"[`*_>#\[\]\(\)]", RegexOptions.Compiled)]
    private static partial Regex MarkdownSyntaxRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
