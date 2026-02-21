using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Application.Exceptions;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Budget;

public sealed class BudgetCategoryAssignmentRuleConsistencyTests
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
    public async Task BudgetPurposeService_Update_ShouldReject_CategoryAssignment_WhenPurposeRulesExistAndCategoryRulesExist()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);

        var category = await catSvc.CreateAsync(ownerId, "Cat", CancellationToken.None);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Purpose", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

        await ruleSvc.CreateAsync(ownerId, purpose.Id, 10m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);
        await ruleSvc.CreateForCategoryAsync(ownerId, category.Id, 5m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(async () =>
        {
            await purposeSvc.UpdateAsync(purpose.Id, ownerId, "Purpose", BudgetSourceType.ContactGroup, purpose.SourceId, null, category.Id, CancellationToken.None);
        });

        Assert.Equal("Err_Conflict_CategoryAndPurposeRules", ex.Code);
    }

    [Fact]
    public async Task BudgetPurposeService_Update_ShouldAllow_CategoryAssignment_WhenOnlyCategoryRulesExist()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);

        var category = await catSvc.CreateAsync(ownerId, "Cat", CancellationToken.None);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Purpose", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

        await ruleSvc.CreateForCategoryAsync(ownerId, category.Id, 5m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        var updated = await purposeSvc.UpdateAsync(purpose.Id, ownerId, "Purpose", BudgetSourceType.ContactGroup, purpose.SourceId, null, category.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(category.Id, updated!.BudgetCategoryId);
    }

    [Fact]
    public async Task BudgetPurposeService_Update_ShouldAllow_CategoryAssignment_WhenOnlyPurposeRulesExist()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);

        var category = await catSvc.CreateAsync(ownerId, "Cat", CancellationToken.None);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Purpose", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

        await ruleSvc.CreateAsync(ownerId, purpose.Id, 10m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        var updated = await purposeSvc.UpdateAsync(purpose.Id, ownerId, "Purpose", BudgetSourceType.ContactGroup, purpose.SourceId, null, category.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(category.Id, updated!.BudgetCategoryId);
    }

    [Fact]
    public async Task BudgetPurposeService_Update_ShouldReject_NonExistingCategory()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Purpose", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

        var missingId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await purposeSvc.UpdateAsync(purpose.Id, ownerId, "Purpose", BudgetSourceType.ContactGroup, purpose.SourceId, null, missingId, CancellationToken.None);
        });
    }

    [Fact]
    public async Task BudgetRuleService_CreateForCategory_ShouldReject_WhenAssignedPurposeHasPurposeRules()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var catSvc = new BudgetCategoryService(db, purposeSvc);
        var ruleSvc = new BudgetRuleService(db);

        var category = await catSvc.CreateAsync(ownerId, "Cat", CancellationToken.None);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Purpose", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, category.Id, CancellationToken.None);

        await ruleSvc.CreateAsync(ownerId, purpose.Id, 10m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ruleSvc.CreateForCategoryAsync(ownerId, category.Id, 5m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null, CancellationToken.None);
        });
    }
}
