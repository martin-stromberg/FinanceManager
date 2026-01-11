using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Budget;

public sealed class BudgetCrudServicesTests
{
    private static async Task<AppDbContext> CreateDbAsync(Guid ownerId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("test", "hash");
        user.Id = ownerId;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return db;
    }

    [Fact]
    public async Task BudgetPurposeService_CRUD_ShouldWork()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var svc = new BudgetPurposeService(db);

        var created = await svc.CreateAsync(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid(), "desc", CancellationToken.None);
        Assert.NotEqual(Guid.Empty, created.Id);

        var got = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.NotNull(got);
        Assert.Equal("Groceries", got!.Name);

        var updated = await svc.UpdateAsync(created.Id, ownerId, "Groceries2", BudgetSourceType.ContactGroup, created.SourceId, null, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Groceries2", updated!.Name);

        var list = await svc.ListAsync(ownerId, 0, 50, null, "Groc", CancellationToken.None);
        Assert.Single(list);

        var delOk = await svc.DeleteAsync(created.Id, ownerId, CancellationToken.None);
        Assert.True(delOk);
        var gone = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.Null(gone);
    }

    [Fact]
    public async Task BudgetRuleService_CRUD_ShouldWork()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, CancellationToken.None);

        var svc = new BudgetRuleService(db);

        var created = await svc.CreateAsync(ownerId, purpose.Id, 350m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, created.Id);

        var got = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.NotNull(got);
        Assert.Equal(350m, got!.Amount);

        var updated = await svc.UpdateAsync(created.Id, ownerId, 400m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(400m, updated!.Amount);

        var list = await svc.ListByPurposeAsync(ownerId, purpose.Id, CancellationToken.None);
        Assert.Single(list);

        var delOk = await svc.DeleteAsync(created.Id, ownerId, CancellationToken.None);
        Assert.True(delOk);
        var gone = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.Null(gone);
    }

    [Fact]
    public async Task BudgetOverrideService_CRUD_ShouldWork()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, CancellationToken.None);

        var svc = new BudgetOverrideService(db);

        var created = await svc.CreateAsync(ownerId, purpose.Id, new BudgetPeriodKey(2026, 3), 500m, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, created.Id);

        var got = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.NotNull(got);
        Assert.Equal(500m, got!.Amount);

        var updated = await svc.UpdateAsync(created.Id, ownerId, new BudgetPeriodKey(2026, 3), 550m, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(550m, updated!.Amount);

        var list = await svc.ListByPurposeAsync(ownerId, purpose.Id, CancellationToken.None);
        Assert.Single(list);

        var delOk = await svc.DeleteAsync(created.Id, ownerId, CancellationToken.None);
        Assert.True(delOk);
        var gone = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.Null(gone);
    }
}
