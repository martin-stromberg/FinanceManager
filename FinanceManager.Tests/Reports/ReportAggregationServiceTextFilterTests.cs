using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Savings;
using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Reports;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Free-text filters in <see cref="ReportAggregationService"/>: kinds with configured text filters are
/// aggregated from raw postings (<see cref="Posting.Subject"/> + <see cref="Posting.Description"/>,
/// case-insensitive substring, OR between entries) while the remaining kinds keep the precomputed
/// aggregate path. Also covers owner restriction, valuta date, amount-zero skipping, linked-account
/// savings-plan sources and the security dividend-net special path.
/// </summary>
public sealed class ReportAggregationServiceTextFilterTests
{
    private static (ReportAggregationService sut, AppDbContext db, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (new ReportAggregationService(db, NullLogger<ReportAggregationService>.Instance), db, conn);
    }

    private static Posting AddPosting(AppDbContext db, PostingKind kind, Guid? accountId, Guid? contactId, Guid? savingsPlanId, Guid? securityId,
        DateTime bookingDate, decimal amount, string? subject, string? description,
        DateTime? valutaDate = null, SecurityPostingSubType? subType = null)
    {
        var p = new Posting(Guid.NewGuid(), kind, accountId, contactId, savingsPlanId, securityId,
            bookingDate, valutaDate ?? bookingDate, amount, subject, null, description, subType, null);
        db.Postings.Add(p);
        return p;
    }

    private static async Task<(Account acc, Contact bankContact)> SeedAccountAsync(AppDbContext db, Guid owner)
    {
        var bankContact = new Contact(owner, "Bank", ContactType.Bank, null, null);
        var acc = new Account(owner, AccountType.Giro, "Giro", null, bankContact.Id);
        db.Contacts.Add(bankContact);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (acc, bankContact);
    }

    private static readonly DateTime Month1 = new(2025, 1, 1);
    private static readonly DateTime Month2 = new(2025, 2, 1);

    /// <summary>
    /// A bank text filter matches case-insensitively against the posting subject; postings whose
    /// subject and description do not contain the text are excluded from the aggregation.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankTextFilter_MatchesSubject_CaseInsensitive()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (acc, _) = await SeedAccountAsync(db, owner);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(4), -50m, "TANKSTELLE Aral", null);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(5), 700m, "Gehalt", null);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Bank, ReportInterval.Month, 6, false, false, false,
            AnalysisDate: Month1, Filters: new ReportAggregationFilters(BankTextFilters: new[] { "tankstelle" })),
            CancellationToken.None);

        result.Points.Should().Contain(p => p.GroupKey == $"Account:{acc.Id}" && p.PeriodStart == Month1 && p.Amount == -50m);
        result.Points.Where(p => p.GroupKey == $"Account:{acc.Id}").Sum(p => p.Amount).Should().Be(-50m);
        conn.Dispose();
    }

    /// <summary>
    /// The filter also inspects the posting description, and multiple entries are OR-linked:
    /// a posting is kept when any entry matches subject or description.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankTextFilter_MatchesDescription_AndOrSemantics()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (acc, _) = await SeedAccountAsync(db, owner);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(4), -30m, "Abbuchung", "Miete Januar");
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(5), -20m, "Abbuchung", "Strom");
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(6), 999m, "Sonstiges", "Unrelated");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Bank, ReportInterval.Month, 6, false, false, false,
            AnalysisDate: Month1, Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Miete", "Strom" })),
            CancellationToken.None);

        result.Points.Where(p => p.GroupKey == $"Account:{acc.Id}").Sum(p => p.Amount).Should().Be(-50m);
        conn.Dispose();
    }

    /// <summary>
    /// Postings with amount zero are skipped in the raw posting path, matching the precomputed
    /// aggregate semantics.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankTextFilter_SkipsZeroAmountPostings()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (acc, _) = await SeedAccountAsync(db, owner);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(4), 0m, "Miete", null);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(5), -40m, "Miete", null);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Bank, ReportInterval.Month, 6, false, false, false,
            AnalysisDate: Month1, Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Miete" })),
            CancellationToken.None);

        result.Points.Where(p => p.GroupKey == $"Account:{acc.Id}").Sum(p => p.Amount).Should().Be(-40m);
        conn.Dispose();
    }

    /// <summary>
    /// With <c>UseValutaDate</c> the raw posting path buckets by valuta date instead of booking date.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankTextFilter_UseValutaDate_BucketsByValutaDate()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (acc, _) = await SeedAccountAsync(db, owner);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(28), -60m, "Miete", null, valutaDate: Month2.AddDays(2));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Bank, ReportInterval.Month, 6, false, false, false,
            UseValutaDate: true, AnalysisDate: Month2,
            Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Miete" })),
            CancellationToken.None);

        result.Points.Should().Contain(p => p.GroupKey == $"Account:{acc.Id}" && p.PeriodStart == Month2 && p.Amount == -60m);
        result.Points.Should().NotContain(p => p.GroupKey == $"Account:{acc.Id}" && p.PeriodStart == Month1);
        conn.Dispose();
    }

    /// <summary>
    /// With <c>UseValutaDate</c> a posting whose valuta date is unset (<see cref="DateTime.MinValue"/>)
    /// falls back to its booking date for the period bucket while a posting with a real valuta
    /// date is still bucketed by that valuta date.
    /// </summary>
    [Fact]
    public async Task QueryAsync_TextFilter_UseValutaDate_UsesValutaWithFallback()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (acc, _) = await SeedAccountAsync(db, owner);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(28), -60m, "Miete", null, valutaDate: Month2.AddDays(2));
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(4), -40m, "Miete", null, valutaDate: DateTime.MinValue);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Bank, ReportInterval.Month, 6, false, false, false,
            UseValutaDate: true, AnalysisDate: Month2,
            Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Miete" })),
            CancellationToken.None);

        // Real valuta date wins -> Month2 bucket; MinValue falls back to booking date -> Month1 bucket.
        result.Points.Should().Contain(p => p.GroupKey == $"Account:{acc.Id}" && p.PeriodStart == Month2 && p.Amount == -60m);
        result.Points.Should().Contain(p => p.GroupKey == $"Account:{acc.Id}" && p.PeriodStart == Month1 && p.Amount == -40m);
        conn.Dispose();
    }

    /// <summary>
    /// Text filters and the category filter of <c>IncludeCategory</c> mode apply cumulatively:
    /// the category filter restricts the entities and the text filter restricts the postings,
    /// so only the matching posting of a category member contributes to entity and category points.
    /// </summary>
    [Fact]
    public async Task QueryAsync_TextFilter_WithIncludeCategory_AppliesCumulatively()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var catA = new ContactCategory(owner, "Kat A");
        var catB = new ContactCategory(owner, "Kat B");
        var contactA = new Contact(owner, "CA", ContactType.Person, catA.Id);
        var contactB = new Contact(owner, "CB", ContactType.Person, catB.Id);
        db.ContactCategories.AddRange(catA, catB);
        db.Contacts.AddRange(contactA, contactB);
        AddPosting(db, PostingKind.Contact, null, contactA.Id, null, null, Month1.AddDays(4), 100m, "Miete", null);
        AddPosting(db, PostingKind.Contact, null, contactA.Id, null, null, Month1.AddDays(5), 40m, "Sonstiges", null);
        AddPosting(db, PostingKind.Contact, null, contactB.Id, null, null, Month1.AddDays(6), 200m, "Miete", null);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Contact, ReportInterval.Month, 6, true, false, false,
            AnalysisDate: Month1,
            Filters: new ReportAggregationFilters(
                ContactCategoryIds: new[] { catA.Id },
                ContactTextFilters: new[] { "Miete" })),
            CancellationToken.None);

        // Only contactA (category member) with its matching "Miete" posting survives.
        result.Points.Should().Contain(p => p.GroupKey == $"Contact:{contactA.Id}" && p.PeriodStart == Month1 && p.Amount == 100m);
        result.Points.Where(p => p.GroupKey == $"Contact:{contactA.Id}").Sum(p => p.Amount).Should().Be(100m);
        result.Points.Should().NotContain(p => p.GroupKey == $"Contact:{contactB.Id}");
        result.Points.Should().Contain(p => p.GroupKey == $"Category:Contact:{catA.Id}" && p.Amount == 100m && p.CategoryName == "Kat A");
        conn.Dispose();
    }

    /// <summary>
    /// Kinds without text filters still use the precomputed aggregate path while the text-filtered
    /// kind uses raw postings; both contribute points to a multi-kind result.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MixedKinds_RawPathAndAggregatePathCoexist()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var (acc, _) = await SeedAccountAsync(db, owner);
        var contact = new Contact(owner, "C1", ContactType.Person, null, null);
        db.Contacts.Add(contact);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(4), -50m, "Miete", null);
        var agg = new PostingAggregate(PostingKind.Contact, null, contact.Id, null, null, Month1, AggregatePeriod.Month);
        agg.Add(10m);
        db.PostingAggregates.Add(agg);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Bank, ReportInterval.Month, 6, false, false, false,
            PostingKinds: new[] { PostingKind.Bank, PostingKind.Contact }, AnalysisDate: Month1,
            Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Miete" })),
            CancellationToken.None);

        result.Points.Should().Contain(p => p.GroupKey == $"Account:{acc.Id}" && p.Amount == -50m);
        result.Points.Should().Contain(p => p.GroupKey == $"Contact:{contact.Id}" && p.Amount == 10m);
        conn.Dispose();
    }

    /// <summary>
    /// Raw postings of entities owned by another user are never aggregated even if they match the
    /// text filter.
    /// </summary>
    [Fact]
    public async Task QueryAsync_BankTextFilter_ExcludesForeignOwnerPostings()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var (acc, _) = await SeedAccountAsync(db, owner);
        var foreignBank = new Contact(foreign, "FB", ContactType.Bank, null, null);
        var foreignAcc = new Account(foreign, AccountType.Giro, "Fremd", null, foreignBank.Id);
        db.Contacts.Add(foreignBank);
        db.Accounts.Add(foreignAcc);
        AddPosting(db, PostingKind.Bank, acc.Id, null, null, null, Month1.AddDays(4), -50m, "Miete", null);
        AddPosting(db, PostingKind.Bank, foreignAcc.Id, null, null, null, Month1.AddDays(4), -999m, "Miete", null);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Bank, ReportInterval.Month, 6, false, false, false,
            AnalysisDate: Month1, Filters: new ReportAggregationFilters(BankTextFilters: new[] { "Miete" })),
            CancellationToken.None);

        result.Points.Should().NotContain(p => p.GroupKey == $"Account:{foreignAcc.Id}");
        result.Points.Where(p => p.GroupKey == $"Account:{acc.Id}").Sum(p => p.Amount).Should().Be(-50m);
        conn.Dispose();
    }

    /// <summary>
    /// With <see cref="SavingsPlanPostingSource.LinkedAccountPostings"/> plus savings-plan text
    /// filters the savings plan rows are derived from the text-filtered bank postings of the
    /// linked accounts.
    /// </summary>
    [Fact]
    public async Task QueryAsync_SavingsPlanTextFilter_LinkedSource_FiltersLinkedBankPostings()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var bankContact = new Contact(owner, "Bank", ContactType.Bank, null, null);
        var contract = new Account(owner, AccountType.Savings, "Vertragskonto", null, bankContact.Id);
        var linked = new SavingsPlan(owner, "Verknüpft", SavingsPlanType.Open, null, null, null);
        db.Contacts.Add(bankContact);
        db.Accounts.Add(contract);
        db.SavingsPlans.Add(linked);
        db.SavingsPlanAccountLinks.Add(new SavingsPlanAccountLink(linked.Id, contract.Id, owner));
        AddPosting(db, PostingKind.Bank, contract.Id, null, null, null, Month1.AddDays(4), 100m, "Spareinlage", null);
        AddPosting(db, PostingKind.Bank, contract.Id, null, null, null, Month1.AddDays(5), 555m, "Sonstiges", null);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.SavingsPlan, ReportInterval.Month, 6, false, false, false,
            AnalysisDate: Month1, SavingsPlanPostingSource: SavingsPlanPostingSource.LinkedAccountPostings,
            Filters: new ReportAggregationFilters(SavingsPlanTextFilters: new[] { "Spareinlage" })),
            CancellationToken.None);

        result.Points.Should().Contain(p => p.GroupKey == $"SavingsPlan:{linked.Id}" && p.PeriodStart == Month1 && p.Amount == 100m);
        result.Points.Where(p => p.GroupKey == $"SavingsPlan:{linked.Id}").Sum(p => p.Amount).Should().Be(100m);
        conn.Dispose();
    }

    /// <summary>
    /// In the security dividend-net path, security text filters are applied to the dividend anchor
    /// postings: only groups whose anchor matches contribute their net amount (dividend + fee/tax).
    /// </summary>
    [Fact]
    public async Task QueryAsync_SecurityDividendNet_TextFilter_FiltersDividendAnchors()
    {
        var (sut, db, conn) = Create();
        var owner = Guid.NewGuid();
        var sec = new Security(owner, "ETF", "ETF001", null, null, "EUR", null);
        db.Securities.Add(sec);

        var groupMatch = Guid.NewGuid();
        var groupOther = Guid.NewGuid();
        var divMatch = AddPosting(db, PostingKind.Security, null, null, null, sec.Id, Month1.AddDays(3), 100m,
            "Ausschüttung ETF", null, subType: SecurityPostingSubType.Dividend);
        var feeMatch = AddPosting(db, PostingKind.Security, null, null, null, sec.Id, Month1.AddDays(3), -5m,
            "Gebühr", null, subType: SecurityPostingSubType.Fee);
        var divOther = AddPosting(db, PostingKind.Security, null, null, null, sec.Id, Month1.AddDays(4), 200m,
            "Dividende Sonstiges", null, subType: SecurityPostingSubType.Dividend);
        divMatch.SetGroup(groupMatch);
        feeMatch.SetGroup(groupMatch);
        divOther.SetGroup(groupOther);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sut.QueryAsync(new ReportAggregationQuery(owner, PostingKind.Security, ReportInterval.Month, 6, false, false, false,
            AnalysisDate: Month1,
            Filters: new ReportAggregationFilters(IncludeDividendRelated: true, SecurityTextFilters: new[] { "Ausschüttung" })),
            CancellationToken.None);

        // Only the matching group's net (100 dividend - 5 fee) contributes.
        result.Points.Where(p => p.GroupKey == $"Security:{sec.Id}").Sum(p => p.Amount).Should().Be(95m);
        conn.Dispose();
    }
}
