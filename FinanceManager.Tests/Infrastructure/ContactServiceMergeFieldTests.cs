using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Contacts;
using FinanceManager.Domain.Contacts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Threading;
using FinanceManager.Shared.Dtos.Contacts;

namespace FinanceManager.Tests.Infrastructure;

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

    [Fact]
    public async Task Merge_DestinationFirst_TargetKeepsValues_WhenBothHave()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var srcCatEntity = new ContactCategory(owner, "SrcCat");
        var tgtCatEntity = new ContactCategory(owner, "TgtCat");
        db.ContactCategories.AddRange(srcCatEntity, tgtCatEntity);
        await db.SaveChangesAsync();
        var src = new FinanceManager.Domain.Contacts.Contact(owner, "SourceName", ContactType.Person, srcCatEntity.Id, "SourceDesc", true);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "TargetName", ContactType.Person, tgtCatEntity.Id, "TargetDesc", true);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync();

        // set different symbol attachments
        var srcSym = Guid.NewGuid();
        var tgtSym = Guid.NewGuid();
        src.SetSymbolAttachment(srcSym);
        tgt.SetSymbolAttachment(tgtSym);
        await db.SaveChangesAsync();

        var svc = new ContactService(db);
        var res = await svc.MergeAsync(owner, src.Id, tgt.Id, CancellationToken.None, MergePreference.DestinationFirst);

        Assert.Equal(tgt.Id, res.Id);
        Assert.Equal("TargetName", res.Name);
        Assert.Equal(tgt.CategoryId, res.CategoryId);
        Assert.Equal("TargetDesc", res.Description);
        Assert.True(res.IsPaymentIntermediary);
        Assert.Equal(tgtSym, res.SymbolAttachmentId);
    }

    [Fact]
    public async Task Merge_DestinationFirst_AdoptsSource_WhenTargetMissingValues()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var srcCatEntity = new ContactCategory(owner, "SrcCat");
        db.ContactCategories.Add(srcCatEntity);
        await db.SaveChangesAsync();
        var src = new FinanceManager.Domain.Contacts.Contact(owner, "SourceName", ContactType.Person, srcCatEntity.Id, "SourceDesc", true);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "TargetName", ContactType.Person, null, null, false);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync();

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

    [Fact]
    public async Task Merge_SourceFirst_OverwritesTarget_WithSourceValues()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var srcCatEntity = new ContactCategory(owner, "SrcCat");
        var tgtCatEntity = new ContactCategory(owner, "TgtCat");
        db.ContactCategories.AddRange(srcCatEntity, tgtCatEntity);
        await db.SaveChangesAsync();

        var src = new FinanceManager.Domain.Contacts.Contact(owner, "SourceName", ContactType.Person, srcCatEntity.Id, "SourceDesc", false);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "TargetName", ContactType.Person, tgtCatEntity.Id, "TargetDesc", true);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync();

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

    [Fact]
    public async Task Merge_SymbolHandling_SourceFirst_ReplacesSymbol_TargetKept_WhenDestinationFirst()
    {
        using var db = CreateSqliteContext();
        var owner = Guid.NewGuid();

        var src = new FinanceManager.Domain.Contacts.Contact(owner, "S", ContactType.Person, null, null, false);
        var tgt = new FinanceManager.Domain.Contacts.Contact(owner, "T", ContactType.Person, null, null, false);
        db.Contacts.AddRange(src, tgt);
        await db.SaveChangesAsync();

        var srcSym = Guid.NewGuid();
        var tgtSym = Guid.NewGuid();
        src.SetSymbolAttachment(srcSym);
        tgt.SetSymbolAttachment(tgtSym);
        await db.SaveChangesAsync();

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
        await db2.SaveChangesAsync();
        s2.SetSymbolAttachment(srcSym);
        t2.SetSymbolAttachment(tgtSym);
        await db2.SaveChangesAsync();
        var svc2 = new ContactService(db2);
        var res2 = await svc2.MergeAsync(owner2, s2.Id, t2.Id, CancellationToken.None, MergePreference.DestinationFirst);
        Assert.Equal(tgtSym, res2.SymbolAttachmentId);
    }
}
