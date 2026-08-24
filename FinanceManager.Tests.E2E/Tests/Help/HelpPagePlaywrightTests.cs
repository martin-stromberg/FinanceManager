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

        await page.Locator("#featureList .feature-card").First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible
        });

        var featureCards = await page.Locator("#featureList .feature-card").CountAsync();
        featureCards.Should().BeGreaterThan(0);

        var featureListText = await page.Locator("#featureList").TextContentAsync();
        featureListText.Should().Contain("Konten und Buchungen");

        var emptyMessages = await page.GetByText("Keine Dokumentation verfügbar").CountAsync();
        emptyMessages.Should().Be(0);

        var loadingMessages = await page.GetByText("Dokumentation wird geladen").CountAsync();
        loadingMessages.Should().Be(0);

        var errorMessages = await page.GetByText("Fehler:").CountAsync();
        errorMessages.Should().Be(0);
    }
}
