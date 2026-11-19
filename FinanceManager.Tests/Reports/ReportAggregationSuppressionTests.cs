using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Reports;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Reports;
using FinanceManager.Shared.Dtos.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers per-favorite suppression of projected expectations (<c>CompareProjection = true</c>):
/// a suppression bound to the queried <c>ReportFavoriteId</c> removes the expected item before
/// the confirmation check so the projection amount drops, while the same query without a
/// favorite context — or scoped to another favorite — keeps projecting it.
/// </summary>
public sealed class ReportAggregationSuppressionTests
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

    private static Account AddAccount(AppDbContext db, Guid ownerUserId, string name)
    {
        var account = new Account(ownerUserId, AccountType.Giro, name, null, Guid.NewGuid());
        db.Accounts.Add(account);
        return account;
    }

    private static ReportFavorite AddFavorite(AppDbContext db, Guid ownerUserId, string name)
    {
        var favorite = new ReportFavorite(ownerUserId, name, PostingKind.Bank, false, ReportInterval.Month, false, false, true, true, true, 12);
        db.ReportFavorites.Add(favorite);
        return favorite;
    }

    private static Posting AddBankPosting(AppDbContext db, Account account, DateTime bookingDate, decimal amount, string subject = "Posting")
    {
        var posting = new Posting(
            Guid.NewGuid(), PostingKind.Bank, account.Id, null, null, null,
            bookingDate, bookingDate, amount, subject, null, null, null);
        db.Postings.Add(posting);
        return posting;
    }

    private static ReportSuppressedExpectation AddSuppression(AppDbContext db, Guid ownerUserId, Guid favoriteId, PostingKind kind, Guid entityId, DateTime priorYearDate, Guid? postingId = null, decimal? amount = null)
    {
        var suppression = new ReportSuppressedExpectation(ownerUserId, favoriteId, kind, entityId, priorYearDate, postingId, amount);
        db.ReportSuppressedExpectations.Add(suppression);
        return suppression;
    }

    private static async Task RebuildAggregatesAsync(AppDbContext db, Guid ownerUserId)
    {
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await new PostingAggregateService(db).RebuildForUserAsync(ownerUserId, (_, _) => { }, TestContext.Current.CancellationToken);
    }

    private static ReportAggregationService CreateSut(AppDbContext db)
        => new(db, new NullLogger<ReportAggregationService>());

    private static ReportAggregationQuery Query(Guid ownerUserId, DateTime analysis, Guid? favoriteId = null)
        => new(ownerUserId, PostingKind.Bank, ReportInterval.Month, 1,
            IncludeCategory: false, ComparePrevious: false, CompareYear: false, CompareProjection: true,
            AnalysisDate: analysis, ReportFavoriteId: favoriteId);

    /// <summary>
    /// A suppression bound to the queried favorite removes the expected posting and lowers the
    /// projection amount to the booked amount.
    /// </summary>
    [Fact]
    public async Task QueryAsync_FavoriteSuppression_RemovesExpectedItem()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-agg");
        var account = AddAccount(db, user.Id, "Festgeld");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var analysis = new DateTime(2026, 5, 1);
        var priorYearDate = new DateTime(2025, 5, 10);
        AddBankPosting(db, account, priorYearDate, 120m);
        await RebuildAggregatesAsync(db, user.Id);
        var sut = CreateSut(db);

        var baseline = await sut.QueryAsync(Query(user.Id, analysis, favorite.Id), TestContext.Current.CancellationToken);
        var basePoint = baseline.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(120m, basePoint.ProjectionAmount);
        var expected = Assert.Single(basePoint.ProjectionExpectedItems!);
        Assert.Equal(priorYearDate, expected.PriorYearDate);

        AddSuppression(db, user.Id, favorite.Id, PostingKind.Bank, account.Id, priorYearDate);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(Query(user.Id, analysis, favorite.Id), TestContext.Current.CancellationToken);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(0m, point.Amount);
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// The same query without a favorite context is unaffected: ad-hoc aggregation (API/MCP)
    /// keeps projecting the expected posting even when a suppression exists.
    /// </summary>
    [Fact]
    public async Task QueryAsync_NoFavoriteContext_SuppressionDoesNotApply()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-adhoc");
        var account = AddAccount(db, user.Id, "Festgeld");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var analysis = new DateTime(2026, 5, 1);
        var priorYearDate = new DateTime(2025, 5, 10);
        AddBankPosting(db, account, priorYearDate, 120m);
        AddSuppression(db, user.Id, favorite.Id, PostingKind.Bank, account.Id, priorYearDate);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(Query(user.Id, analysis), TestContext.Current.CancellationToken);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(120m, point.ProjectionAmount);
        Assert.Single(point.ProjectionExpectedItems!);
    }

    /// <summary>
    /// A suppression stored on a different favorite does not affect the queried one.
    /// </summary>
    [Fact]
    public async Task QueryAsync_OtherFavoriteSuppression_DoesNotApply()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-otherfav");
        var account = AddAccount(db, user.Id, "Festgeld");
        var queried = AddFavorite(db, user.Id, "Queried");
        var other = AddFavorite(db, user.Id, "Other");
        var analysis = new DateTime(2026, 5, 1);
        var priorYearDate = new DateTime(2025, 5, 10);
        AddBankPosting(db, account, priorYearDate, 120m);
        AddSuppression(db, user.Id, other.Id, PostingKind.Bank, account.Id, priorYearDate);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(Query(user.Id, analysis, queried.Id), TestContext.Current.CancellationToken);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(120m, point.ProjectionAmount);
        Assert.Single(point.ProjectionExpectedItems!);
    }

    /// <summary>
    /// Deleting the suppression restores the original projection completely.
    /// </summary>
    [Fact]
    public async Task QueryAsync_Unsuppress_RestoresProjection()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-restore");
        var account = AddAccount(db, user.Id, "Festgeld");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var analysis = new DateTime(2026, 5, 1);
        var priorYearDate = new DateTime(2025, 5, 10);
        AddBankPosting(db, account, priorYearDate, 120m);
        var suppression = AddSuppression(db, user.Id, favorite.Id, PostingKind.Bank, account.Id, priorYearDate);
        await RebuildAggregatesAsync(db, user.Id);
        var sut = CreateSut(db);

        var suppressed = await sut.QueryAsync(Query(user.Id, analysis, favorite.Id), TestContext.Current.CancellationToken);
        Assert.Null(suppressed.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis).ProjectionExpectedItems);

        db.ReportSuppressedExpectations.Remove(suppression);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var restored = await sut.QueryAsync(Query(user.Id, analysis, favorite.Id), TestContext.Current.CancellationToken);
        var point = restored.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(120m, point.ProjectionAmount);
        Assert.Single(point.ProjectionExpectedItems!);
    }

    /// <summary>
    /// The suppression key is (favorite, kind, entity, prior-year date) — the amount is not
    /// part of it: an amount correction on the prior-year posting does not lift the marker.
    /// </summary>
    [Fact]
    public async Task QueryAsync_AmountChange_KeepsSuppression()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-amount");
        var account = AddAccount(db, user.Id, "Festgeld");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var analysis = new DateTime(2026, 5, 1);
        var priorYearDate = new DateTime(2025, 5, 10);
        var posting = AddBankPosting(db, account, priorYearDate, 120m);
        AddSuppression(db, user.Id, favorite.Id, PostingKind.Bank, account.Id, priorYearDate);
        await RebuildAggregatesAsync(db, user.Id);

        // Amount correction on the prior-year posting must not lift the suppression.
        db.Postings.Remove(posting);
        AddBankPosting(db, account, priorYearDate, 200m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(Query(user.Id, analysis, favorite.Id), TestContext.Current.CancellationToken);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(0m, point.ProjectionAmount);
        Assert.Null(point.ProjectionExpectedItems);
    }

    /// <summary>
    /// Suppressing one expected posting leaves other expected postings of the same entity and
    /// favorite untouched.
    /// </summary>
    [Fact]
    public async Task QueryAsync_OnlySuppressedCandidate_IsRemoved()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-partial");
        var account = AddAccount(db, user.Id, "Konto");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var analysis = new DateTime(2026, 5, 1);
        // Two distinct prior-year events of one entity (irregular pattern) that both land on
        // the same displayed month: only one of them is suppressed.
        AddBankPosting(db, account, new DateTime(2025, 5, 10), 120m);
        AddBankPosting(db, account, new DateTime(2025, 5, 20), 80m);
        AddSuppression(db, user.Id, favorite.Id, PostingKind.Bank, account.Id, new DateTime(2025, 5, 10));
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(Query(user.Id, analysis, favorite.Id), TestContext.Current.CancellationToken);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);
        Assert.Equal(80m, point.ProjectionAmount);
        var remaining = Assert.Single(point.ProjectionExpectedItems!);
        Assert.Equal(new DateTime(2025, 5, 20), remaining.PriorYearDate);
        Assert.Equal(80m, remaining.Amount);
    }

    /// <summary>
    /// Posting-level suppression distinguishes two prior-year postings on the same account and
    /// date even when their subject is identical: hiding one keeps the other expected.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SameDateSameSubjectPostings_SuppressOnlySelectedPosting()
    {
        using var db = CreateDb();
        var user = await AddUserAsync(db, "sup-same-date");
        var account = AddAccount(db, user.Id, "Giro");
        var favorite = AddFavorite(db, user.Id, "Fav");
        var analysis = new DateTime(2026, 5, 1);
        var priorYearDate = new DateTime(2025, 5, 10);
        var hidden = AddBankPosting(db, account, priorYearDate, 120m, "Zinsen Sparkonto");
        var visible = AddBankPosting(db, account, priorYearDate, 80m, "Zinsen Sparkonto");
        AddSuppression(db, user.Id, favorite.Id, PostingKind.Bank, account.Id, priorYearDate, hidden.Id, 120m);
        await RebuildAggregatesAsync(db, user.Id);

        var result = await CreateSut(db).QueryAsync(Query(user.Id, analysis, favorite.Id), TestContext.Current.CancellationToken);
        var point = result.Points.Single(p => p.GroupKey == $"Account:{account.Id}" && p.PeriodStart == analysis);

        Assert.Equal(80m, point.ProjectionAmount);
        var remaining = Assert.Single(point.ProjectionExpectedItems!);
        Assert.Equal(visible.Id, remaining.PostingId);
        Assert.Equal(80m, remaining.Amount);
        Assert.Equal(priorYearDate, remaining.PriorYearDate);
    }
}
