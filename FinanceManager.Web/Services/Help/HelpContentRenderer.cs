using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Markdig;

namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Converts help markdown and legacy HTML through a single sanitizer boundary.
/// </summary>
public sealed partial class HelpContentRenderer : IHelpContentRenderer
{
    private const string InternalHelpLinkBaseUrl = "https://finance-manager.local/help/view/";
    private static readonly ISet<string> AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a", "blockquote", "br", "code", "div", "em", "h1", "h2", "h3", "h4", "h5", "h6",
        "hr", "li", "ol", "p", "pre", "strong", "table", "tbody", "td", "th", "thead", "tr", "ul"
    };

    private static readonly ISet<string> DropWithContentTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "form", "input", "button", "textarea", "select", "option"
    };

    private readonly MarkdownPipeline _markdownPipeline;

    /// <summary>
    /// Initializes a new instance of the help content renderer.
    /// </summary>
    public HelpContentRenderer()
    {
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .DisableHtml()
            .Build();

    }

    /// <inheritdoc />
    public string RemoveFrontmatter(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        return FrontmatterRegex().Replace(markdown, string.Empty);
    }

    /// <inheritdoc />
    public string RenderMarkdownToHtml(string markdown)
    {
        return RenderMarkdownToHtml(markdown, currentDocumentPath: null);
    }

    /// <inheritdoc />
    public string RenderMarkdownToHtml(string markdown, string? currentDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalizedMarkdown = RewriteInternalHelpLinks(RemoveFrontmatter(markdown), currentDocumentPath);
        var html = Markdown.ToHtml(normalizedMarkdown, _markdownPipeline);
        return SanitizeHtml(html);
    }

    /// <inheritdoc />
    public string SanitizeHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var sanitized = string.Concat(document.Body?.ChildNodes.Select(RenderSanitizedNode) ?? []);
        sanitized = RestoreInternalHelpLinks(sanitized);
        return sanitized;
    }

    private static string RenderSanitizedNode(INode node)
    {
        return node switch
        {
            IText text => System.Net.WebUtility.HtmlEncode(text.Data),
            IElement element => RenderSanitizedElement(element),
            _ => string.Empty
        };
    }

    private static string RenderSanitizedElement(IElement element)
    {
        var tagName = element.TagName.ToLowerInvariant();
        if (DropWithContentTags.Contains(tagName))
        {
            return string.Empty;
        }

        var children = string.Concat(element.ChildNodes.Select(RenderSanitizedNode));
        if (!AllowedTags.Contains(tagName))
        {
            return children;
        }

        if (tagName is "br" or "hr")
        {
            return $"<{tagName}>";
        }

        var attributes = tagName == "a" ? BuildAnchorAttributes(element) : string.Empty;
        return $"<{tagName}{attributes}>{children}</{tagName}>";
    }

    private static string BuildAnchorAttributes(IElement element)
    {
        var href = element.GetAttribute("href")?.Trim();
        if (string.IsNullOrWhiteSpace(href) || !IsAllowedHref(href))
        {
            return string.Empty;
        }

        var attributes = $" href=\"{EncodeAttribute(href)}\"";
        if (!IsExternalHref(href))
        {
            return attributes;
        }

        attributes += " target=\"_blank\"";
        var rel = BuildSafeRelValue(element.GetAttribute("rel") ?? string.Empty);
        attributes += $" rel=\"{EncodeAttribute(rel)}\"";
        return attributes;
    }

    private static string EncodeAttribute(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value).Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static string RewriteInternalHelpLinks(string markdown, string? currentDocumentPath)
    {
        return MarkdownLinkRegex().Replace(markdown, match =>
        {
            if (match.Groups["image"].Value == "!")
            {
                return match.Value;
            }

            var target = match.Groups["target"].Value;
            if (!TryBuildInternalHelpRoute(target, currentDocumentPath, out var route))
            {
                return match.Value;
            }

            return $"[{match.Groups["text"].Value}]({InternalHelpLinkBaseUrl}{route}{match.Groups["title"].Value})";
        });
    }

    private static string RestoreInternalHelpLinks(string html)
    {
        return html.Replace(
            $"href=\"{InternalHelpLinkBaseUrl}",
            "href=\"/help/view/",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSafeRelValue(string relValue)
    {
        var tokens = relValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !token.Equals("opener", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!tokens.Contains("noopener", StringComparer.OrdinalIgnoreCase))
        {
            tokens.Add("noopener");
        }

        if (!tokens.Contains("noreferrer", StringComparer.OrdinalIgnoreCase))
        {
            tokens.Add("noreferrer");
        }

        return string.Join(' ', tokens);
    }

    private static bool IsAllowedHref(string href)
    {
        if (InternalHrefRegex().IsMatch(href)
            || href.StartsWith(InternalHelpLinkBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(href, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsExternalHref(string href)
    {
        return !href.StartsWith(InternalHelpLinkBaseUrl, StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(href, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool TryBuildInternalHelpRoute(string target, string? currentDocumentPath, out string route)
    {
        route = string.Empty;
        var normalizedTarget = target.Trim().Trim('<', '>');
        if (normalizedTarget.Length == 0
            || normalizedTarget.StartsWith('#')
            || normalizedTarget.Contains(':', StringComparison.Ordinal)
            || Uri.TryCreate(normalizedTarget, UriKind.Absolute, out _))
        {
            return false;
        }

        var fragment = string.Empty;
        var fragmentIndex = normalizedTarget.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex >= 0)
        {
            var rawFragment = normalizedTarget[(fragmentIndex + 1)..];
            normalizedTarget = normalizedTarget[..fragmentIndex];
            if (SafeFragmentRegex().IsMatch(rawFragment))
            {
                fragment = $"#{rawFragment}";
            }
        }

        var queryIndex = normalizedTarget.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            normalizedTarget = normalizedTarget[..queryIndex];
        }

        normalizedTarget = normalizedTarget.Replace('\\', '/');
        if (!normalizedTarget.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var combinedSegments = new List<string>();
        if (!normalizedTarget.StartsWith('/'))
        {
            foreach (var segment in GetDirectorySegments(currentDocumentPath))
            {
                combinedSegments.Add(segment);
            }
        }

        foreach (var rawSegment in normalizedTarget.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = Uri.UnescapeDataString(rawSegment).Trim();
            if (segment is ".")
            {
                continue;
            }

            if (segment is "..")
            {
                if (combinedSegments.Count == 0)
                {
                    return false;
                }

                combinedSegments.RemoveAt(combinedSegments.Count - 1);
                continue;
            }

            combinedSegments.Add(segment);
        }

        if (combinedSegments.Count == 0)
        {
            return false;
        }

        var last = combinedSegments[^1];
        if (!last.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        combinedSegments[^1] = last[..^3];
        if (combinedSegments[^1].Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            combinedSegments.RemoveAt(combinedSegments.Count - 1);
        }

        if (combinedSegments.Count == 0)
        {
            combinedSegments.Add("index");
        }

        var normalizedSegments = new List<string>();
        foreach (var segment in combinedSegments)
        {
            var normalizedSegment = segment.ToLowerInvariant();
            if (!HelpRouteSegmentRegex().IsMatch(normalizedSegment))
            {
                return false;
            }

            normalizedSegments.Add(normalizedSegment);
        }

        route = string.Join('/', normalizedSegments) + fragment;
        return true;
    }

    private static IEnumerable<string> GetDirectorySegments(string? currentDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            yield break;
        }

        var normalized = currentDocumentPath.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < Math.Max(0, segments.Length - 1); i++)
        {
            yield return segments[i];
        }
    }

    [GeneratedRegex(@"^---\s*[\r\n][\s\S]*?[\r\n]---\s*[\r\n]?", RegexOptions.Compiled)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"(?<image>!?)\[(?<text>(?:[^\]\\]|\\.)+)\]\((?<target><[^>]+>|[^\s\)]+)(?<title>\s+(?:""[^""]*""|'[^']*'))?\)", RegexOptions.Compiled)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"^/help/view/[a-z][a-z0-9-]{0,63}(?:/[a-z][a-z0-9-]{0,63})*(?:#[A-Za-z0-9_-]{1,80})?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex InternalHrefRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9-]{0,63}$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex HelpRouteSegmentRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,80}$", RegexOptions.Compiled)]
    private static partial Regex SafeFragmentRegex();

}
