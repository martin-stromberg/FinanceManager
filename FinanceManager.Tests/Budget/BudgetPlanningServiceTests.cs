using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Budget;

public sealed class BudgetPlanningServiceTests
{
    [Fact]
    public async Task CalculatePlannedValuesAsync_ShouldReturnMonthlyRuleAmount_WhenMonthlyRuleExists()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);

        var purpose = new BudgetPurpose(ownerId, "Car provision", BudgetSourceType.SavingsPlan, Guid.NewGuid());
        db.BudgetPurposes.Add(purpose);

        db.BudgetRules.Add(new BudgetRule(ownerId, purpose.Id, 50m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)));
        await db.SaveChangesAsync();

        var repo = new BudgetPlanningRepository(db);
        var svc = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, repo);

        // Act
        var res = await svc.CalculatePlannedValuesAsync(ownerId, new[] { purpose.Id }, new BudgetPeriodKey(2026, 1), new BudgetPeriodKey(2026, 1), CancellationToken.None);

        // Assert
        Assert.Single(res.Values, v => v.BudgetPurposeId == purpose.Id && v.Period == new BudgetPeriodKey(2026, 1) && v.Amount == 50m);
    }

    [Fact]
    public async Task CalculatePlannedValuesAsync_ShouldReturnYearlyRuleAmountOnlyInStartMonth_WhenYearlyRuleExists()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);

        var purpose = new BudgetPurpose(ownerId, "Insurance", BudgetSourceType.Contact, Guid.NewGuid());
        db.BudgetPurposes.Add(purpose);

        db.BudgetRules.Add(new BudgetRule(ownerId, purpose.Id, 600m, BudgetIntervalType.Yearly, new DateOnly(2026, 5, 1)));
        await db.SaveChangesAsync();

        var repo = new BudgetPlanningRepository(db);
        var svc = new BudgetPlanningService(NullLogger<BudgetPlanningService>.Instance, repo);

        // Act
        var res = await svc.CalculatePlannedValuesAsync(ownerId, new[] { purpose.Id }, new BudgetPeriodKey(2026, 1), new BudgetPeriodKey(2026, 12), CancellationToken.None);

        // Assert
        Assert.Equal(600m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 5)));
        Assert.Equal(0m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 4)));
        Assert.Equal(0m, res.GetPlanned(purpose.Id, new BudgetPeriodKey(2026, 6)));
    }

    [Fact]
    public async Task CalculatePlannedValuesAsync_ShouldApplyOverride_WhenOverrideExistsForMonth()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);

        var purpose = new BudgetPurpose(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid());
        db.BudgetPurposes.Add(purpose);

        db.BudgetRules.Add(new BudgetRule(ownerId, purpose.Id, 350m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)));
        db.BudgetOverrides.Add(new BudgetOverride(ownerId, purpose.Id, new BudgetPeriodKey(2026, 3), 500m));
        await db.SaveChangesAsync();

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
