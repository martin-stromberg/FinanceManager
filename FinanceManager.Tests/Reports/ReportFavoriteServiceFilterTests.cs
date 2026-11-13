using System.Text.Json;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using FinanceManager.Shared.Dtos.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Free-text filter persistence in <see cref="ReportFavoriteService"/>: create/update store the four
/// text filter lists as JSON columns and return them via the filters DTO; overlong entries are
/// rejected with <see cref="ArgumentException"/>; favorites without text filters expose no filter DTO.
/// </summary>
public sealed class ReportFavoriteServiceFilterTests
{
    private static AppDbContext CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<(ReportFavoriteService svc, Guid userId)> CreateServiceAsync(AppDbContext db)
    {
        var user = new FinanceManager.Domain.Users.User("user", "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (new ReportFavoriteService(db), user.Id);
    }

    /// <summary>
    /// Creating a favorite with text filters persists them as JSON columns and the created DTO as
    /// well as subsequent list/get calls return the same lists.
    /// </summary>
    [Fact]
    public async Task CreateListGet_ShouldRoundtripTextFilters()
    {
        using var db = CreateDb();
        var (svc, userId) = await CreateServiceAsync(db);

        var filters = new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null,
            BankTextFilters: new[] { "Tankstelle", "Miete" }, SecurityTextFilters: new[] { "Dividende" });
        var created = await svc.CreateAsync(userId,
            new ReportFavoriteCreateRequest("Texts", PostingKind.Bank, false, ReportInterval.Month, 24, false, false, false, true, true, Filters: filters),
            CancellationToken.None);

        Assert.Equal(new[] { "Tankstelle", "Miete" }, created.Filters!.BankTextFilters);
        Assert.Equal(new[] { "Dividende" }, created.Filters!.SecurityTextFilters);
        Assert.Null(created.Filters!.ContactTextFilters);

        var entity = await db.ReportFavorites.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "Tankstelle", "Miete" }, JsonSerializer.Deserialize<string[]>(entity.BankTextFiltersJson!));
        Assert.Equal(new[] { "Dividende" }, JsonSerializer.Deserialize<string[]>(entity.SecurityTextFiltersJson!));
        Assert.Null(entity.ContactTextFiltersJson);
        Assert.Null(entity.SavingsPlanTextFiltersJson);

        var listed = await svc.ListAsync(userId, CancellationToken.None);
        Assert.Equal(new[] { "Tankstelle", "Miete" }, listed.Single().Filters!.BankTextFilters);

        var fetched = await svc.GetAsync(created.Id, userId, CancellationToken.None);
        Assert.Equal(new[] { "Dividende" }, fetched!.Filters!.SecurityTextFilters);
    }

    /// <summary>
    /// Updating a favorite replaces the stored text filters; entries are trimmed and deduplicated
    /// case-insensitively before persistence.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldReplaceTextFilters()
    {
        using var db = CreateDb();
        var (svc, userId) = await CreateServiceAsync(db);

        var created = await svc.CreateAsync(userId,
            new ReportFavoriteCreateRequest("Fav", PostingKind.Contact, false, ReportInterval.Month, 24, false, false, false, true, true,
                Filters: new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null, ContactTextFilters: new[] { "Old" })),
            CancellationToken.None);

        var updated = await svc.UpdateAsync(created.Id, userId,
            new ReportFavoriteUpdateRequest("Fav", PostingKind.Contact, false, ReportInterval.Month, 24, false, false, false, true, true,
                Filters: new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null,
                    ContactTextFilters: new[] { "  New  ", "NEW", "Zweiter" })),
            CancellationToken.None);

        Assert.Equal(new[] { "New", "Zweiter" }, updated!.Filters!.ContactTextFilters);
        var entity = await db.ReportFavorites.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "New", "Zweiter" }, JsonSerializer.Deserialize<string[]>(entity.ContactTextFiltersJson!));
    }

    /// <summary>
    /// Updating a favorite with <c>Filters = null</c> clears previously stored text filters:
    /// all four JSON columns become <c>null</c> and the DTO exposes no filters anymore.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_NullFilters_ClearsTextFilters()
    {
        using var db = CreateDb();
        var (svc, userId) = await CreateServiceAsync(db);

        var created = await svc.CreateAsync(userId,
            new ReportFavoriteCreateRequest("Fav", PostingKind.Bank, false, ReportInterval.Month, 24, false, false, false, true, true,
                Filters: new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null,
                    BankTextFilters: new[] { "Miete" }, SecurityTextFilters: new[] { "Dividende" })),
            CancellationToken.None);

        var updated = await svc.UpdateAsync(created.Id, userId,
            new ReportFavoriteUpdateRequest("Fav", PostingKind.Bank, false, ReportInterval.Month, 24, false, false, false, true, true,
                Filters: null),
            CancellationToken.None);

        Assert.Null(updated!.Filters);
        var entity = await db.ReportFavorites.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(entity.BankTextFiltersJson);
        Assert.Null(entity.ContactTextFiltersJson);
        Assert.Null(entity.SavingsPlanTextFiltersJson);
        Assert.Null(entity.SecurityTextFiltersJson);
    }

    /// <summary>
    /// A text filter entry longer than 500 characters is rejected with <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenTextFilterExceedsMaxLength()
    {
        using var db = CreateDb();
        var (svc, userId) = await CreateServiceAsync(db);

        var filters = new ReportFavoriteFiltersDto(null, null, null, null, null, null, null, null, null,
            BankTextFilters: new[] { new string('x', 501) });
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(userId,
            new ReportFavoriteCreateRequest("TooLong", PostingKind.Bank, false, ReportInterval.Month, 24, false, false, false, true, true, Filters: filters),
            CancellationToken.None));
    }

    /// <summary>
    /// Favorites created without any filters (including the legacy state without text filter
    /// columns) expose a <c>null</c> filters DTO.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldReturnNullFilters_WhenNoFiltersConfigured()
    {
        using var db = CreateDb();
        var (svc, userId) = await CreateServiceAsync(db);

        var created = await svc.CreateAsync(userId,
            new ReportFavoriteCreateRequest("Plain", PostingKind.Bank, false, ReportInterval.Month, 24, false, false, false, true, true),
            CancellationToken.None);

        var fetched = await svc.GetAsync(created.Id, userId, CancellationToken.None);
        Assert.Null(fetched!.Filters);
    }
}
