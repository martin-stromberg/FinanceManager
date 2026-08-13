using FinanceManager.Application.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Verifies cache invalidation behavior for budget purpose write operations (create/update/delete), in
/// particular when a purpose's contact/contact-group/savings-plan source assignment changes.
/// </summary>
public sealed class BudgetPurposeServiceCacheInvalidationTests
{
    /// <summary>
    /// Ensures create operations trigger report cache invalidation.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldInvalidateAllReportCacheEntries_WhenPurposeIsCreated()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetPurposeService(db, cache.Object);

        var created = await sut.CreateAsync(
            ownerId,
            "Utilities",
            BudgetSourceType.Contact,
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None);

        created.Should().NotBeNull();
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Ensures update operations that change the purpose's source assignment (contact/contact-group/
    /// savings-plan) trigger report cache invalidation, so the report never keeps serving cached data
    /// computed against the purpose's previous source.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldInvalidateAllReportCacheEntries_WhenSourceAssignmentChanges()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetPurposeService(db, cache.Object);
        var created = await sut.CreateAsync(
            ownerId,
            "Utilities",
            BudgetSourceType.Contact,
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None);

        cache.Invocations.Clear();
        var newSourceId = Guid.NewGuid();
        var updated = await sut.UpdateAsync(
            created.Id,
            ownerId,
            "Utilities",
            BudgetSourceType.Contact,
            newSourceId,
            null,
            null,
            CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.SourceId.Should().Be(newSourceId);
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Ensures delete operations trigger report cache invalidation when a purpose was removed.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldInvalidateAllReportCacheEntries_WhenPurposeIsDeleted()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetPurposeService(db, cache.Object);
        var created = await sut.CreateAsync(
            ownerId,
            "Utilities",
            BudgetSourceType.Contact,
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None);

        cache.Invocations.Clear();
        var deleted = await sut.DeleteAsync(created.Id, ownerId, CancellationToken.None);

        deleted.Should().BeTrue();
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Ensures deleting a non-existing purpose does not trigger cache invalidation.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldNotInvalidateCache_WhenPurposeDoesNotExist()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var cache = new Mock<IReportCacheService>();
        cache
            .Setup(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new BudgetPurposeService(db, cache.Object);

        var deleted = await sut.DeleteAsync(Guid.NewGuid(), ownerId, CancellationToken.None);

        deleted.Should().BeFalse();
        cache.Verify(x => x.MarkAllReportCacheEntriesForUpdateAsync(ownerId, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Ensures the cache service dependency being absent (e.g. in contexts that don't register it) does
    /// not break create/update/delete - cache invalidation is best-effort.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldSucceed_WhenNoReportCacheServiceIsRegistered()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);

        var sut = new BudgetPurposeService(db);

        var created = await sut.CreateAsync(
            ownerId,
            "Utilities",
            BudgetSourceType.Contact,
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None);

        created.Should().NotBeNull();
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
}
