namespace FinanceManager.Tests.E2E;

/// <summary>Isolates first-user demo registration from tests that have already registered users.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BudgetDemoDataCollection : ICollectionFixture<PlaywrightWebAppFixture>
{
    /// <summary>The dedicated first-user scenario collection.</summary>
    public const string Name = "BudgetDemoData";
}
