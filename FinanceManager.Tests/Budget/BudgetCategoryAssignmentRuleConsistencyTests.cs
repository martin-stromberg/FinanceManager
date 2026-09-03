using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Application.Exceptions;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Verifies the invariant that a budget category cannot end up with both purpose-level and direct
/// category-level budget rules active for it at the same time, since combining both would double-count the
/// category's budgeted amount in reports (once via the purpose's rules, once via the direct category rule).
/// Covers rejection of that conflicting state from both the purpose-assignment side and the
/// category-rule-creation side, plus the non-conflicting combinations that must still be allowed.
/// </summary>
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

    /// <summary>
    /// Ensures assigning a category to a purpose is rejected when the purpose already has its own budget
    /// rules and the target category already has direct category-level rules - combining both would
    /// double-count the category's budgeted amount (once via the purpose, once via the direct category rule).
    /// </summary>
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

    /// <summary>
    /// Ensures a purpose can still be assigned to a category that has direct category-level rules as long as
    /// the purpose itself has no rules yet - there is nothing on the purpose side that could be double-counted.
    /// </summary>
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

    /// <summary>
    /// Ensures a purpose with its own budget rules can still be assigned to a category as long as that
    /// category has no direct category-level rules of its own - only the combination of both rule kinds is
    /// forbidden, not either one in isolation.
    /// </summary>
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

    /// <summary>
    /// Ensures assigning a purpose to a category id that does not exist fails with an ArgumentException
    /// instead of silently persisting a dangling category reference.
    /// </summary>
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

    /// <summary>
    /// Ensures creating a direct category-level rule is rejected when the category is already assigned to a
    /// purpose that has its own budget rules - the same double-counting conflict as
    /// <see cref="BudgetPurposeService_Update_ShouldReject_CategoryAssignment_WhenPurposeRulesExistAndCategoryRulesExist"/>,
    /// but triggered from the category-rule-creation side instead of the purpose-update side.
    /// </summary>
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
