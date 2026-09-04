namespace FinanceManager.Tests.E2E;

/// <summary>
/// End-to-end test for the in-app Help hub: verifies that the feature list on the help overview
/// page only shows the user-facing topics (internal/API/data-model topics are excluded), that
/// clicking a topic card navigates to its detail page with the expected content, and that
/// navigating back to the overview does not leave any loading/error/empty-state messages behind.
/// </summary>
[Collection(PlaywrightCollection.CollectionName)]
public sealed class HelpPagePlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HelpPagePlaywrightTests"/> class.
    /// </summary>
    /// <param name="fixture">Shared Playwright web app fixture providing the browser and test server.</param>
    public HelpPagePlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies that the help overview page lists only user-facing topics (excluding internal
    /// topics like "API" or "Datenmodell"), that clicking a topic card navigates to its detail
    /// page and shows the expected content, and that navigating back to the overview leaves no
    /// stray loading, error or empty-state messages visible.
    /// </summary>
    [Fact]
    public async Task HelpHub_ShouldShowDocumentationContent()
    {
        await using var session = await _fixture.CreateSessionAsync(new()
        {
            Locale = "de-DE"
        });

        var page = session.Page;

        await page.GotoAsync("/help");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.Locator("#featureList .help-topic-card").First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible
        });

        var featureCards = await page.Locator("#featureList .help-topic-card").CountAsync();
        featureCards.Should().Be(12);

        var featureListText = await page.Locator("#featureList").TextContentAsync();
        featureListText.Should().Contain("Konten und Buchungen");
        featureListText.Should().NotContain("API");
        featureListText.Should().NotContain("Datenmodell");

        await page.Locator("#featureList .help-topic-card").Filter(new() { HasText = "Konten und Buchungen" }).ClickAsync();
        await page.WaitForURLAsync("**/help/view/konten-und-buchungen");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Konten und Buchungen" }).First.WaitForAsync();
        var detailText = await page.Locator(".help-detail-shell").TextContentAsync();
        detailText.Should().Contain("Vorlaeufige Buchungen");
        detailText.Should().NotContain("API");
        detailText.Should().NotContain("Datenmodell");

        await page.GetByRole(AriaRole.Link, new() { Name = "Hilfeuebersicht" }).ClickAsync();
        await page.WaitForURLAsync("**/help");

        var emptyMessages = await page.GetByText("Es sind keine Hilfethemen verfuegbar").CountAsync();
        emptyMessages.Should().Be(0);

        var loadingMessages = await page.GetByText("Hilfeinhalt wird geladen").CountAsync();
        loadingMessages.Should().Be(0);

        var errorMessages = await page.GetByText("Hilfeseite nicht verfuegbar").CountAsync();
        errorMessages.Should().Be(0);
    }
}
