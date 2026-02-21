using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Budget;

public sealed class BudgetCategoryServiceTests
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
    public async Task BudgetCategoryService_CRUD_ShouldWork()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var svc = new BudgetCategoryService(db, purposeSvc);

        var created = await svc.CreateAsync(ownerId, "Food", CancellationToken.None);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Food", created.Name);

        var got = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.NotNull(got);
        Assert.Equal(created.Id, got!.Id);

        var updated = await svc.UpdateAsync(created.Id, ownerId, "Food2", CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Food2", updated!.Name);

        var list = await svc.ListAsync(ownerId, CancellationToken.None);
        Assert.Single(list);

        var delOk = await svc.DeleteAsync(created.Id, ownerId, CancellationToken.None);
        Assert.True(delOk);

        var gone = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.Null(gone);

        var list2 = await svc.ListAsync(ownerId, CancellationToken.None);
        Assert.Empty(list2);
    }

    [Fact]
    public async Task BudgetCategoryService_Delete_ShouldClearCategoryFromPurposes()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);

        var cat = await catSvc.CreateAsync(ownerId, "C", CancellationToken.None);
        var purpose = await purposeSvc.CreateAsync(ownerId, "P", Shared.Dtos.Budget.BudgetSourceType.ContactGroup, Guid.NewGuid(), null, cat.Id, CancellationToken.None);

        var before = await purposeSvc.GetAsync(purpose.Id, ownerId, CancellationToken.None);
        Assert.NotNull(before);
        Assert.Equal(cat.Id, before!.BudgetCategoryId);

        var ok = await catSvc.DeleteAsync(cat.Id, ownerId, CancellationToken.None);
        Assert.True(ok);

        var after = await purposeSvc.GetAsync(purpose.Id, ownerId, CancellationToken.None);
        Assert.NotNull(after);
        Assert.Null(after!.BudgetCategoryId);
    }
}
