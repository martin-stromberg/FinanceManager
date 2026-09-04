using FinanceManager.Web.Services.Help;

namespace FinanceManager.Tests.Web.Help;

/// <summary>
/// Tests for <see cref="HelpContentCatalog"/>, the curated list of user-facing help topics and documents:
/// that the catalog only exposes reviewed, non-technical content; that route-to-document resolution correctly
/// maps a topic route to its primary document and rejects technical-only document names; and that the search
/// index built from the catalog stays in sync with it. Also cross-checks the real on-disk
/// <c>Docs/help</c> tree against the catalog so a document referenced by the catalog always exists and never
/// leaks implementation details (class/endpoint names, config keys) into user-facing help text.
/// </summary>
public sealed class HelpContentCatalogTests
{
    private static readonly string[] TechnicalContentMarkers =
    [
        "Controller",
        "Endpunkt",
        "API-seitig",
        "appsettings",
        "Jwt:",
        "JWT",
        "ViewModel",
        "Technische Umsetzung",
        "RepositoryOwner",
        "ExecutablePath"
    ];

    /// <summary>
    /// Verifies the exact expected topic count and that the catalog includes a known reviewed topic while
    /// excluding an internal/unreviewed one (<c>bestandsaufnahme</c>), guarding against an unreviewed
    /// documentation folder being accidentally published to end users.
    /// </summary>
    [Fact]
    public void Topics_ContainsReviewedUserHelpTopicsOnly()
    {
        Assert.Equal(12, HelpContentCatalog.Topics.Count);
        Assert.Contains(HelpContentCatalog.Topics, topic => topic.Id == "konten-und-buchungen");
        Assert.DoesNotContain(HelpContentCatalog.Topics, topic => topic.Id == "bestandsaufnahme");
    }

    /// <summary>
    /// Verifies that document names reserved for technical/implementation documentation (API reference, data
    /// model, business rules, technical flow, deployment) never resolve through the public help route, even
    /// though the underlying markdown files exist on disk for internal reference.
    /// </summary>
    /// <param name="document">A technical-only document name that must not be publicly resolvable.</param>
    [Theory]
    [InlineData("api")]
    [InlineData("datenmodell")]
    [InlineData("business-rules")]
    [InlineData("ablauf-technisch")]
    [InlineData("bereitstellung")]
    public void TryResolveDocument_RejectsTechnicalOnlyDocuments(string document)
    {
        var docsPath = GetDocsHelpPath();

        var resolved = HelpContentCatalog.TryResolveDocument(
            docsPath,
            "de",
            $"systemverwaltung-und-setup/{document}",
            out _,
            out _,
            out _);

        Assert.False(resolved);
    }

    /// <summary>
    /// Verifies that resolving a bare topic route (no explicit document segment) maps to that topic's primary
    /// "beschreibung" document and the corresponding markdown file path on disk.
    /// </summary>
    [Fact]
    public void TryResolveDocument_MapsTopicRouteToReviewedPrimaryDocument()
    {
        var docsPath = GetDocsHelpPath();

        var resolved = HelpContentCatalog.TryResolveDocument(
            docsPath,
            "de",
            "budgetplanung",
            out var topic,
            out var document,
            out var markdownPath);

        Assert.True(resolved);
        Assert.Equal("budgetplanung", topic.Id);
        Assert.Equal("beschreibung", document.RouteSegment);
        Assert.EndsWith(Path.Combine("budgetplanung", "beschreibung.md"), markdownPath);
    }

    /// <summary>
    /// Verifies that the search index built from the on-disk docs has exactly one entry per catalog topic,
    /// using the topics' own ids, and that no indexed document id contains a nested path segment — the index
    /// is meant to surface topics, not every individual sub-document.
    /// </summary>
    [Fact]
    public void HelpSearchIndexBuilder_UsesCatalogTopicsAndPrimaryDocuments()
    {
        var docsPath = GetDocsHelpPath();

        var index = HelpSearchIndexBuilder.Build(docsPath, "de");

        Assert.Equal(HelpContentCatalog.Topics.Count, index.Documents.Count);
        Assert.Equal(
            HelpContentCatalog.Topics.Select(topic => topic.Id).OrderBy(id => id),
            index.Documents.Select(document => document.Id).OrderBy(id => id));
        Assert.DoesNotContain(index.Documents, document => document.Id.Contains('/', StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that no topic in the catalog publishes a document whose file name is on the
    /// <see cref="HelpContentCatalog.TechnicalOnlyDocumentNames"/> list, and that every document the catalog
    /// does reference actually exists on disk — catching both an accidental technical-doc publish and a
    /// broken/renamed markdown file reference.
    /// </summary>
    [Fact]
    public void CatalogDocuments_ExistAndDoNotPublishTechnicalOnlyFiles()
    {
        var docsPath = GetDocsHelpPath();
        var publishedFileNames = HelpContentCatalog.Topics
            .SelectMany(topic => topic.Documents.Select(document => document.FileName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(publishedFileNames.Intersect(HelpContentCatalog.TechnicalOnlyDocumentNames, StringComparer.OrdinalIgnoreCase));

        foreach (var topic in HelpContentCatalog.Topics)
        {
            foreach (var document in topic.Documents)
            {
                var path = Path.Combine(docsPath, topic.Id, document.FileName);
                Assert.True(File.Exists(path), $"Published help document is missing: {path}");
            }
        }
    }

    /// <summary>
    /// Verifies that the content of every published help document is free of technical implementation
    /// markers (class/type name fragments like "Controller"/"ViewModel", config keys like "Jwt:", etc.) —
    /// a content-level guard against implementation details leaking into user-facing help text, complementing
    /// the file-name-based check in <see cref="CatalogDocuments_ExistAndDoNotPublishTechnicalOnlyFiles"/>.
    /// </summary>
    [Fact]
    public void CatalogDocuments_DoNotContainTechnicalImplementationMarkers()
    {
        var docsPath = GetDocsHelpPath();

        foreach (var topic in HelpContentCatalog.Topics)
        {
            foreach (var document in topic.Documents)
            {
                var path = Path.Combine(docsPath, topic.Id, document.FileName);
                var content = File.ReadAllText(path);

                foreach (var marker in TechnicalContentMarkers)
                {
                    Assert.True(
                        !content.Contains(marker, StringComparison.OrdinalIgnoreCase),
                        $"Technical marker '{marker}' found in published help document: {path}");
                }
            }
        }
    }

    private static string GetDocsHelpPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Docs",
            "help"));
    }
}
