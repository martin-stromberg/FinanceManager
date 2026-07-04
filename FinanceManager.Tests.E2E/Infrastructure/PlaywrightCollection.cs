namespace FinanceManager.Tests.E2E;

[CollectionDefinition(CollectionName)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightWebAppFixture>
{
    public const string CollectionName = "Playwright";
}
