using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Covers the CRUD behavior of the three services that back a budget's core configuration -
/// <see cref="BudgetPurposeService"/>, <see cref="BudgetRuleService"/> and <see cref="BudgetOverrideService"/> -
/// including <see cref="BudgetRuleService"/>'s validation of the optional regex-based purpose pattern.
/// </summary>
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

    /// <summary>
    /// Exercises the full create/read/update/list/delete lifecycle of a budget purpose, including the
    /// list endpoint's name-substring filter, to confirm each step reflects the previous one's state.
    /// </summary>
    [Fact]
    public async Task BudgetPurposeService_CRUD_ShouldWork()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var svc = new BudgetPurposeService(db);

        var created = await svc.CreateAsync(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid(), "desc", null, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, created.Id);

        var got = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.NotNull(got);
        Assert.Equal("Groceries", got!.Name);

        var updated = await svc.UpdateAsync(created.Id, ownerId, "Groceries2", BudgetSourceType.ContactGroup, created.SourceId, null, null, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Groceries2", updated!.Name);

        var list = await svc.ListAsync(ownerId, 0, 50, null, "Groc", CancellationToken.None);
        Assert.Single(list);

        var delOk = await svc.DeleteAsync(created.Id, ownerId, CancellationToken.None);
        Assert.True(delOk);
        var gone = await svc.GetAsync(created.Id, ownerId, CancellationToken.None);
        Assert.Null(gone);
    }

    /// <summary>
    /// Exercises the full create/read/update/list-by-purpose/delete lifecycle of a monthly budget rule
    /// attached to a purpose, confirming the amount change from an update is visible on subsequent reads.
    /// </summary>
    [Fact]
    public async Task BudgetRuleService_CRUD_ShouldWork()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

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

    /// <summary>
    /// Verifies that a syntactically valid regex purpose pattern is accepted as-is on creation - the service
    /// only checks that the pattern compiles, it does not require the pattern to actually match anything,
    /// since a rule may legitimately be created before any matching postings exist.
    /// </summary>
    [Fact]
    public async Task BudgetRuleService_Create_ShouldAcceptCompilableRegexPattern_WithoutMatchingValidation()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Electricity", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

        var svc = new BudgetRuleService(db);

        var created = await svc.CreateAsync(
            ownerId,
            purpose.Id,
            120m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "^ST\\d{10}$",
            true,
            CancellationToken.None);

        Assert.Equal("^ST\\d{10}$", created.PurposePattern);
        Assert.True(created.UseRegex);
    }

    /// <summary>
    /// Verifies that creating a rule with a purpose pattern that fails to compile as a regex (e.g. an
    /// unbalanced parenthesis) throws <see cref="ArgumentException"/> rather than persisting a broken
    /// pattern that would fail later, at posting-matching time.
    /// </summary>
    [Fact]
    public async Task BudgetRuleService_Create_ShouldRejectInvalidRegexPattern()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Electricity", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

        var svc = new BudgetRuleService(db);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await svc.CreateAsync(
                ownerId,
                purpose.Id,
                120m,
                BudgetIntervalType.Monthly,
                null,
                new DateOnly(2026, 1, 1),
                null,
                "(",
                true,
                CancellationToken.None));
    }

    /// <summary>
    /// Verifies that the same regex-compilability guard enforced on create also applies to update - an
    /// existing rule with a valid pattern cannot be modified into one with a broken pattern.
    /// </summary>
    [Fact]
    public async Task BudgetRuleService_Update_ShouldRejectInvalidRegexPattern()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Electricity", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

        var svc = new BudgetRuleService(db);
        var created = await svc.CreateAsync(
            ownerId,
            purpose.Id,
            120m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "ST\\d{10}",
            true,
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await svc.UpdateAsync(
                created.Id,
                ownerId,
                120m,
                BudgetIntervalType.Monthly,
                null,
                new DateOnly(2026, 1, 1),
                null,
                "(",
                true,
                CancellationToken.None));
    }

    /// <summary>
    /// Exercises the full create/read/update/list-by-purpose/delete lifecycle of a per-period budget override
    /// (identified by a <see cref="BudgetPeriodKey"/>), which lets a single month's expected amount deviate
    /// from the purpose's regular rule without changing the rule itself.
    /// </summary>
    [Fact]
    public async Task BudgetOverrideService_CRUD_ShouldWork()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var purposeSvc = new BudgetPurposeService(db);
        var purpose = await purposeSvc.CreateAsync(ownerId, "Groceries", BudgetSourceType.ContactGroup, Guid.NewGuid(), null, null, CancellationToken.None);

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
