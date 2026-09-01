using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Covers <see cref="ReportAggregationService.QueryAsync"/> when a query spans multiple <see cref="PostingKind"/>
/// values at once (via <c>PostingKinds</c>): the resulting hierarchy of synthetic "Type:{kind}" rows that sum
/// each kind's entities, and how category grouping nests underneath - either Type -> Category -> Entity when
/// categories are included, or Type -> Entity directly when they are not.
/// </summary>
public sealed class ReportAggregationServiceMultiKindTests
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

    private static Contact NewContact(AppDbContext db, Guid ownerId, string name, ContactCategory? cat = null)
    {
        var c = new Contact(ownerId, name, ContactType.Person, cat?.Id, null);
        db.Contacts.Add(c);
        return c;
    }

    private static SavingsPlan NewSavingsPlan(AppDbContext db, Guid ownerId, string name, Guid? categoryId = null)
    {
        var sp = new SavingsPlan(ownerId, name, SavingsPlanType.Recurring, null, null, null, categoryId);
        db.SavingsPlans.Add(sp);
        return sp;
    }

    /// <summary>
    /// With multiple posting kinds and <c>IncludeCategory = true</c>, the result must build a full three-level
    /// hierarchy per kind: a "Type:{kind}" row summing all of that kind's entities, "Category:{kind}:{categoryId}"
    /// rows (including an "_none" pseudo-category for entities without one) parented under the matching Type row,
    /// and entity rows parented under their category row - with amounts correctly summing up each level.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MultiKinds_WithCategories_ShouldCreateTypeCategoryEntityHierarchy()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("owner", "pw", false);
        db.Users.Add(user);
        var contactCat = new ContactCategory(user.Id, "Friends");
        db.ContactCategories.Add(contactCat);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var c = NewContact(db, user.Id, "Alice", contactCat);
        var sp = NewSavingsPlan(db, user.Id, "ETF Plan");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var jan = new DateTime(2025, 1, 1);
        var feb = new DateTime(2025, 2, 1);
        db.PostingAggregates.AddRange(
            new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, jan, AggregatePeriod.Month).WithAdd(10).WithAdd(10),
            new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, feb, AggregatePeriod.Month).WithAdd(10).WithAdd(10),
            new PostingAggregate(PostingKind.SavingsPlan, null, null, sp.Id, null, jan, AggregatePeriod.Month).WithAdd(15).WithAdd(15),
            new PostingAggregate(PostingKind.SavingsPlan, null, null, sp.Id, null, feb, AggregatePeriod.Month).WithAdd(15).WithAdd(15)
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var query = new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Month, 12, IncludeCategory: true, ComparePrevious: false, CompareYear: false, PostingKinds: new[] { PostingKind.Contact, PostingKind.SavingsPlan });
        var result = await sut.QueryAsync(query, CancellationToken.None);

        // Expect type rows for both kinds in latest month (feb)
        var typeContact = result.Points.Single(p => p.GroupKey == $"Type:{PostingKind.Contact}" && p.PeriodStart == feb);
        var typeSavings = result.Points.Single(p => p.GroupKey == $"Type:{PostingKind.SavingsPlan}" && p.PeriodStart == feb);
        Assert.Equal(20m, typeContact.Amount); // category sum of contacts
        Assert.Equal(30m, typeSavings.Amount); // category sum of savings plans (uncategorized)

        // Category nodes exist with parent=Type
        var catContact = result.Points.Single(p => p.GroupKey == $"Category:{PostingKind.Contact}:{contactCat.Id}" && p.PeriodStart == feb);
        Assert.Equal($"Type:{PostingKind.Contact}", catContact.ParentGroupKey);
        var catSavings = result.Points.Single(p => p.GroupKey == $"Category:{PostingKind.SavingsPlan}:_none" && p.PeriodStart == feb);
        Assert.Equal($"Type:{PostingKind.SavingsPlan}", catSavings.ParentGroupKey);

        // Entity nodes exist with parent=Category when includeCategory=true in multi
        var contactEntity = result.Points.Single(p => p.GroupKey.StartsWith("Contact:") && p.PeriodStart == feb);
        Assert.Equal(catContact.GroupKey, contactEntity.ParentGroupKey);
        Assert.Equal(20m, contactEntity.Amount);
        var savingsEntity = result.Points.Single(p => p.GroupKey.StartsWith("SavingsPlan:") && p.PeriodStart == feb);
        Assert.Equal(catSavings.GroupKey, savingsEntity.ParentGroupKey);
        Assert.Equal(30m, savingsEntity.Amount);
    }

    /// <summary>
    /// With multiple posting kinds and <c>IncludeCategory = false</c>, the result must skip the category level
    /// entirely: "Type:{kind}" rows still sum each kind's entities, but entity rows are parented directly under
    /// their Type row and no "Category:" rows appear at all.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MultiKinds_WithoutCategories_ShouldCreateTypeRowsWithEntityChildren()
    {
        using var db = CreateDb();
        var user = new FinanceManager.Domain.Users.User("owner", "pw", false);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var c = NewContact(db, user.Id, "Bob");
        var sp = NewSavingsPlan(db, user.Id, "Depot Plan");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var feb = new DateTime(2025, 2, 1);
        db.PostingAggregates.AddRange(
            new PostingAggregate(PostingKind.Contact, null, c.Id, null, null, feb, AggregatePeriod.Month).WithAdd(12).WithAdd(8), // 20
            new PostingAggregate(PostingKind.SavingsPlan, null, null, sp.Id, null, feb, AggregatePeriod.Month).WithAdd(5).WithAdd(7) // 12
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new ReportAggregationService(db, new NullLogger<ReportAggregationService>());
        var query = new ReportAggregationQuery(user.Id, PostingKind.Contact, ReportInterval.Month, 6, IncludeCategory: false, ComparePrevious: false, CompareYear: false, PostingKinds: new[] { PostingKind.Contact, PostingKind.SavingsPlan });
        var result = await sut.QueryAsync(query, CancellationToken.None);

        // Type rows exist and sum entity amounts
        var typeContact = result.Points.Single(p => p.GroupKey == $"Type:{PostingKind.Contact}" && p.PeriodStart == feb);
        var typeSavings = result.Points.Single(p => p.GroupKey == $"Type:{PostingKind.SavingsPlan}" && p.PeriodStart == feb);
        Assert.Equal(20m, typeContact.Amount);
        Assert.Equal(12m, typeSavings.Amount);

        // No category nodes
        Assert.DoesNotContain(result.Points, p => p.GroupKey.StartsWith("Category:"));

        // Entities parent is Type
        var contactEntity = result.Points.Single(p => p.GroupKey.StartsWith("Contact:") && p.PeriodStart == feb);
        Assert.Equal($"Type:{PostingKind.Contact}", contactEntity.ParentGroupKey);
        var savingsEntity = result.Points.Single(p => p.GroupKey.StartsWith("SavingsPlan:") && p.PeriodStart == feb);
        Assert.Equal($"Type:{PostingKind.SavingsPlan}", savingsEntity.ParentGroupKey);
    }
}

internal static class PostingAggregateTestExtensions
{
    public static PostingAggregate WithAdd(this PostingAggregate agg, decimal amount)
    {
        agg.Add(amount);
        return agg;
    }
}
