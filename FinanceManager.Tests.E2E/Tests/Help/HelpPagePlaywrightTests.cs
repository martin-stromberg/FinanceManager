namespace FinanceManager.Tests.E2E;

[Collection(PlaywrightCollection.CollectionName)]
public sealed class HelpPagePlaywrightTests
{
    private readonly PlaywrightWebAppFixture _fixture;

    public HelpPagePlaywrightTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

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
