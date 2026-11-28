using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Web;

/// <summary>
/// Static regression guard for the PathBase (reverse-proxy subfolder) hosting scenario: internal links,
/// assets and navigations must never be root-absolute because the browser resolves them against the
/// domain root and ignores both the dynamic <c>&lt;base href&gt;</c> rendered from <c>Request.PathBase</c>
/// in <c>App.razor</c> and the <c>UsePathBase()</c> prefix. Internal targets are always relative;
/// exceptions are server-side URLs built explicitly with <c>Request.PathBase</c> (e.g.
/// <c>AuthenticationRedirectMiddleware</c>) and route declarations (<c>@page</c>, endpoint mappings).
/// This error class already occurred twice (fixes <c>0890dd16</c>/<c>23898593</c>/<c>0ba3b200</c>,
/// regression in PR #591), so the scan runs as a regular unit test to catch the next reintroduction
/// before it ships.
/// </summary>
/// <remarks>
/// Accepted residual coverage — deliberately not scanned: unquoted attributes (<c>href=/x</c>;
/// extending <c>["']</c> to optional would false-positive on JS regex literals like
/// <c>const src = /re/</c>), <c>srcset</c> entries behind the first, CSS <c>@import</c> in string
/// form, URL-carrying <c>data-*</c> attributes beyond the suffix-matched ones
/// (<c>data-href</c>/<c>data-src</c>/…), <c>Worker</c>/<c>importScripts</c>/
/// <c>serviceWorker.register</c>/<c>WebSocket</c>/<c>pushState</c>/<c>replaceState</c>,
/// <c>manifest.json</c>, JS-interop dispatch (<c>InvokeVoidAsync("fetch", "/x")</c>) and
/// line-spanning literals/calls. Rationale: the guard covers the historically real
/// reintroduction vectors; widening it to these leftovers would buy marginal coverage at a
/// disproportionate false-positive rate.
/// </remarks>
public sealed class RazorRootAbsolutePathTests
{
    // href="/…", src='/…' plus the other markup attributes that carry a URL (<form action="/…">,
    // formaction="/…", srcset="/…", poster="/…") — the negative lookahead excludes protocol-relative
    // "//" URLs while still flagging a literal that ends at the line break (e.g. a wrapped href="/).
    private static readonly Regex RootAbsoluteLiteral = new(
        "(href|src|srcset|action|formaction|poster)\\s*=\\s*[\"']/(?!/)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Variant of RootAbsoluteLiteral for *.cs files: the attribute must sit inside a tag context
    // ("<" before the attribute on the same line, e.g. new MarkupString("<a href=\"/x\">") or
    // AddMarkupContent("<form action=\"/x\">")), so plain C# assignments to same-named variables
    // or properties (var action = "/submit";) stay unflagged. \\? additionally accepts the
    // escaped quotes of C# string literals (href=\"/x\").
    private static readonly Regex RootAbsoluteCsMarkup = new(
        "<[^<>]*\\b(href|src|srcset|action|formaction|poster)\\s*=\\s*\\\\?[\"']/(?!/)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // NavigateTo("/…") — the likeliest reintroduction vector in @code blocks and view models.
    // The optional "$" also catches interpolated/concatenated literals (NavigateTo($"/{x}"),
    // NavigateTo("/" + x)); all such calls still start with a root-absolute character.
    private static readonly Regex RootAbsoluteNavigateTo = new(
        "NavigateTo\\s*\\(\\s*\\$?[\"']/(?!/)",
        RegexOptions.Compiled);

    // fetch("/…") / import("/…") — in inline <script> blocks and wwwroot JavaScript.
    // Case-sensitive: JS API names are lowercase-canonical, PascalCase members must not match.
    private static readonly Regex RootAbsoluteScriptCall = new(
        "(fetch|import)\\s*\\(\\s*[\"'`]/(?!/)",
        RegexOptions.Compiled);

    // navigator.sendBeacon("/…"), new EventSource("/…") and XMLHttpRequest.open("GET", "/…") —
    // JavaScript APIs taking a URL argument that resolves against the domain root when written
    // root-absolute. The open() alternative anchors the literal at an argument start (first
    // argument, or directly after a comma) so concatenated relative paths like
    // xhr.open("GET", apiBase + "/accounts") stay unflagged; [^)]* still stops at the first
    // ")" so a match cannot span calls. Case-sensitive: .open must not catch PascalCase
    // members like File.Open("/x") or dialog.Open("/help").
    private static readonly Regex RootAbsoluteJsApi = new(
        "(sendBeacon|EventSource)\\s*\\(\\s*[\"'`]/(?!/)|\\.open\\s*\\((\\s*|[^)]*,\\s*)[\"'`]/(?!/)",
        RegexOptions.Compiled);

    // location.href = "/…", location.pathname = "/…", location.assign("/…"), location.replace("/…"),
    // (window.)location = "/…". The \b keeps identifiers like myLocation out of scope.
    // Case-sensitive: JS location is lowercase; PascalCase members like vm.Location must not match.
    private static readonly Regex RootAbsoluteLocationAssignment = new(
        "\\blocation\\s*(\\.\\s*(href|pathname)\\s*=|\\.\\s*(assign|replace)\\s*\\(|=)\\s*[\"']/(?!/)",
        RegexOptions.Compiled);

    // url(/…), url('/…'), url("/…") in inline styles and stylesheets - url(#…) fragment references
    // (SVG) and url(data:…) are not matched because they do not start with a slash.
    private static readonly Regex RootAbsoluteCssUrl = new(
        "url\\(\\s*[\"']?/(?!/)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string WebProjectName = "FinanceManager.Web";

    /// <summary>
    /// Verifies that no <c>*.razor</c>, <c>*.razor.js</c> or <c>*.razor.css</c> file under
    /// <c>FinanceManager.Web</c> contains a root-absolute URL-bearing attribute
    /// (<c>href</c>/<c>src</c>/<c>srcset</c>/<c>action</c>/<c>formaction</c>/<c>poster</c>),
    /// a <c>NavigateTo("/…")</c>/<c>fetch("/…")</c> call, a <c>location</c> assignment, a
    /// <c>url(/…)</c> style reference or a <c>sendBeacon</c>/<c>EventSource</c>/<c>open("/…")</c>
    /// call (single and double quotes; protocol-relative <c>//</c> URLs are excluded). The collated
    /// file kinds are enumerated explicitly because the <c>*.razor</c> glob does not match them.
    /// Violations are reported with file, line number and the offending line so the fix is
    /// immediately obvious.
    /// </summary>
    [Fact]
    public void RazorFiles_ShouldNotContainRootAbsoluteReferences()
    {
        var repoRoot = GetRepoRoot();
        var webProjectRoot = Path.Combine(repoRoot, WebProjectName);

        var violations = ScanFiles(
            repoRoot,
            EnumerateSourceFiles(webProjectRoot, "*.razor", "*.razor.js", "*.razor.css"),
            RootAbsoluteLiteral,
            RootAbsoluteNavigateTo,
            RootAbsoluteScriptCall,
            RootAbsoluteJsApi,
            RootAbsoluteLocationAssignment,
            RootAbsoluteCssUrl);

        AssertNoViolations(violations);
    }

    /// <summary>
    /// Verifies that no <c>*.cs</c> file under <c>FinanceManager.Web</c> (markup-emitting view models,
    /// helpers, code-behind) contains a root-absolute URL attribute literal
    /// (<c>href</c>/<c>src</c>/<c>srcset</c>/<c>action</c>/<c>formaction</c>/<c>poster</c>) inside a
    /// tag context (<c>MarkupString</c>, <c>AddMarkupContent</c>, appended HTML strings) or a
    /// <c>NavigateTo("/…")</c> call. Server-side URLs built from <c>Request.PathBase</c> are
    /// string-concatenated, not literals, and stay unflagged — as do plain assignments to
    /// same-named variables (<c>var action = "/submit";</c>) because the markup pattern
    /// requires a <c>&lt;</c> tag context on the same line.
    /// </summary>
    [Fact]
    public void CodeFiles_ShouldNotContainRootAbsoluteReferences()
    {
        var repoRoot = GetRepoRoot();
        var webProjectRoot = Path.Combine(repoRoot, WebProjectName);

        var violations = ScanFiles(
            repoRoot,
            EnumerateSourceFiles(webProjectRoot, "*.cs"),
            RootAbsoluteCsMarkup,
            RootAbsoluteNavigateTo);

        AssertNoViolations(violations);
    }

    /// <summary>
    /// Verifies that no script or stylesheet under <c>FinanceManager.Web/wwwroot</c> fetches, imports,
    /// assigns or references a root-absolute URL; relative URLs resolve against the dynamic
    /// <c>&lt;base href&gt;</c> and therefore stay inside the PathBase prefix.
    /// </summary>
    [Fact]
    public void WwwrootAssets_ShouldNotContainRootAbsoluteReferences()
    {
        var repoRoot = GetRepoRoot();
        var wwwroot = Path.Combine(repoRoot, WebProjectName, "wwwroot");

        var violations = ScanFiles(
            repoRoot,
            EnumerateSourceFiles(wwwroot, "*.js"),
            RootAbsoluteLiteral,
            RootAbsoluteScriptCall,
            RootAbsoluteJsApi,
            RootAbsoluteLocationAssignment);

        violations.AddRange(ScanFiles(
            repoRoot,
            EnumerateSourceFiles(wwwroot, "*.css"),
            RootAbsoluteCssUrl));

        AssertNoViolations(violations);
    }

    private static List<string> ScanFiles(string repoRoot, IEnumerable<string> files, params Regex[] patterns)
    {
        var violations = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (patterns.Any(pattern => pattern.IsMatch(lines[i])))
                {
                    var relativePath = Path.GetRelativePath(repoRoot, file);
                    violations.Add($"{relativePath}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Self-test for the guard patterns: each documented reintroduction vector must actually be
    /// matched by at least one pattern, and the sanctioned exceptions (protocol-relative URLs,
    /// <c>@page</c> routes, <c>Request.PathBase</c> concatenations, <c>url(#…)</c>/<c>data:</c>
    /// references and <c>location</c> reads) must produce no match. This keeps the regexes honest —
    /// a pattern that silently stops matching would turn the guard green without coverage.
    /// </summary>
    [Fact]
    public void GuardPatterns_ShouldMatchKnownViolationVectors()
    {
        var allPatterns = new[]
        {
            RootAbsoluteLiteral,
            RootAbsoluteCsMarkup,
            RootAbsoluteNavigateTo,
            RootAbsoluteScriptCall,
            RootAbsoluteJsApi,
            RootAbsoluteLocationAssignment,
            RootAbsoluteCssUrl
        };

        var violations = new[]
        {
            "<a href=\"/login\">",
            "<img src='/icon.png'>",
            "<form action=\"/import\">",
            "<button formaction=\"/save\">",
            "<img srcset=\"/a.png 1x\">",
            "<video poster=\"/poster.png\">",
            "Nav.NavigateTo(\"/login\")",
            "Nav.NavigateTo($\"/{path}\")",
            "Nav.NavigateTo(\"/\" + path)",
            "fetch('/api/ping')",
            "import(\"/js/mod.js\")",
            "navigator.sendBeacon(\"/api/track\")",
            "new EventSource(\"/api/stream\")",
            "xhr.open(\"GET\", \"/api/data\")",
            "window.open(\"/help\")",
            "location.href = \"/login\"",
            "location = '/login'",
            "window.location.assign(\"/login\")",
            "background: url('/img/bg.png')",
            "new MarkupString(\"<a href='/list'>\");",
            "builder.AddMarkupContent(0, \"<form action=\\\"/import\\\">\");"
        };

        foreach (var violation in violations)
        {
            Assert.True(
                allPatterns.Any(pattern => pattern.IsMatch(violation)),
                $"No guard pattern matched the violation vector: {violation}");
        }

        var allowed = new[]
        {
            "<a href=\"//cdn.example.com/x.js\">",
            "@page \"/list/accounts\"",
            "var loginUrl = Request.PathBase.Value + \"/login\";",
            "background: url(#icon-sprite)",
            "background: url(data:image/png;base64,AAAA)",
            "var path = window.location.pathname;",
            "if (e.Location == \"x\") { }",
            "fetch(\"api/auth/login\")",
            "Nav.NavigateTo($\"card/contacts/{id}\")",
            // Known false-positive classes pinned as allowed (review fixes):
            // PascalCase identifiers that must not hit the case-sensitive JS patterns …
            "File.Open(\"/var/x\")",
            "dialog.Open(\"/help\")",
            "vm.Location = \"/home\"",
            "myLocation = \"/home\"",
            "location.protocol = \"/x\"",
            // … and URL literals that are only path segments of a concatenation, not
            // root-absolute URL arguments.
            "xhr.open(\"GET\", apiBase + \"/accounts\")",
            "xhr.open(\"GET\", base + \"/\" + id)",
            "fetch(apiBase + \"/x\")",
            "navigator.sendBeacon(apiBase + \"/x\")"
        };

        foreach (var line in allowed)
        {
            Assert.False(
                allPatterns.Any(pattern => pattern.IsMatch(line)),
                $"A guard pattern matched an allowed construct: {line}");
        }

        // False-positive regression pins for the *.cs scan specifically: same-named variable
        // and property assignments without a tag context must stay unflagged there even
        // though the broad markup pattern (which also covers multi-line tags in *.razor)
        // would match them.
        var codeScanPatterns = new[]
        {
            RootAbsoluteCsMarkup,
            RootAbsoluteNavigateTo
        };

        var allowedInCodeFiles = new[]
        {
            "var action = \"/submit\";",
            "var href = \"/next\";",
            "model.Src = \"/img/a.png\";",
            "options.Poster = \"/img/p.png\";"
        };

        foreach (var line in allowedInCodeFiles)
        {
            Assert.False(
                codeScanPatterns.Any(pattern => pattern.IsMatch(line)),
                $"A *.cs guard pattern matched an allowed construct: {line}");
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root, params string[] patterns)
    {
        return patterns.SelectMany(pattern => Directory
                .EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"));
    }

    private static void AssertNoViolations(List<string> violations)
    {
        Assert.True(
            violations.Count == 0,
            "Root-absolute URLs are forbidden because they ignore the PathBase prefix "
            + "(use relative paths; the dynamic <base href> in App.razor resolves them. "
            + "Exceptions: server-side URLs built with Request.PathBase and @page route declarations):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 4; i++)
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Unable to resolve repository root.");
        }

        return dir.FullName;
    }
}
