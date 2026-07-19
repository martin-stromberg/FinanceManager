using System.Text.RegularExpressions;
using Xunit;

namespace FinanceManager.Tests.Integration;

public sealed partial class HelpSecurityMiddlewareTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HelpSecurityMiddlewareTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/help/view/konten-und-buchungen")]
    [InlineData("/help/js/help-search.js")]
    [InlineData("/api/help/search-index/de.json")]
    [InlineData("/api/help/markdown/de/konten-und-buchungen")]
    [InlineData("/api/help/de/f001.html")]
    public async Task HelpRoutes_IncludeRestrictiveContentSecurityPolicy(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
        var csp = string.Join("; ", values);
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/help/view/budgetplanung")]
    [InlineData("/help/view/budgetplanung/beschreibung")]
    public async Task HelpUi_RendersWithoutInlineScriptsUnderRestrictiveCsp(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));

        var csp = string.Join("; ", values);
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("<script type=\"importmap\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_framework/blazor.web.js", html, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(InlineScriptRegex().Matches(html).Where(match => !match.Value.Contains(" src=", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task HelpView_RendersRealRelativeLinksAsInternalRoutes()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/help/view/budgetplanung", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("href=\"/help/view/budgetplanung/beschreibung\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/help/view/budgetplanung/api\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"beschreibung.md\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownHelpFileExtension_IsBlockedBeforeStaticFiles()
    {
        var payloadPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "FinanceManager.Web",
            "wwwroot",
            "help",
            "payload.svg"));

        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        await File.WriteAllTextAsync(payloadPath, "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", TestContext.Current.CancellationToken);

        try
        {
            using var client = _factory.CreateClient();

            using var response = await client.GetAsync("/help/payload.svg", TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
            Assert.Contains(values, value => value.Contains("default-src 'self'", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    [GeneratedRegex("<script\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex InlineScriptRegex();
}
