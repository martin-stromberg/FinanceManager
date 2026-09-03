using FinanceManager.Web.Services.Help;

namespace FinanceManager.Tests.Web.Help;

/// <summary>
/// Tests for <see cref="HelpContentRenderer"/>, which turns help markdown (and legacy raw HTML) into sanitized
/// HTML for display: stripping executable content (scripts, event handlers, <c>javascript:</c>/<c>data:</c>
/// URIs) while preserving legitimate formatting, and rewriting relative document links into the app's
/// <c>/help/view/...</c> routes. Several tests render the actual on-disk <c>Docs/help</c> markdown files
/// to catch link-rewriting regressions against real content, not just synthetic snippets.
/// </summary>
public sealed class HelpContentRendererTests
{
    private readonly HelpContentRenderer _renderer = new();

    /// <summary>
    /// Verifies that rendering markdown strips a <c>&lt;script&gt;</c> tag, an <c>onerror</c>-bearing image,
    /// a <c>javascript:</c> link, and the raw front-matter block, while the legitimate heading still renders.
    /// </summary>
    [Fact]
    public void RenderMarkdownToHtml_RemovesExecutableHtml()
    {
        var html = _renderer.RenderMarkdownToHtml("""
            ---
            title: Test
            ---
            # Heading

            <script>alert(1)</script>
            <img src=x onerror=alert(1)>
            [bad](javascript:alert(1))
            """);

        Assert.Contains("<h1", html);
        Assert.Contains("Heading", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("title: Test", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that safe formatting (headings, bold, inline code, tables) renders correctly, that external
    /// links get <c>rel="noopener noreferrer"</c> to prevent tab-nabbing, and that a relative link to another
    /// markdown file is rewritten to the app's <c>/help/view/...</c> route.
    /// </summary>
    [Fact]
    public void RenderMarkdownToHtml_KeepsAllowedFormattingAndSafeLinks()
    {
        var html = _renderer.RenderMarkdownToHtml("""
            ## Abschnitt

            **Fett** und `Code`

            | A | B |
            | - | - |
            | 1 | 2 |

            [Extern](https://example.test)
            [Intern](F001-konten.md)
            """);

        Assert.Contains("<h2", html);
        Assert.Contains("<strong", html);
        Assert.Contains("<code", html);
        Assert.Contains("<table", html);
        Assert.Contains("href=\"https://example.test\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
        Assert.Contains("href=\"/help/view/f001-konten\"", html);
    }

    /// <summary>
    /// Verifies against the real <c>Docs/help/index.md</c> that top-level topic links are rewritten to their
    /// <c>/help/view/...</c> routes and that no raw <c>.md</c> file reference leaks through unrewritten.
    /// </summary>
    [Fact]
    public void RenderMarkdownToHtml_RewritesRealDocsHelpTopLevelLinks()
    {
        var markdown = File.ReadAllText(GetDocsHelpPath("index.md"));

        var html = _renderer.RenderMarkdownToHtml(markdown, "index.md");

        Assert.Contains("href=\"/help/view/bestandsaufnahme\"", html);
        Assert.Contains("href=\"/help/view/budgetplanung\"", html);
        Assert.Contains("href=\"/help/view/kontoauszuege-und-import\"", html);
        Assert.DoesNotContain("href=\"budgetplanung/index.md\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies against the real <c>Docs/help/budgetplanung/index.md</c> that sibling-document links within a
    /// topic section resolve relative to the current document's directory rather than the help root, so
    /// the same relative link works correctly regardless of which topic it appears in.
    /// </summary>
    [Fact]
    public void RenderMarkdownToHtml_RewritesRealDocsHelpSectionLinksRelativeToCurrentDirectory()
    {
        var markdown = File.ReadAllText(GetDocsHelpPath("budgetplanung", "index.md"));

        var html = _renderer.RenderMarkdownToHtml(markdown, "budgetplanung/index.md");

        Assert.Contains("href=\"/help/view/budgetplanung/beschreibung\"", html);
        Assert.Contains("href=\"/help/view/budgetplanung/api\"", html);
        Assert.Contains("href=\"/help/view/budgetplanung/datenmodell\"", html);
        Assert.DoesNotContain("href=\"beschreibung.md\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies against the real <c>Docs/help/budgetplanung/beschreibung.md</c> that a "back to section"
    /// link (<c>index.md</c>) is rewritten to the topic's own route rather than left as a raw file reference.
    /// </summary>
    [Fact]
    public void RenderMarkdownToHtml_RewritesRealDocsHelpBackLinksToSectionIndex()
    {
        var markdown = File.ReadAllText(GetDocsHelpPath("budgetplanung", "beschreibung.md"));

        var html = _renderer.RenderMarkdownToHtml(markdown, "budgetplanung/beschreibung.md");

        Assert.Contains("href=\"/help/view/budgetplanung\"", html);
        Assert.DoesNotContain("href=\"index.md\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that unsafe content nested inside otherwise-safe structures (a <c>data:</c> URI link, a
    /// <c>javascript:</c> link inside a table cell) is stripped, while a script sample shown as fenced code
    /// is safely HTML-escaped rather than executed or removed — since fenced code blocks are meant to display
    /// their content literally.
    /// </summary>
    [Fact]
    public void RenderMarkdownToHtml_RemovesUnsafeNestedPayloadsAndDataUrls()
    {
        var html = _renderer.RenderMarkdownToHtml("""
            # Payloads

            [data](data:text/html,<script>alert(1)</script>)

            | A | B |
            | - | - |
            | 1 | [bad](javascript:alert(1)) |

            ```html
            <script>alert(1)</script>
            ```
            """);

        Assert.Contains("<table", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("data:text/html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that the legacy raw-HTML sanitization path (used for pre-markdown help pages) strips an
    /// inline <c>onclick</c> handler, a <c>javascript:</c> link, and a <c>&lt;script&gt;</c> tag while keeping
    /// the legitimate text content.
    /// </summary>
    [Fact]
    public void SanitizeHtml_RemovesLegacyScriptAndInlineHandlers()
    {
        var html = _renderer.SanitizeHtml("""
            <h1 onclick="alert(1)">Titel</h1>
            <a href="javascript:alert(1)">bad</a>
            <script>alert(1)</script>
            """);

        Assert.Contains("Titel", html);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that legacy HTML sanitization normalizes an external link's <c>rel</c> attribute to include
    /// <c>nofollow noopener noreferrer</c> even when the source HTML supplied a different, unsafe <c>rel</c>
    /// value (here "opener", which is exactly what <c>noopener</c> exists to prevent).
    /// </summary>
    [Fact]
    public void SanitizeHtml_ForcesSafeRelOnExternalLegacyLinks()
    {
        var html = _renderer.SanitizeHtml("""
            <a href="https://example.test" target="_blank" rel="opener nofollow">Extern</a>
            """);

        Assert.Contains("href=\"https://example.test\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("rel=\"nofollow noopener noreferrer\"", html);
        Assert.DoesNotContain("rel=\"opener", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that legacy HTML sanitization also catches event handlers and unsafe URIs nested several
    /// levels deep inside a table structure, not just at the top level, while preserving the surrounding safe
    /// markup (the table itself and a legitimate external link).
    /// </summary>
    [Fact]
    public void SanitizeHtml_RemovesNestedLegacyEventHandlersAndDataUrls()
    {
        var html = _renderer.SanitizeHtml("""
            <table>
              <tr onclick="alert(1)">
                <td><a href="data:text/html,<script>alert(1)</script>" onmouseover="alert(1)">bad</a></td>
              </tr>
            </table>
            <a href="https://example.test" onmouseover="alert(1)"><strong>extern</strong></a>
            """);

        Assert.Contains("<table", html);
        Assert.Contains("href=\"https://example.test\"", html);
        Assert.DoesNotContain("data:text/html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDocsHelpPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(
            new[]
            {
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Docs",
                "help"
            }.Concat(segments).ToArray()));
    }
}
