namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Renders help content to sanitized HTML that may be passed to MarkupString.
/// </summary>
public interface IHelpContentRenderer
{
    /// <summary>
    /// Removes YAML frontmatter from a markdown document.
    /// </summary>
    /// <param name="markdown">The markdown document.</param>
    /// <returns>The document without a leading frontmatter block.</returns>
    string RemoveFrontmatter(string markdown);

    /// <summary>
    /// Converts markdown to sanitized HTML.
    /// </summary>
    /// <param name="markdown">The markdown document.</param>
    /// <returns>Sanitized HTML suitable for trusted rendering.</returns>
    string RenderMarkdownToHtml(string markdown);

    /// <summary>
    /// Converts markdown to sanitized HTML and resolves relative markdown links from the current help document.
    /// </summary>
    /// <param name="markdown">The markdown document.</param>
    /// <param name="currentDocumentPath">The document path relative to Docs/help, for example <c>budgetplanung/index.md</c>.</param>
    /// <returns>Sanitized HTML suitable for trusted rendering.</returns>
    string RenderMarkdownToHtml(string markdown, string? currentDocumentPath);

    /// <summary>
    /// Sanitizes legacy HTML help content.
    /// </summary>
    /// <param name="html">The legacy HTML document.</param>
    /// <returns>Sanitized HTML suitable for trusted rendering.</returns>
    string SanitizeHtml(string html);
}
