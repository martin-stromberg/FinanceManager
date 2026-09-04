using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Contacts;
using FinanceManager.Domain.Contacts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Threading;
using FinanceManager.Shared.Dtos.Contacts;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Guards the field-level conflict-resolution rules applied by <see cref="ContactService.MergeAsync"/> when
/// two contacts hold different values for the same scalar field (Name, CategoryId, Description,
/// IsPaymentIntermediary, SymbolAttachmentId). The service supports two opposite policies -
/// <see cref="MergePreference.DestinationFirst"/> (keep the target's data, only fill gaps from the source)
/// and <see cref="MergePreference.SourceFirst"/> (force the source's data onto the target) - and these tests
/// pin down exactly which side wins per field under each policy so a future refactor of the merge logic
/// cannot silently flip a precedence rule.
/// </summary>
public sealed class ContactServiceMergeFieldTests
{
    private static AppDbContext CreateSqliteContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>
    /// Verifies that under <see cref="MergePreference.DestinationFirst"/>, when both the source and the
    /// target already have a value for every mergeable field (name, category, description, payment
    /// intermediary flag, symbol attachment), the target's own values are preserved and nothing is
    /// overwritten by the source - "destination first" must mean the target's existing data is never
    /// clobbered just because it is being merged into.
    /// </summary>
    [Fact]
    public async Task Merge_DestinationFirst_TargetKeepsValues_WhenBothHave()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var srcCatEntity = new ContactCategory(owner, "SrcCat");
        var tgtCatEntity = new ContactCategory(owner, "TgtCat");
        db.ContactCategories.AddRange(srcCatEntity, tgtCatEntity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var src = new FinanceManager.Domain.Contacts.Contact(owner, "SourceName", ContactType.Person, srcCatEntity.Id, "SourceDesc", true);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "TargetName", ContactType.Person, tgtCatEntity.Id, "TargetDesc", true);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // set different symbol attachments
        var srcSym = Guid.NewGuid();
        var tgtSym = Guid.NewGuid();
        src.SetSymbolAttachment(srcSym);
        tgt.SetSymbolAttachment(tgtSym);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new ContactService(db);
        var res = await svc.MergeAsync(owner, src.Id, tgt.Id, CancellationToken.None, MergePreference.DestinationFirst);

        Assert.Equal(tgt.Id, res.Id);
        Assert.Equal("TargetName", res.Name);
        Assert.Equal(tgt.CategoryId, res.CategoryId);
        Assert.Equal("TargetDesc", res.Description);
        Assert.True(res.IsPaymentIntermediary);
        Assert.Equal(tgtSym, res.SymbolAttachmentId);
    }

    /// <summary>
    /// Verifies the gap-filling half of <see cref="MergePreference.DestinationFirst"/>: when the target is
    /// missing category and description, those fields are adopted from the source instead of staying blank,
    /// while a name the target already has is left untouched. It also checks that the boolean
    /// IsPaymentIntermediary flag is treated as an OR rather than a plain "keep target" rule - if either
    /// side is a payment intermediary, the merged contact must be too, since losing that flag silently
    /// would misclassify future postings.
    /// </summary>
    [Fact]
    public async Task Merge_DestinationFirst_AdoptsSource_WhenTargetMissingValues()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var srcCatEntity = new ContactCategory(owner, "SrcCat");
        db.ContactCategories.Add(srcCatEntity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var src = new FinanceManager.Domain.Contacts.Contact(owner, "SourceName", ContactType.Person, srcCatEntity.Id, "SourceDesc", true);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "TargetName", ContactType.Person, null, null, false);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new ContactService(db);
        var res = await svc.MergeAsync(owner, src.Id, tgt.Id, CancellationToken.None, MergePreference.DestinationFirst);

        Assert.Equal(tgt.Id, res.Id);
        // Name: target had a name so kept
        Assert.Equal("TargetName", res.Name);
        // Category and description should be adopted from source
        Assert.Equal(srcCatEntity.Id, res.CategoryId);
        Assert.Equal("SourceDesc", res.Description);
        // IsPaymentIntermediary: target false, source true -> should become true
        Assert.True(res.IsPaymentIntermediary);
    }

    /// <summary>
    /// Verifies that under <see cref="MergePreference.SourceFirst"/> the source's values overwrite the
    /// target's for every field, even fields where the target already had a non-empty value (name,
    /// category, description) and even where the boolean IsPaymentIntermediary flag would otherwise only be
    /// "upgraded" to true - here the source is false, so the merged contact must end up false too. This is
    /// the deliberate opposite of DestinationFirst and confirms callers can choose "the source record is
    /// authoritative" as an explicit merge strategy.
    /// </summary>
    [Fact]
    public async Task Merge_SourceFirst_OverwritesTarget_WithSourceValues()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var srcCatEntity = new ContactCategory(owner, "SrcCat");
        var tgtCatEntity = new ContactCategory(owner, "TgtCat");
        db.ContactCategories.AddRange(srcCatEntity, tgtCatEntity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var src = new FinanceManager.Domain.Contacts.Contact(owner, "SourceName", ContactType.Person, srcCatEntity.Id, "SourceDesc", false);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "TargetName", ContactType.Person, tgtCatEntity.Id, "TargetDesc", true);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new ContactService(db);
        var res = await svc.MergeAsync(owner, src.Id, tgt.Id, CancellationToken.None, MergePreference.SourceFirst);

        Assert.Equal(tgt.Id, res.Id);
        // Name overwritten by source
        Assert.Equal("SourceName", res.Name);
        Assert.Equal(srcCatEntity.Id, res.CategoryId);
        Assert.Equal("SourceDesc", res.Description);
        // IsPaymentIntermediary should follow source (false)
        Assert.False(res.IsPaymentIntermediary);
    }

    /// <summary>
    /// Verifies symbol-attachment precedence specifically, across both merge preferences within a single
    /// test: with <see cref="MergePreference.SourceFirst"/> the target's symbol attachment is replaced by
    /// the source's, while with <see cref="MergePreference.DestinationFirst"/> the target keeps its own
    /// symbol attachment even though the source also has one set. Symbol attachments are tested separately
    /// from the other scalar fields because they are looked up by id rather than compared by value equality,
    /// so a reference-handling bug here would not be caught by the generic field-merge tests.
    /// </summary>
    [Fact]
    public async Task Merge_SymbolHandling_SourceFirst_ReplacesSymbol_TargetKept_WhenDestinationFirst()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var src = new FinanceManager.Domain.Contacts.Contact(owner, "S", ContactType.Person, null, null, false);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "T", ContactType.Person, null, null, false);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var srcSym = Guid.NewGuid();
        var tgtSym = Guid.NewGuid();
        src.SetSymbolAttachment(srcSym);
        tgt.SetSymbolAttachment(tgtSym);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new ContactService(db);

        // SourceFirst: target should get source symbol
        var res1 = await svc.MergeAsync(owner, src.Id, tgt.Id, CancellationToken.None, MergePreference.SourceFirst);
        Assert.Equal(srcSym, res1.SymbolAttachmentId);

        // prepare fresh pair for DestinationFirst test
        using var db2 = CreateSqliteContext();
        var owner2 = Guid.NewGuid();
        var s2 = new FinanceManager.Domain.Contacts.Contact(owner2, "S2", ContactType.Person, null, null, false);
        var t2 = new FinanceManager.Domain.Contacts.Contact(owner2, "T2", ContactType.Person, null, null, false);
        db2.Contacts.AddRange(s2, t2);
        await db2.SaveChangesAsync(TestContext.Current.CancellationToken);
        s2.SetSymbolAttachment(srcSym);
        t2.SetSymbolAttachment(tgtSym);
        await db2.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc2 = new ContactService(db2);
        var res2 = await svc2.MergeAsync(owner2, s2.Id, t2.Id, CancellationToken.None, MergePreference.DestinationFirst);
        Assert.Equal(tgtSym, res2.SymbolAttachmentId);
    }
}
