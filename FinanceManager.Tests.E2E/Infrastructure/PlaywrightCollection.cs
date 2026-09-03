namespace FinanceManager.Tests.E2E;

/// <summary>
/// xUnit collection definition that binds all Playwright E2E test classes to a single shared
/// <see cref="PlaywrightWebAppFixture"/> instance, so the test server and browser are started once per
/// collection rather than once per test class, which would be prohibitively slow.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightWebAppFixture>
{
    /// <summary>
    /// The xUnit collection name test classes reference via <c>[Collection(CollectionName)]</c> to share
    /// this collection's <see cref="PlaywrightWebAppFixture"/> instance.
    /// </summary>
    public const string CollectionName = "Playwright";
}
