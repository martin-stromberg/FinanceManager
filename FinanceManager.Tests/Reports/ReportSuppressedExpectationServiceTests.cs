using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Reports;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using FinanceManager.Shared.Dtos.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers <see cref="ReportSuppressedExpectationService"/>: creating, listing and removing
/// per-favorite suppression markers, idempotent suppression, owner and favorite scoping, and
/// prior-year-date normalization (the suppression key never includes the amount).
/// </summary>
public sealed class ReportSuppressedExpectationServiceTests
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

    private static async Task<FinanceManager.Domain.Users.User> AddUserAsync(AppDbContext db, string name)
    {
        var user = new FinanceManager.Domain.Users.User(name, "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static ReportFavorite AddFavorite(AppDbContext db, Guid ownerUserId, string name)
    {
        var favorite = new ReportFavorite(ownerUserId, name, PostingKind.Bank, false, ReportInterval.Month, false, false, true, true, true, 12);
        db.ReportFavorites.Add(favorite);
        return favorite;
    }

    private static Account AddAccount(AppDbContext db, Guid ownerUserId, string name)
    {
        var account = new Account(ownerUserId, AccountType.Giro, name, null, Guid.NewGuid());
        db.Accounts.Add(account);
        return account;
    }

    private static ReportSuppressedExpectationService CreateSut(AppDbContext db) => new(db);

    /// <summary>
    /// Suppressing an expected item stores the marker and returns it via the favorite-scoped
    /// list with the resolved entity name and the expected date (prior year + 1).
    /// </summary>
    [Fact]
    public async Task SuppressAsync_CreatesMarker_AndListsIt()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-user");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var account = AddAccount(db, user.Id, "Festgeld");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);
        var priorYearDate = new DateTime(2025, 5, 10);
        var dto = await sut.SuppressAsync(user.Id, favorite.Id,
            new ReportSuppressedExpectationRequest(PostingKind.Bank, account.Id, priorYearDate),
            TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal(PostingKind.Bank, dto!.Kind);
        Assert.Equal(account.Id, dto.EntityId);
        Assert.Equal("Festgeld", dto.EntityName);
        Assert.Equal(priorYearDate, dto.PriorYearDate);
        Assert.Equal(priorYearDate.AddYears(1), dto.ExpectedDate);
        Assert.NotEqual(default, dto.SuppressedAtUtc);

        var list = await sut.ListAsync(user.Id, favorite.Id, TestContext.Current.CancellationToken);
        var entry = Assert.Single(list);
        Assert.Equal(dto.Id, entry.Id);
        Assert.Equal("Festgeld", entry.EntityName);
    }

    /// <summary>
    /// Suppressing the same (kind, entity, prior-year date) twice is idempotent and keeps a
    /// single marker — the amount is deliberately not part of the key.
    /// </summary>
    [Fact]
    public async Task SuppressAsync_IsIdempotent_PerFavoriteKey()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-idem");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var account = AddAccount(db, user.Id, "Giro");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);
        var req = new ReportSuppressedExpectationRequest(PostingKind.Bank, account.Id, new DateTime(2025, 5, 10));
        var first = await sut.SuppressAsync(user.Id, favorite.Id, req, TestContext.Current.CancellationToken);
        var second = await sut.SuppressAsync(user.Id, favorite.Id, req, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(1, await db.ReportSuppressedExpectations.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The same item can be suppressed independently on a different favorite of the same user.
    /// </summary>
    [Fact]
    public async Task SuppressAsync_DifferentFavorites_AreIndependent()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-favs");
        var favA = AddFavorite(db, user.Id, "Fav A");
        var favB = AddFavorite(db, user.Id, "Fav B");
        var account = AddAccount(db, user.Id, "Giro");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);
        var req = new ReportSuppressedExpectationRequest(PostingKind.Bank, account.Id, new DateTime(2025, 5, 10));
        await sut.SuppressAsync(user.Id, favA.Id, req, TestContext.Current.CancellationToken);
        await sut.SuppressAsync(user.Id, favB.Id, req, TestContext.Current.CancellationToken);

        Assert.Equal(2, await db.ReportSuppressedExpectations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(await sut.ListAsync(user.Id, favA.Id, TestContext.Current.CancellationToken));
        Assert.Single(await sut.ListAsync(user.Id, favB.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A suppression on a favorite that does not belong to the caller is rejected and creates
    /// nothing; the list returns empty for foreign favorites.
    /// </summary>
    [Fact]
    public async Task SuppressAsync_ForeignFavorite_ReturnsNull_AndWritesNothing()
    {
        using var db = CreateDb();
        var owner = await AddUserAsync(db, "sup-owner");
        var other = await AddUserAsync(db, "sup-other");
        var foreignFavorite = AddFavorite(db, other.Id, "Foreign");
        var account = AddAccount(db, owner.Id, "Giro");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);
        var dto = await sut.SuppressAsync(owner.Id, foreignFavorite.Id,
            new ReportSuppressedExpectationRequest(PostingKind.Bank, account.Id, new DateTime(2025, 5, 10)),
            TestContext.Current.CancellationToken);

        Assert.Null(dto);
        Assert.Empty(await db.ReportSuppressedExpectations.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await sut.ListAsync(owner.Id, foreignFavorite.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Removing a suppression deletes the marker so the original projection behavior is
    /// restored; removing twice or for a foreign owner returns false.
    /// </summary>
    [Fact]
    public async Task UnsuppressAsync_RemovesMarker_AndIsScoped()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-del");
        var other = await AddUserAsync(db, "sup-del-other");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var account = AddAccount(db, user.Id, "Giro");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);
        var dto = await sut.SuppressAsync(user.Id, favorite.Id,
            new ReportSuppressedExpectationRequest(PostingKind.Bank, account.Id, new DateTime(2025, 5, 10)),
            TestContext.Current.CancellationToken);
        Assert.NotNull(dto);

        // Wrong owner cannot delete it.
        Assert.False(await sut.UnsuppressAsync(other.Id, favorite.Id, dto!.Id, TestContext.Current.CancellationToken));
        // Wrong favorite cannot delete it.
        var otherFav = AddFavorite(db, user.Id, "Other Fav");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(await sut.UnsuppressAsync(user.Id, otherFav.Id, dto.Id, TestContext.Current.CancellationToken));

        Assert.True(await sut.UnsuppressAsync(user.Id, favorite.Id, dto.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await db.ReportSuppressedExpectations.ToListAsync(TestContext.Current.CancellationToken));
        Assert.False(await sut.UnsuppressAsync(user.Id, favorite.Id, dto.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The stored prior-year date is normalized to a date so a request carrying a time
    /// component still matches the projection candidate's date.
    /// </summary>
    [Fact]
    public async Task SuppressAsync_NormalizesPriorYearDate()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-date");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var account = AddAccount(db, user.Id, "Giro");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);
        var dto = await sut.SuppressAsync(user.Id, favorite.Id,
            new ReportSuppressedExpectationRequest(PostingKind.Bank, account.Id, new DateTime(2025, 5, 10, 14, 37, 0)),
            TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal(new DateTime(2025, 5, 10), dto!.PriorYearDate);
        Assert.Equal(new DateTime(2025, 5, 10), (await db.ReportSuppressedExpectations.SingleAsync(TestContext.Current.CancellationToken)).PriorYearDate);
    }
}
