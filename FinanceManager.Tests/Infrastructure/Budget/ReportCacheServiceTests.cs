using System.Globalization;
using System.Text.Json;
using FinanceManager.Application;
using FinanceManager.Application.Budget;
using FinanceManager.Domain.Reports;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Infrastructure.Budget;

/// <summary>
/// Tests for <see cref="ReportCacheService"/> cache invalidation behaviors.
/// </summary>
public sealed class ReportCacheServiceTests
{
    /// <summary>
    /// Marks cache entries for update when the booking range is inside the cached range.
    /// </summary>
    [Fact]
    public async Task MarkBudgetReportCacheEntriesForUpdateAsync_ShouldMarkEntry_WhenBookingRangeInsideCacheRange()
    {
        // Arrange
        var (db, service) = CreateService();
        var ownerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var cacheFrom = new DateOnly(2026, 1, 1);
        var cacheTo = new DateOnly(2026, 1, 31);
        var dateBasis = BudgetReportDateBasis.BookingDate;

        var entry = new ReportCacheEntry(ownerId, BuildKey(cacheFrom, cacheTo, dateBasis), "{}",
            JsonSerializer.Serialize(new BudgetReportCacheParameter(cacheFrom, cacheTo, dateBasis)), false);
        db.ReportCacheEntries.Add(entry);
        await db.SaveChangesAsync();

        // Act
        await service.MarkBudgetReportCacheEntriesForUpdateAsync(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15), CancellationToken.None);

        // Assert
        var updated = await db.ReportCacheEntries.AsNoTracking().FirstAsync();
        updated.NeedsRefresh.Should().BeTrue();
    }

    /// <summary>
    /// Does not mark cache entries when the booking range is outside the cached range or cache key prefix differs.
    /// </summary>
    [Fact]
    public async Task MarkBudgetReportCacheEntriesForUpdateAsync_ShouldNotMarkEntry_WhenBookingRangeOutsideCacheRange()
    {
        // Arrange
        var (db, service) = CreateService();
        var ownerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var cacheFrom = new DateOnly(2026, 1, 1);
        var cacheTo = new DateOnly(2026, 1, 31);
        var dateBasis = BudgetReportDateBasis.BookingDate;

        var entry = new ReportCacheEntry(ownerId, BuildKey(cacheFrom, cacheTo, dateBasis), "{}",
            JsonSerializer.Serialize(new BudgetReportCacheParameter(cacheFrom, cacheTo, dateBasis)), false);
        var otherEntry = new ReportCacheEntry(ownerId, "othercache-20260101-20260131-BookingDate", "{}",
            JsonSerializer.Serialize(new BudgetReportCacheParameter(cacheFrom, cacheTo, dateBasis)), false);

        db.ReportCacheEntries.Add(entry);
        db.ReportCacheEntries.Add(otherEntry);
        await db.SaveChangesAsync();

        // Act
        await service.MarkBudgetReportCacheEntriesForUpdateAsync(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 2), CancellationToken.None);

        // Assert
        var entries = await db.ReportCacheEntries.AsNoTracking().OrderBy(e => e.CacheKey).ToListAsync();
        entries.Should().AllSatisfy(e => e.NeedsRefresh.Should().BeFalse());
    }

    /// <summary>
    /// Marks overlapping monthly cache entries when a booking range spans multiple months.
    /// </summary>
    [Fact]
    public async Task MarkBudgetReportCacheEntriesForUpdateAsync_ShouldMarkOverlappingMonths_WhenBookingRangeCrossesMonths()
    {
        // Arrange
        var (db, service) = CreateService();
        var ownerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var dateBasis = BudgetReportDateBasis.BookingDate;

        var janFrom = new DateOnly(2026, 1, 1);
        var janTo = new DateOnly(2026, 1, 31);
        var febFrom = new DateOnly(2026, 2, 1);
        var febTo = new DateOnly(2026, 2, 28);
        var marFrom = new DateOnly(2026, 3, 1);
        var marTo = new DateOnly(2026, 3, 31);

        db.ReportCacheEntries.Add(new ReportCacheEntry(ownerId, BuildKey(janFrom, janTo, dateBasis), "{}",
            JsonSerializer.Serialize(new BudgetReportCacheParameter(janFrom, janTo, dateBasis)), false));
        db.ReportCacheEntries.Add(new ReportCacheEntry(ownerId, BuildKey(febFrom, febTo, dateBasis), "{}",
            JsonSerializer.Serialize(new BudgetReportCacheParameter(febFrom, febTo, dateBasis)), false));
        db.ReportCacheEntries.Add(new ReportCacheEntry(ownerId, BuildKey(marFrom, marTo, dateBasis), "{}",
            JsonSerializer.Serialize(new BudgetReportCacheParameter(marFrom, marTo, dateBasis)), false));
        await db.SaveChangesAsync();

        // Act
        await service.MarkBudgetReportCacheEntriesForUpdateAsync(new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 10), CancellationToken.None);

        // Assert
        var entries = await db.ReportCacheEntries.AsNoTracking().OrderBy(e => e.CacheKey).ToListAsync();
        entries[0].NeedsRefresh.Should().BeTrue();
        entries[1].NeedsRefresh.Should().BeTrue();
        entries[2].NeedsRefresh.Should().BeFalse();
    }

    private static (AppDbContext Db, ReportCacheService Service) CreateService()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var taskManager = new BackgroundTaskManager();
        return (db, new ReportCacheService(db, taskManager));
    }

    private static string BuildKey(DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis)
        => string.Format(CultureInfo.InvariantCulture, "budgetreportraw-{0:yyyyMMdd}-{1:yyyyMMdd}-{2}", from, to, dateBasis);
}
