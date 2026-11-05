using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of <see cref="McpScopeEvaluator"/>: effective profiles of the access levels,
/// tuple checks, filter semantics (AND over dimensions / OR over rows / unclassified not
/// matched), anchor derivation and the AccountShare role lid.
/// </summary>
public sealed class McpScopeEvaluatorTests
{
    private static AppDbContext NewDb()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return services.BuildServiceProvider().GetRequiredService<AppDbContext>();
    }

    private static McpAccess NewAccess(Guid owner, McpAccessLevel level = McpAccessLevel.Analyse)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", level, McpApprovalMode.PerAction);

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AnalyseProfile_GrantsReadOnEveryDataType()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var access = NewAccess(Guid.NewGuid(), McpAccessLevel.Analyse);

        var scopes = await eval.GetEffectiveScopesAsync(access, CancellationToken.None);
        Assert.Equal(Enum.GetValues<McpDataType>().Length, scopes.Count);
        Assert.All(scopes, s => Assert.Equal(McpRightKind.Read, s.Right));

        Assert.True(await eval.IsAllowedAsync(access, McpDataType.Postings, McpRightKind.Read, CancellationToken.None));
        Assert.False(await eval.IsAllowedAsync(access, McpDataType.Postings, McpRightKind.Delete, CancellationToken.None));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task BuchhalterProfile_GrantsBookOnlyOnDrafts_AndDeleteExceptImportAndTypes()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var access = NewAccess(Guid.NewGuid(), McpAccessLevel.Buchhalter);

        Assert.True(await eval.IsAllowedAsync(access, McpDataType.StatementDrafts, McpRightKind.Book, CancellationToken.None));
        Assert.False(await eval.IsAllowedAsync(access, McpDataType.Postings, McpRightKind.Book, CancellationToken.None));
        Assert.False(await eval.IsAllowedAsync(access, McpDataType.StatementImports, McpRightKind.Delete, CancellationToken.None));
        Assert.True(await eval.IsAllowedAsync(access, McpDataType.Postings, McpRightKind.Delete, CancellationToken.None));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DeactivatedAccess_IsNeverAllowed()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var access = NewAccess(Guid.NewGuid());
        access.Deactivate();
        Assert.False(await eval.IsAllowedAsync(access, McpDataType.Accounts, McpRightKind.Read, CancellationToken.None));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task IndividualLevel_ReadsPersistedScopes_Only()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        db.McpAccesses.Add(access);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.McpAccessScopes.Add(new McpAccessScope(access.Id, owner, McpDataType.Contacts, McpRightKind.Read));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await eval.IsAllowedAsync(access, McpDataType.Contacts, McpRightKind.Read, CancellationToken.None));
        Assert.False(await eval.IsAllowedAsync(access, McpDataType.Accounts, McpRightKind.Read, CancellationToken.None));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_OwnedRecord_StandardLevel_Allows()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Analyse);

        var verdict = await eval.CheckRecordAccessAsync(access, McpDataType.Contacts, McpRightKind.Read,
            new McpRecordContext { OwnerUserId = owner }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Allowed, verdict);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_ForeignRecord_IsInvisible_NotFound()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var access = NewAccess(Guid.NewGuid(), McpAccessLevel.Buchhalter);

        var verdict = await eval.CheckRecordAccessAsync(access, McpDataType.Contacts, McpRightKind.Read,
            new McpRecordContext { OwnerUserId = Guid.NewGuid() }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.NotFound, verdict);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_AccountAnchor_OwnedAccount_Allows()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Analyse);
        var account = new Account(owner, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var verdict = await eval.CheckRecordAccessAsync(access, McpDataType.StatementImports, McpRightKind.Read,
            new McpRecordContext { AccountId = account.Id }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Allowed, verdict);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_AccountAnchor_SharedRead_BlocksWrite()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var reader = Guid.NewGuid();
        var foreignOwner = Guid.NewGuid();
        var account = new Account(foreignOwner, AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(account);
        db.AccountShares.Add(new AccountShare(account.Id, reader, AccountShareRole.Read));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var access = NewAccess(reader, McpAccessLevel.Buchhalter);

        // Read on account-bound data is allowed through the share…
        var read = await eval.CheckRecordAccessAsync(access, McpDataType.Postings, McpRightKind.Read,
            new McpRecordContext { AccountId = account.Id }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Allowed, read);

        // …but the Read share caps every write attempt (lid is absolute).
        var write = await eval.CheckRecordAccessAsync(access, McpDataType.Contacts, McpRightKind.Modify,
            new McpRecordContext { AccountId = account.Id, OwnerUserId = reader }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Denied, write);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_AccountAnchor_SharedWrite_AllowsUpToBook()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var writer = Guid.NewGuid();
        var account = new Account(Guid.NewGuid(), AccountType.Giro, "Giro", null, Guid.NewGuid());
        db.Accounts.Add(account);
        db.AccountShares.Add(new AccountShare(account.Id, writer, AccountShareRole.Write));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var access = NewAccess(writer, McpAccessLevel.Buchhalter);

        var book = await eval.CheckRecordAccessAsync(access, McpDataType.StatementDrafts, McpRightKind.Book,
            new McpRecordContext { AccountId = account.Id }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Allowed, book);

        var del = await eval.CheckRecordAccessAsync(access, McpDataType.Postings, McpRightKind.Delete,
            new McpRecordContext { AccountId = account.Id }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Denied, del);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_Filters_AndOverDimensions_OrOverRows_UnclassifiedNotMatched()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var owner = Guid.NewGuid();
        var catA = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        db.McpAccesses.Add(access);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        // Row 1: ContactCategories = {catA} → matches only contacts classified catA.
        // Row 2: no filters → matches everything → demonstrates OR over rows.
        var rowFiltered = new McpAccessScope(access.Id, owner, McpDataType.Contacts, McpRightKind.Read,
            contactCategoryIdsCsv: catA.ToString());
        db.McpAccessScopes.Add(rowFiltered);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Unclassified record is never matched by the whitelist row.
        var unclassified = await eval.CheckRecordAccessAsync(access, McpDataType.Contacts, McpRightKind.Read,
            new McpRecordContext { OwnerUserId = owner, ContactCategoryId = null }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Denied, unclassified);

        var classified = await eval.CheckRecordAccessAsync(access, McpDataType.Contacts, McpRightKind.Read,
            new McpRecordContext { OwnerUserId = owner, ContactCategoryId = catA }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Allowed, classified);

        // OR over rows: adding an unfiltered row makes any owned record pass.
        db.McpAccessScopes.Add(new McpAccessScope(access.Id, owner, McpDataType.Contacts, McpRightKind.Read));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var viaSecondRow = await eval.CheckRecordAccessAsync(access, McpDataType.Contacts, McpRightKind.Read,
            new McpRecordContext { OwnerUserId = owner }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Allowed, viaSecondRow);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_StatementImportAnchor_ResolvesViaAccount()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var owner = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var account = new Account(owner, AccountType.Giro, "Giro", null, Guid.NewGuid());
        var import = new FinanceManager.Domain.Statements.StatementImport(account.Id, FinanceManager.Shared.Dtos.Statements.ImportFormat.Csv, "f.csv");
        db.Accounts.Add(account);
        db.StatementImports.Add(import);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Owner access resolves the import through the account chain.
        var own = await eval.CheckRecordAccessAsync(NewAccess(owner), McpDataType.StatementImports, McpRightKind.Read,
            new McpRecordContext { StatementImportId = import.Id }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.Allowed, own);

        // Foreign access sees nothing (NotFound, no existence leak).
        var foreignCheck = await eval.CheckRecordAccessAsync(NewAccess(foreign), McpDataType.StatementImports, McpRightKind.Read,
            new McpRecordContext { StatementImportId = import.Id }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.NotFound, foreignCheck);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task CheckRecord_MissingAnchor_IsNotFound()
    {
        var db = NewDb();
        var eval = new McpScopeEvaluator(db);
        var access = NewAccess(Guid.NewGuid());
        var verdict = await eval.CheckRecordAccessAsync(access, McpDataType.Contacts, McpRightKind.Read,
            new McpRecordContext { ContactId = Guid.NewGuid() }, CancellationToken.None);
        Assert.Equal(McpAccessVerdict.NotFound, verdict);
    }
}
