using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Verifies cache invalidation behavior for budget rule write operations.
/// </summary>
public sealed class BudgetRuleServiceCacheInvalidationTests
{
    /// <summary>
    /// Ensures create operations trigger report cache invalidation.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldInvalidateAllReportCacheEntries_WhenRuleIsCreated()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetRuleService(db, cache.Object);

        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            120m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            CancellationToken.None);

        created.Should().NotBeNull();
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Ensures update operations trigger report cache invalidation.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldInvalidateAllReportCacheEntries_WhenRuleIsUpdated()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetRuleService(db, cache.Object);
        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            CancellationToken.None);

        cache.Invocations.Clear();
        var updated = await sut.UpdateAsync(
            created.Id,
            ownerId,
            150m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Amount.Should().Be(150m);
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Ensures delete operations trigger report cache invalidation when a rule was removed.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldInvalidateAllReportCacheEntries_WhenRuleIsDeleted()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetRuleService(db, cache.Object);
        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            CancellationToken.None);

        cache.Invocations.Clear();
        var deleted = await sut.DeleteAsync(created.Id, ownerId, CancellationToken.None);

        deleted.Should().BeTrue();
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Ensures deleting a non-existing rule does not trigger cache invalidation.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldNotInvalidateCache_WhenRuleDoesNotExist()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetRuleService(db, cache.Object);

        var deleted = await sut.DeleteAsync(Guid.NewGuid(), ownerId, CancellationToken.None);

        deleted.Should().BeFalse();
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid ownerId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var user = new User("cache-owner", "hash");
        user.Id = ownerId;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<BudgetPurpose> CreatePurposeAsync(AppDbContext db, Guid ownerId)
    {
        var purpose = new BudgetPurpose(ownerId, "Utilities", BudgetSourceType.Contact, Guid.NewGuid());
        db.BudgetPurposes.Add(purpose);
        await db.SaveChangesAsync();
        return purpose;
    }
}
