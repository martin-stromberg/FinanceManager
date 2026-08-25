using FinanceManager.Web.Services.Help;

namespace FinanceManager.Tests.Web.Help;

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

    [Fact]
    public void Topics_ContainsReviewedUserHelpTopicsOnly()
    {
        Assert.Equal(12, HelpContentCatalog.Topics.Count);
        Assert.Contains(HelpContentCatalog.Topics, topic => topic.Id == "konten-und-buchungen");
        Assert.DoesNotContain(HelpContentCatalog.Topics, topic => topic.Id == "bestandsaufnahme");
    }

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
