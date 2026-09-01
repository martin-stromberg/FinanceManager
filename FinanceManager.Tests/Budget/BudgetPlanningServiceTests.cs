using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Covers <see cref="BudgetPlanningService.CalculatePlannedValuesAsync"/>, which projects a purpose's
/// <see cref="BudgetRule"/> into per-month planned amounts over a period - verifying that monthly and
/// yearly recurrence intervals expand correctly and that a <see cref="BudgetOverride"/> takes precedence
/// over the rule for the specific month it targets.
/// </summary>
public sealed class BudgetPlanningServiceTests
{
    /// <summary>
    /// Verifies that a monthly-interval rule produces its amount in the single requested month -
    /// the base case for recurrence expansion before any interval-skipping logic is involved.
    /// </summary>
    [Fact]
    public async Task CalculatePlannedValuesAsync_ShouldReturnMonthlyRuleAmount_WhenMonthlyRuleExists()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);

        var purpose = new BudgetPurpose(ownerId, "Car provision", BudgetSourceType.SavingsPlan, Guid.NewGuid());
        db.BudgetPurposes.Add(purpose);

        db.BudgetRules.Add(new BudgetRule(ownerId, budgetPurposeId: purpose.Id, budgetCategoryId: null, 50m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new BudgetPlanningRepository(db);
        var svc = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, repo);

        // Act
        var res = await svc.CalculatePlannedValuesAsync(ownerId, new[] { purpose.Id }, new BudgetPeriodKey(2026, 1), new BudgetPeriodKey(2026, 1), CancellationToken.None);

        // Assert
        Assert.Single(res.Values, v => v.BudgetPurposeId == purpose.Id && v.Period == new BudgetPeriodKey(2026, 1) && v.Amount == 50m);
    }

    /// <summary>
    /// Verifies that a yearly-interval rule with a start date in May only produces its planned amount
    /// in May, and zero in the neighboring months - a yearly rule must not repeat every month like a
    /// monthly one, and its "home month" is anchored to the rule's start date, not to January.
    /// </summary>
    [Fact]
    public async Task CalculatePlannedValuesAsync_ShouldReturnYearlyRuleAmountOnlyInStartMonth_WhenYearlyRuleExists()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);

        var purpose = new BudgetPurpose(ownerId, "Insurance", BudgetSourceType.Contact, Guid.NewGuid());
        db.BudgetPurposes.Add(purpose);

        db.BudgetRules.Add(new BudgetRule(ownerId, budgetPurposeId: purpose.Id, budgetCategoryId: null, 600m, BudgetIntervalType.Yearly, new DateOnly(2026, 5, 1)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new BudgetPlanningRepository(db);
        var svc = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, repo);

        // Act
        var res = await svc.CalculatePlannedValuesAsync(ownerId, new[] { purpose.Id }, new BudgetPeriodKey(2026, 1), new BudgetPeriodKey(2026, 12), CancellationToken.None);

        // Assert
        Assert.Equal(600m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 5)));
        Assert.Equal(0m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 4)));
        Assert.Equal(0m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 6)));
    }

    /// <summary>
    /// Verifies that a <see cref="BudgetOverride"/> for a specific month replaces the rule's regular
    /// planned amount for that month only, while the surrounding months keep falling back to the rule -
    /// confirming overrides are applied per-period rather than shifting the rule's baseline permanently.
    /// </summary>
    [Fact]
    public async Task CalculatePlannedValuesAsync_ShouldApplyOverride_WhenOverrideExistsForMonth()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);

        var purpose = new BudgetPurpose(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid());
        db.BudgetPurposes.Add(purpose);

        db.BudgetRules.Add(new BudgetRule(ownerId, budgetPurposeId: purpose.Id, budgetCategoryId: null, 350m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)));
        db.BudgetOverrides.Add(new BudgetOverride(ownerId, purpose.Id, new BudgetPeriodKey(2026, 3), 500m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new BudgetPlanningRepository(db);
        var svc = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, repo);

        // Act
        var res = await svc.CalculatePlannedValuesAsync(ownerId, new[] { purpose.Id }, new BudgetPeriodKey(2026, 1), new BudgetPeriodKey(2026, 4), CancellationToken.None);

        // Assert
        Assert.Equal(350m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 2)));
        Assert.Equal(500m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 3)));
        Assert.Equal(350m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 4)));
    }
}
