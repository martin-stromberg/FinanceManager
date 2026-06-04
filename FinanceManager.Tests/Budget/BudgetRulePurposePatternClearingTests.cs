using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Budget;

/// <summary>
/// Verifies that purpose patterns can be cleared/deleted from budget rules.
/// </summary>
public sealed class BudgetRulePurposePatternClearingTests
{
    /// <summary>
    /// Tests that a purpose pattern can be added to a budget rule on creation.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldSetPurposePattern_WhenPatternIsProvided()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var sut = new BudgetRuleService(db);

        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "INVOICE*",
            false,
            CancellationToken.None);

        created.PurposePattern.Should().Be("INVOICE*");
        created.UseRegex.Should().BeFalse();
    }

    /// <summary>
    /// Tests that a purpose pattern can be cleared (set to null) via update.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldClearPurposePattern_WhenPatternIsSetToNull()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var sut = new BudgetRuleService(db);

        // Create with a pattern
        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "INVOICE*",
            false,
            CancellationToken.None);

        created.PurposePattern.Should().Be("INVOICE*");

        // Update and clear the pattern
        var updated = await sut.UpdateAsync(
            created.Id,
            ownerId,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            null,  // <- Clear pattern
            false,
            CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.PurposePattern.Should().BeNull();
        updated.UseRegex.Should().BeFalse();
    }

    /// <summary>
    /// Tests that a purpose pattern can be cleared with an empty string.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldClearPurposePattern_WhenPatternIsSetToEmptyString()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var sut = new BudgetRuleService(db);

        // Create with a pattern
        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "TEST*",
            false,
            CancellationToken.None);

        created.PurposePattern.Should().Be("TEST*");

        // Update with empty string
        var updated = await sut.UpdateAsync(
            created.Id,
            ownerId,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            string.Empty,  // <- Empty string should be treated as clear
            false,
            CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.PurposePattern.Should().BeNull();
        updated.UseRegex.Should().BeFalse();
    }

    /// <summary>
    /// Tests that UseRegex is also reset when pattern is cleared.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldResetUseRegex_WhenPatternIsCleared()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var sut = new BudgetRuleService(db);

        // Create with a regex pattern
        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "INV.*",
            true,  // <- Regex enabled
            CancellationToken.None);

        created.PurposePattern.Should().Be("INV.*");
        created.UseRegex.Should().BeTrue();

        // Clear the pattern
        var updated = await sut.UpdateAsync(
            created.Id,
            ownerId,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            null,  // <- Clear pattern
            false,  // <- UseRegex also reset
            CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.PurposePattern.Should().BeNull();
        updated.UseRegex.Should().BeFalse();
    }

    /// <summary>
    /// Tests that a cleared purpose pattern allows rules to match all bookings again.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldReturnClearedRule_WhenPatternWasCleared()
    {
        var ownerId = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerId);
        var purpose = await CreatePurposeAsync(db, ownerId);
        var sut = new BudgetRuleService(db);

        // Create with pattern
        var created = await sut.CreateAsync(
            ownerId,
            purpose.Id,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "SPECIFIC_PATTERN",
            false,
            CancellationToken.None);

        // Clear pattern
        await sut.UpdateAsync(
            created.Id,
            ownerId,
            100m,
            BudgetIntervalType.Monthly,
            null,
            new DateOnly(2026, 1, 1),
            null,
            null,
            false,
            CancellationToken.None);

        // Reload and verify
        var retrieved = await sut.GetAsync(created.Id, ownerId, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.PurposePattern.Should().BeNull();
        retrieved.UseRegex.Should().BeFalse();
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid userId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new User("testuser@example.com", "hashedpassword");
        user.Id = userId;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return db;
    }

    private static async Task<BudgetPurpose> CreatePurposeAsync(AppDbContext db, Guid userId)
    {
        var purpose = new BudgetPurpose(
            userId, 
            "Test Purpose", 
            BudgetSourceType.Contact, 
            Guid.NewGuid(), 
            null);
        db.BudgetPurposes.Add(purpose);
        await db.SaveChangesAsync();

        return purpose;
    }
}
