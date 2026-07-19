using FinanceManager.Web.Services.Help;

namespace FinanceManager.Tests.Web.Help;

public sealed class HelpContentRendererTests
{
    private readonly HelpContentRenderer _renderer = new();

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

    [Fact]
    public void RenderMarkdownToHtml_RewritesRealDocsHelpBackLinksToSectionIndex()
    {
        var markdown = File.ReadAllText(GetDocsHelpPath("budgetplanung", "beschreibung.md"));

        var html = _renderer.RenderMarkdownToHtml(markdown, "budgetplanung/beschreibung.md");

        Assert.Contains("href=\"/help/view/budgetplanung\"", html);
        Assert.DoesNotContain("href=\"index.md\"", html, StringComparison.OrdinalIgnoreCase);
    }

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
