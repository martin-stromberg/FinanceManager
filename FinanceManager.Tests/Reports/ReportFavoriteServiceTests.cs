using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers <see cref="ReportFavoriteService"/>'s CRUD operations for a user's saved report configurations
/// ("favorites"): per-user unique naming, full round-trip of all configuration fields (including the projection
/// comparison flag), and ownership enforcement for update/delete/get/list.
/// </summary>
public sealed class ReportFavoriteServiceTests
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

    /// <summary>
    /// Creating a favorite must persist it and return a DTO whose id, name, and configuration fields (e.g.
    /// <c>IncludeCategory</c>, <c>Interval</c>) match the request, with the underlying database row reflecting
    /// the same data.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistAndReturnDto()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("user", "pw", false);
        db.Users.Add(user); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = new ReportFavoriteService(db);

        var dto = await svc.CreateAsync(user.Id, new ReportFavoriteCreateRequest("MyFav", PostingKind.Contact, true, ReportInterval.Month, true, false, true, true), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("MyFav", dto.Name);
        Assert.True(dto.IncludeCategory);
        Assert.Equal(ReportInterval.Month, dto.Interval);

        var entity = await db.ReportFavorites.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("MyFav", entity.Name);
    }

    /// <summary>
    /// A favorite name must be unique per user; creating a second favorite with the same name for the same user
    /// must throw <see cref="InvalidOperationException"/> rather than silently creating a duplicate.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldThrow_OnDuplicateNamePerUser()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("user", "pw", false);
        db.Users.Add(user); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = new ReportFavoriteService(db);
        await svc.CreateAsync(user.Id, new ReportFavoriteCreateRequest("Dup", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(user.Id, new ReportFavoriteCreateRequest("Dup", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None));
    }

    /// <summary>
    /// The uniqueness constraint on favorite names is scoped per user - two different users can each create a
    /// favorite with the identical name without conflict.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldAllowSameNameForDifferentUsers()
    {
        using var db = CreateDb();
        var user1 = new FinanceManager.Domain.Users.User("u1", "pw", false);
        var user2 = new FinanceManager.Domain.Users.User("u2", "pw", false);
        db.Users.AddRange(user1, user2); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = new ReportFavoriteService(db);
        await svc.CreateAsync(user1.Id, new ReportFavoriteCreateRequest("Same", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);
        await svc.CreateAsync(user2.Id, new ReportFavoriteCreateRequest("Same", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);
        Assert.Equal(2, await db.ReportFavorites.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Renaming a favorite to a name already used by another of the same user's favorites must be rejected with
    /// <see cref="InvalidOperationException"/> (the same per-user uniqueness rule applies on update, not just
    /// create), while a valid update must persist every changed field (name, posting kind, include-category,
    /// interval, previous/year comparisons, chart visibility, and expandability).
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldModifyFields_AndRejectDuplicate()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("u", "pw", false);
        db.Users.Add(user); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = new ReportFavoriteService(db);
        var a = await svc.CreateAsync(user.Id, new ReportFavoriteCreateRequest("A", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);
        var b = await svc.CreateAsync(user.Id, new ReportFavoriteCreateRequest("B", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);

        // Duplicate rename attempt
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateAsync(a.Id, user.Id, new ReportFavoriteUpdateRequest("B", PostingKind.SavingsPlan, true, ReportInterval.Year, true, true, true, true), CancellationToken.None));

        // Valid update
        var updated = await svc.UpdateAsync(a.Id, user.Id, new ReportFavoriteUpdateRequest("A-Updated", PostingKind.SavingsPlan, true, ReportInterval.Year, true, true, true, true), CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("A-Updated", updated!.Name);
        Assert.Equal(PostingKind.SavingsPlan, updated.PostingKind);
        Assert.True(updated.IncludeCategory);
        Assert.Equal(ReportInterval.Year, updated.Interval);
        Assert.True(updated.ComparePrevious);
        Assert.True(updated.CompareYear);
        Assert.True(updated.ShowChart);
        Assert.True(updated.Expandable);
    }

    /// <summary>
    /// The <c>CompareProjection</c> flag (dividend projection comparison) must round-trip correctly through the
    /// full lifecycle: set to true on create and reflected in the create response, the persisted entity, a
    /// subsequent list, and a subsequent get - then flipped to false via update and again reflected everywhere.
    /// </summary>
    [Fact]
    public async Task CreateListGetAndUpdate_ShouldRoundtripCompareProjection()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("projection-user", "pw", false);
        db.Users.Add(user); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = new ReportFavoriteService(db);

        var created = await svc.CreateAsync(
            user.Id,
            new ReportFavoriteCreateRequest(
                "Projection",
                PostingKind.Security,
                true,
                ReportInterval.Month,
                false,
                false,
                true,
                true,
                true),
            CancellationToken.None);

        Assert.True(created.CompareProjection);
        Assert.True((await db.ReportFavorites.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).CompareProjection);

        var listed = await svc.ListAsync(user.Id, CancellationToken.None);
        Assert.True(listed.Single().CompareProjection);

        var fetched = await svc.GetAsync(created.Id, user.Id, CancellationToken.None);
        Assert.True(fetched!.CompareProjection);

        var updated = await svc.UpdateAsync(
            created.Id,
            user.Id,
            new ReportFavoriteUpdateRequest(
                "Projection",
                PostingKind.Security,
                true,
                ReportInterval.Month,
                false,
                false,
                false,
                true,
                true),
            CancellationToken.None);

        Assert.False(updated!.CompareProjection);
        Assert.False((await db.ReportFavorites.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).CompareProjection);
    }

    /// <summary>
    /// Deleting a favorite must return false (not throw) both when a different user attempts to delete someone
    /// else's favorite and when the given id doesn't exist at all, while the actual owner deleting their own
    /// favorite must succeed and return true.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenNotOwnedOrMissing()
    {
        using var db = CreateDb();
        var user1 = new FinanceManager.Domain.Users.User("u1", "pw", false);
        var user2 = new FinanceManager.Domain.Users.User("u2", "pw", false);
        db.Users.AddRange(user1, user2); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = new ReportFavoriteService(db);
        var fav = await svc.CreateAsync(user1.Id, new ReportFavoriteCreateRequest("Fav", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);
        Assert.False(await svc.DeleteAsync(fav.Id, user2.Id, CancellationToken.None));
        Assert.False(await svc.DeleteAsync(Guid.NewGuid(), user1.Id, CancellationToken.None));
        Assert.True(await svc.DeleteAsync(fav.Id, user1.Id, CancellationToken.None));
    }

    /// <summary>
    /// Listing favorites must return only the calling user's own favorites, ordered alphabetically by name (not
    /// creation order), and <c>GetAsync</c> must return null when the requesting user does not own the favorite
    /// even if the id itself is valid for another user.
    /// </summary>
    [Fact]
    public async Task ListAndGet_ShouldRespectOwnershipAndOrdering()
    {
        using var db = CreateDb();
        var user1 = new FinanceManager.Domain.Users.User("u1", "pw", false);
        var user2 = new FinanceManager.Domain.Users.User("u2", "pw", false);
        db.Users.AddRange(user1, user2); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = new ReportFavoriteService(db);
        await svc.CreateAsync(user1.Id, new ReportFavoriteCreateRequest("Zeta", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);
        await svc.CreateAsync(user1.Id, new ReportFavoriteCreateRequest("Alpha", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);
        await svc.CreateAsync(user2.Id, new ReportFavoriteCreateRequest("Other", PostingKind.Contact, false, ReportInterval.Month, false, false, false, false), CancellationToken.None);

        var list1 = await svc.ListAsync(user1.Id, CancellationToken.None);
        var names = list1.Select(l => l.Name).ToArray();
        Assert.Equal(new[] { "Alpha", "Zeta" }, names); // ordered by name
        Assert.Equal(2, list1.Count);

        var list2 = await svc.ListAsync(user2.Id, CancellationToken.None);
        Assert.Equal(1, list2.Count);
        Assert.All(list2, f => Assert.Equal("Other", f.Name));

        var first = list1.First();
        var fetched = await svc.GetAsync(first.Id, user1.Id, CancellationToken.None);
        Assert.Equal(first.Name, fetched!.Name);
        Assert.Null(await svc.GetAsync(first.Id, user2.Id, CancellationToken.None));
    }
}
