using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of <see cref="McpAccessService"/>: creation with one-time plaintext token,
/// rotation with immediate invalidation, revocation, status switches, scope validation and
/// strict owner isolation.
/// </summary>
public sealed class McpAccessServiceTests
{
    private static (McpAccessService svc, AppDbContext db, McpTokenIssuer issuer) Create()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        var options = new TestOptionsMonitor(new McpOptions { TokenHash = new McpTokenHashOptions { Iterations = 1000 } });
        var issuer = new McpTokenIssuer(new McpTokenHasher(options));
        var evaluator = new McpScopeEvaluator(db);
        var svc = new McpAccessService(db, issuer, evaluator,
            new NullMcpAuditWriter(NullLogger<NullMcpAuditWriter>.Instance),
            Mock.Of<IMcpApprovalService>(),
            NullLogger<McpAccessService>.Instance);
        return (svc, db, issuer);
    }

    /// <summary>
    /// Variant with the real hash-chained <see cref="McpAuditWriter"/> so the before/after
    /// deltas of access events can be asserted on the persisted audit rows.
    /// </summary>
    private static (McpAccessService svc, AppDbContext db, McpAuditWriter audit) CreateWithAudit()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        var options = new TestOptionsMonitor(new McpOptions { TokenHash = new McpTokenHashOptions { Iterations = 1000 } });
        var issuer = new McpTokenIssuer(new McpTokenHasher(options));
        var evaluator = new McpScopeEvaluator(db);
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var svc = new McpAccessService(db, issuer, evaluator, audit,
            Mock.Of<IMcpApprovalService>(),
            NullLogger<McpAccessService>.Instance);
        return (svc, db, audit);
    }

    /// <summary>
    /// Variant with the real <see cref="McpApprovalService"/> queue so the withdraw coupling
    /// of deactivate/revoke/scope-changes can be asserted end to end (F-0904,
    /// approval-konzept.md §3.3/§6.1/§6.3).
    /// </summary>
    private static (McpAccessService svc, AppDbContext db) CreateWithApprovals(Mock<IMcpToolRegistry>? registry = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        var options = new TestOptionsMonitor(new McpOptions { TokenHash = new McpTokenHashOptions { Iterations = 1000 } });
        var issuer = new McpTokenIssuer(new McpTokenHasher(options));
        var evaluator = new McpScopeEvaluator(db);
        var audit = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var notifications = new Mock<INotificationWriter>();
        var approvals = new McpApprovalService(db, audit, notifications.Object,
            (registry ?? new Mock<IMcpToolRegistry>()).Object, evaluator, options,
            NullLogger<McpApprovalService>.Instance);
        var svc = new McpAccessService(db, issuer, evaluator, audit, approvals,
            NullLogger<McpAccessService>.Instance);
        return (svc, db);
    }

    private static McpAccessCreateRequest NewRequest(short level = (short)McpAccessLevel.Analyse, IReadOnlyList<McpAccessScopeDto>? scopes = null)
        => new("client-a", "note", level, (short)McpApprovalMode.PerAction, null, null, scopes);

    private static McpAccessScopeDto ScopeDto(McpDataType dataType, McpRightKind right,
        string? accountTypes = null, string? contactCategories = null, string? securityCategories = null,
        string? savingsPlanCategories = null, string? attachmentCategories = null)
        => new(Guid.Empty, (short)dataType, (short)right,
            accountTypes, contactCategories, securityCategories, savingsPlanCategories, attachmentCategories);

    /// <summary>Grants <paramref name="userId"/> membership of the <c>Admin</c> identity role —
    /// the same source the fm_admin_* tool gate resolves via <c>IUserAdminService</c>.</summary>
    private static async Task SeedAdminRoleAsync(AppDbContext db, Guid userId)
    {
        var role = new IdentityRole<Guid> { Name = "Admin", NormalizedName = "ADMIN" };
        db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = role.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<McpAuditEntry> LatestAccessEventAsync(AppDbContext db, string eventType)
        => await db.McpAuditEntries.AsNoTracking()
            .Where(e => e.EventType == eventType)
            .OrderByDescending(e => e.SequenceNo)
            .FirstAsync();

    private static async Task<List<McpAuditEntryChange>> ChangesOfAsync(AppDbContext db, Guid entryId)
        => await db.McpAuditEntryChanges.AsNoTracking().Where(c => c.EntryId == entryId).ToListAsync();

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Create_ReturnsPlaintextToken_OnlyOnce_AndStoresOnlyHash()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);

        Assert.StartsWith("fm_mcp_", created.PlainTextToken);
        var entity = await db.McpAccesses.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(created.PlainTextToken, entity.TokenHash);
        Assert.DoesNotContain(created.PlainTextToken, entity.AccessKeyHint);
        Assert.Equal(McpAccessStatus.Active, entity.Status);
        Assert.Empty(await db.McpAccesses.Where(a => a.OwnerUserId != owner).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Create_DuplicateName_ThrowsNameInUse()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.CreateAsync(owner, NewRequest(), CancellationToken.None));
        Assert.Equal("Err_Mcp_NameInUse", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Create_InvalidScopeTuple_ThrowsInvalidScope()
    {
        var (svc, _, _) = Create();
        var scopes = new[] { new McpAccessScopeDto(Guid.Empty, (short)McpDataType.Postings, (short)McpRightKind.Create, null, null, null, null, null) };
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.CreateAsync(Guid.NewGuid(), NewRequest((short)McpAccessLevel.Individuell, scopes), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidScope", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Create_ScopesOnNonIndividualLevel_ThrowsInvalidScope()
    {
        var (svc, _, _) = Create();
        var scopes = new[] { new McpAccessScopeDto(Guid.Empty, (short)McpDataType.Contacts, (short)McpRightKind.Read, null, null, null, null, null) };
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.CreateAsync(Guid.NewGuid(), NewRequest((short)McpAccessLevel.Analyse, scopes), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidScope", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Rotate_IssuesNewToken_AndOldTokenNoLongerVerifies()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);
        var oldHash = (await db.McpAccesses.FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).TokenHash;

        var rotated = await svc.RotateTokenAsync(owner, created.Access.Id, CancellationToken.None);
        Assert.NotNull(rotated);
        Assert.StartsWith("fm_mcp_", rotated!.PlainTextToken);
        Assert.NotEqual(created.PlainTextToken, rotated.PlainTextToken);

        var entity = await db.McpAccesses.AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(oldHash, entity.TokenHash);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Revoke_MarksRevoked_AndCannotReactivate()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);

        Assert.True(await svc.RevokeAsync(owner, created.Access.Id, "lost", CancellationToken.None));
        var entity = await db.McpAccesses.AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(McpAccessStatus.Revoked, entity.Status);
        Assert.NotNull(entity.RevokedUtc);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.ActivateAsync(owner, created.Access.Id, CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidState", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Deactivate_Activate_RoundTrip()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);

        Assert.True(await svc.DeactivateAsync(owner, created.Access.Id, CancellationToken.None));
        Assert.Equal(McpAccessStatus.Deactivated, (await db.McpAccesses.AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).Status);

        Assert.True(await svc.ActivateAsync(owner, created.Access.Id, CancellationToken.None));
        Assert.Equal(McpAccessStatus.Active, (await db.McpAccesses.AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).Status);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task OwnerIsolation_ForeignOperationsReturnNotFound()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);

        Assert.Null(await svc.GetAsync(foreign, created.Access.Id, CancellationToken.None));
        Assert.False(await svc.UpdateAsync(foreign, created.Access.Id,
            new McpAccessUpdateRequest("x", null, (short)McpAccessLevel.Analyse, (short)McpApprovalMode.PerAction, null, null), CancellationToken.None));
        Assert.False(await svc.ActivateAsync(foreign, created.Access.Id, CancellationToken.None));
        Assert.False(await svc.DeactivateAsync(foreign, created.Access.Id, CancellationToken.None));
        Assert.Null(await svc.RotateTokenAsync(foreign, created.Access.Id, CancellationToken.None));
        Assert.False(await svc.RevokeAsync(foreign, created.Access.Id, null, CancellationToken.None));
        Assert.Empty(await svc.ListAsync(foreign, null, 0, 10, CancellationToken.None));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task SetScopes_IndividualLevel_AppliesAndFilters()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);

        var scopes = new McpScopeSetRequest(new[]
        {
            new McpAccessScopeDto(Guid.Empty, (short)McpDataType.Contacts, (short)McpRightKind.Read, null, null, null, null, null),
            new McpAccessScopeDto(Guid.Empty, (short)McpDataType.StatementDrafts, (short)McpRightKind.Read, null, null, null, null, null),
            new McpAccessScopeDto(Guid.Empty, (short)McpDataType.StatementDrafts, (short)McpRightKind.Book, null, null, null, null, null)
        });
        Assert.True(await svc.SetScopesAsync(owner, created.Access.Id, scopes, CancellationToken.None));

        var persisted = await db.McpAccessScopes.Where(s => s.McpAccessId == created.Access.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, persisted.Count);

        var scope = persisted.First(s => s.DataType == McpDataType.Contacts);
        var filters = new McpFilterSetRequest("0", Guid.NewGuid().ToString(), null, null, null);
        Assert.True(await svc.SetFiltersAsync(owner, created.Access.Id, scope.Id, filters, CancellationToken.None));
        var updated = await db.McpAccessScopes.AsNoTracking().FirstAsync(s => s.Id == scope.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("0", updated.AccountTypesCsv);
        Assert.NotNull(updated.ContactCategoryIdsCsv);
    }

    /// <summary>
    /// F-0501: <c>PUT …/scopes</c> rejects a tuple outside the F3 matrix with
    /// <c>Err_Mcp_InvalidScope</c> — the profile may only ever restrict, never extend
    /// beyond the catalog edges (postings are created via booking only).
    /// </summary>
    [Fact]
    public async Task SetScopes_InvalidTuple_ThrowsInvalidScope()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.SetScopesAsync(owner, created.Access.Id, new McpScopeSetRequest(new[]
            {
                ScopeDto(McpDataType.Postings, McpRightKind.Create)
            }), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidScope", ex.Code);
    }

    /// <summary>
    /// F-0501: persisted scope rows belong to the individual level only — a standard-level
    /// access derives its profile at runtime and cannot take a scope set.
    /// </summary>
    [Fact]
    public async Task SetScopes_NonIndividualLevel_ThrowsInvalidScope()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Analyse), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.SetScopesAsync(owner, created.Access.Id, new McpScopeSetRequest(new[]
            {
                ScopeDto(McpDataType.Contacts, McpRightKind.Read)
            }), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidScope", ex.Code);
    }

    /// <summary>
    /// F-0501/F-0502: a non-Read right without a Read row on the same data type is the
    /// fachlich unsinnige Kombination of ui-konzept.md §4.3.2 — rejected with
    /// <c>Err_Mcp_InvalidScope</c> (the matrix editor auto-selects Read; the API enforces it).
    /// </summary>
    [Fact]
    public async Task SetScopes_WriteWithoutRead_ThrowsInvalidScope()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.SetScopesAsync(owner, created.Access.Id, new McpScopeSetRequest(new[]
            {
                ScopeDto(McpDataType.StatementDrafts, McpRightKind.Book)
            }), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidScope", ex.Code);

        // A Read row on a *different* data type does not satisfy the implication.
        ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.SetScopesAsync(owner, created.Access.Id, new McpScopeSetRequest(new[]
            {
                ScopeDto(McpDataType.Contacts, McpRightKind.Read),
                ScopeDto(McpDataType.StatementDrafts, McpRightKind.Modify)
            }), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidScope", ex.Code);
    }

    /// <summary>
    /// F-0501: an out-of-range enum value is rejected before any matrix check —
    /// <c>Err_Mcp_InvalidScope</c>.
    /// </summary>
    [Fact]
    public async Task SetScopes_UnknownEnumValue_ThrowsInvalidScope()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.SetScopesAsync(owner, created.Access.Id, new McpScopeSetRequest(new[]
            {
                new McpAccessScopeDto(Guid.Empty, DataType: 99, (short)McpRightKind.Read, null, null, null, null, null)
            }), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidScope", ex.Code);
    }

    /// <summary>
    /// F-0501: a foreign owner cannot replace scopes — the access behaves like a
    /// non-existing record (404 semantics, no information leak).
    /// </summary>
    [Fact]
    public async Task SetScopes_ForeignOwner_ReturnsFalse()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);

        Assert.False(await svc.SetScopesAsync(Guid.NewGuid(), created.Access.Id,
            new McpScopeSetRequest(new[] { ScopeDto(McpDataType.Contacts, McpRightKind.Read) }),
            CancellationToken.None));
    }

    /// <summary>
    /// A-01: a scope change persists the normative before/after scope delta — added tuples
    /// with their after-image, removed tuples with their before-image and filter-set
    /// changes of a surviving tuple as old→new (audit-konzept.md §3.3).
    /// </summary>
    [Fact]
    public async Task SetScopes_WritesScopeDelta_AddedChangedRemoved()
    {
        var (svc, db, audit) = CreateWithAudit();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);
        var accessId = created.Access.Id;

        Assert.True(await svc.SetScopesAsync(owner, accessId, new McpScopeSetRequest(new[]
        {
            ScopeDto(McpDataType.Contacts, McpRightKind.Read),
            ScopeDto(McpDataType.Accounts, McpRightKind.Read, accountTypes: "0,1")
        }), CancellationToken.None));

        var entry = await LatestAccessEventAsync(db, "ScopesChanged");
        Assert.Equal(McpAuditCategory.AccessEvent, entry.Category);
        var changes = await ChangesOfAsync(db, entry.Id);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, c =>
        {
            Assert.Equal("McpAccessScope", c.EntityType);
            Assert.Equal(accessId, c.EntityId);
            Assert.Null(c.OldValue); // added tuples carry only the after-image
        });
        Assert.Equal("Contacts:Read", changes.Single(c => c.Field == "Scope[Contacts:Read]").NewValue);
        Assert.Equal("Accounts:Read[AccountTypes=0,1]",
            changes.Single(c => c.Field == "Scope[Accounts:Read]").NewValue);

        // Second round: Contacts removed, the Accounts filter set changed.
        Assert.True(await svc.SetScopesAsync(owner, accessId, new McpScopeSetRequest(new[]
        {
            ScopeDto(McpDataType.Accounts, McpRightKind.Read, accountTypes: "0,1,2")
        }), CancellationToken.None));

        entry = await LatestAccessEventAsync(db, "ScopesChanged");
        changes = await ChangesOfAsync(db, entry.Id);
        Assert.Equal(2, changes.Count);
        var removed = changes.Single(c => c.Field == "Scope[Contacts:Read]");
        Assert.Equal("Contacts:Read", removed.OldValue);
        Assert.Null(removed.NewValue);
        var changed = changes.Single(c => c.Field == "Scope[Accounts:Read]");
        Assert.Equal("Accounts:Read[AccountTypes=0,1]", changed.OldValue);
        Assert.Equal("Accounts:Read[AccountTypes=0,1,2]", changed.NewValue);

        // The delta rows are part of the hashed entry bundle — the chain stays intact.
        Assert.True((await audit.VerifyAsync(owner, CancellationToken.None)).IsIntact);
    }

    /// <summary>
    /// A-01: a filter change persists one delta row per changed filter dimension of the
    /// affected scope row (audit-konzept.md §3.3).
    /// </summary>
    [Fact]
    public async Task SetFilters_WritesFilterDelta_PerDimension()
    {
        var (svc, db, _) = CreateWithAudit();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell, new[]
        {
            ScopeDto(McpDataType.Contacts, McpRightKind.Read, accountTypes: "0")
        }), CancellationToken.None);
        var scopeId = (await db.McpAccessScopes.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).Id;
        var categoryId = Guid.NewGuid();

        Assert.True(await svc.SetFiltersAsync(owner, created.Access.Id, scopeId,
            new McpFilterSetRequest("0,1", categoryId.ToString(), null, null, null), CancellationToken.None));

        var entry = await LatestAccessEventAsync(db, "FiltersChanged");
        var changes = await ChangesOfAsync(db, entry.Id);
        Assert.Equal(2, changes.Count); // only the changed dimensions
        Assert.All(changes, c =>
        {
            Assert.Equal("McpAccessScope", c.EntityType);
            Assert.Equal(scopeId, c.EntityId);
        });
        var accountTypes = changes.Single(c => c.Field == "AccountTypesCsv");
        Assert.Equal("0", accountTypes.OldValue);
        Assert.Equal("0,1", accountTypes.NewValue);
        var contactCategories = changes.Single(c => c.Field == "ContactCategoryIdsCsv");
        Assert.Null(contactCategories.OldValue);
        Assert.Equal(categoryId.ToString(), contactCategories.NewValue);
    }

    /// <summary>
    /// A-01: an access update persists the changed configuration fields as before/after
    /// rows; a level switch away from the individual level additionally records the removal
    /// of the persisted scope rows in the same delta (audit-konzept.md §3.3).
    /// </summary>
    [Fact]
    public async Task Update_WritesFieldDelta_IncludingScopeRemovalOnLevelSwitch()
    {
        var (svc, db, _) = CreateWithAudit();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell, new[]
        {
            ScopeDto(McpDataType.Contacts, McpRightKind.Read)
        }), CancellationToken.None);
        var accessId = created.Access.Id;

        Assert.True(await svc.UpdateAsync(owner, accessId,
            new McpAccessUpdateRequest("client-b", null, (short)McpAccessLevel.Analyse,
                (short)McpApprovalMode.PreApproved, null, "10.0.0.0/8"),
            CancellationToken.None));

        var entry = await LatestAccessEventAsync(db, "AccessUpdated");
        var changes = await ChangesOfAsync(db, entry.Id);
        Assert.Equal("client-a", changes.Single(c => c.Field == "Name").OldValue);
        Assert.Equal("client-b", changes.Single(c => c.Field == "Name").NewValue);
        var description = changes.Single(c => c.Field == "Description");
        Assert.Equal("note", description.OldValue);
        Assert.Null(description.NewValue);
        var level = changes.Single(c => c.Field == "Level");
        Assert.Equal("Individuell", level.OldValue);
        Assert.Equal("Analyse", level.NewValue);
        var approvalMode = changes.Single(c => c.Field == "ApprovalMode");
        Assert.Equal("PerAction", approvalMode.OldValue);
        Assert.Equal("PreApproved", approvalMode.NewValue);
        var ipRanges = changes.Single(c => c.Field == "AllowedIpRanges");
        Assert.Null(ipRanges.OldValue);
        Assert.Equal("10.0.0.0/8", ipRanges.NewValue);
        // Removed scope tuple of the abandoned individual profile.
        var scope = changes.Single(c => c.Field == "Scope[Contacts:Read]");
        Assert.Equal("Contacts:Read", scope.OldValue);
        Assert.Null(scope.NewValue);
        Assert.DoesNotContain(changes, c => c.Field == "ExpiresUtc"); // unchanged fields produce no row
    }

    /// <summary>
    /// A-01: the delta rows never carry credential material — no token hash, plaintext
    /// token or secret-shaped substring reaches the persisted audit values.
    /// </summary>
    [Fact]
    public async Task AccessEvents_DeltaContainsNoSecrets()
    {
        var (svc, db, _) = CreateWithAudit();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell, new[]
        {
            ScopeDto(McpDataType.Contacts, McpRightKind.Read)
        }), CancellationToken.None);
        var accessId = created.Access.Id;
        var tokenHash = (await db.McpAccesses.AsNoTracking().FirstAsync(a => a.Id == accessId, cancellationToken: TestContext.Current.CancellationToken)).TokenHash;

        await svc.SetScopesAsync(owner, accessId, new McpScopeSetRequest(new[]
        {
            ScopeDto(McpDataType.Accounts, McpRightKind.Read)
        }), CancellationToken.None);
        await svc.UpdateAsync(owner, accessId,
            new McpAccessUpdateRequest("client-b", null, (short)McpAccessLevel.Analyse,
                (short)McpApprovalMode.PerAction, null, null), CancellationToken.None);
        await svc.RotateTokenAsync(owner, accessId, CancellationToken.None);

        var changes = await db.McpAuditEntryChanges.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(changes);
        Assert.All(changes, c =>
        {
            foreach (var value in new[] { c.OldValue, c.NewValue })
            {
                Assert.DoesNotContain("pbkdf2", value ?? string.Empty);
                Assert.DoesNotContain("fm_mcp_", value ?? string.Empty);
                Assert.DoesNotContain(tokenHash, value ?? string.Empty);
                Assert.DoesNotContain(created.PlainTextToken, value ?? string.Empty);
            }
            Assert.DoesNotContain("Token", c.Field); // no credential field is ever diffed
        });
    }

    /// <summary>
    /// F-0904 (approval-konzept.md §6.3): revoking an access withdraws all still-open
    /// (pending and approved-not-claimed) approval requests of that access with a documented
    /// system reason and a coupled <c>ApprovalWithdrawn</c> record; requests of other
    /// accesses are untouched.
    /// </summary>
    [Fact]
    public async Task Revoke_WithdrawsOpenApprovalRequests_WithAudit()
    {
        var (svc, db) = CreateWithApprovals();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);
        var accessId = created.Access.Id;
        var otherAccessId = Guid.NewGuid();

        var pending = new AiActionRequest(owner, accessId, "acc", "fm_x", "delete", "katalog-v1",
            "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-p");
        var approved = AiActionRequest.CreatePreApproved(owner, accessId, "acc", "fm_y", "call",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-a", "pre");
        var claimed = AiActionRequest.CreatePreApproved(owner, accessId, "acc", "fm_z", "call",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-c", "pre");
        claimed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow); // active execution — untouched
        var foreign = new AiActionRequest(owner, otherAccessId, "other", "fm_w", "call",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-f");
        db.AiActionRequests.AddRange(pending, approved, claimed, foreign);
        await db.SaveChangesAsync(CancellationToken.None);

        Assert.True(await svc.RevokeAsync(owner, accessId, "lost", CancellationToken.None));

        var rows = await db.AiActionRequests.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var withdrawn = rows.Where(r => r.Id == pending.Id || r.Id == approved.Id).ToList();
        Assert.All(withdrawn, r =>
        {
            Assert.Equal(AiActionRequestStatus.Rejected, r.Status);
            Assert.Equal("Zugang entzogen", r.DecisionNote);
            Assert.Null(r.DecidedByUserId); // system transition
        });
        Assert.Equal(AiActionRequestStatus.Approved, rows.Single(r => r.Id == claimed.Id).Status);
        Assert.Equal(AiActionRequestStatus.Pending, rows.Single(r => r.Id == foreign.Id).Status);
        Assert.Equal(2, await db.McpAuditEntries.AsNoTracking()
            .CountAsync(e => e.EventType == "ApprovalWithdrawn", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// F-0904 (approval-konzept.md §6.3): deactivating an access withdraws its open approval
    /// requests as well — the status change must also stop banked effects.
    /// </summary>
    [Fact]
    public async Task Deactivate_WithdrawsOpenApprovalRequests()
    {
        var (svc, db) = CreateWithApprovals();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);
        var pending = new AiActionRequest(owner, created.Access.Id, "acc", "fm_x", "delete",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-p");
        db.AiActionRequests.Add(pending);
        await db.SaveChangesAsync(CancellationToken.None);

        Assert.True(await svc.DeactivateAsync(owner, created.Access.Id, CancellationToken.None));

        var row = await db.AiActionRequests.AsNoTracking().SingleAsync(r => r.Id == pending.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(AiActionRequestStatus.Rejected, row.Status);
        Assert.Equal("Zugang deaktiviert", row.DecisionNote);
    }

    /// <summary>A data-tool descriptor stub declaring the given required scope tuples.</summary>
    private static IMcpToolDescriptor DataToolDescriptor(string name, params McpToolScope[] requiredScopes)
    {
        var d = new Mock<IMcpToolDescriptor>();
        d.SetupGet(x => x.Name).Returns(name);
        d.SetupGet(x => x.RequiredScopes).Returns(requiredScopes);
        d.SetupGet(x => x.MetaScope).Returns((string?)null);
        return d.Object;
    }

    /// <summary>
    /// A1 (F-0904, approval-konzept.md §3.3/§6.1): a scope-profile change system-withdraws —
    /// granularly — the open requests whose required scope tuple the new profile no longer
    /// grants („Recht entfallen"); still-covered requests stay open for a normal decision.
    /// </summary>
    [Fact]
    public async Task SetScopes_RemovedScope_WithdrawsUncoveredRequests()
    {
        var registry = new Mock<IMcpToolRegistry>();
        registry.Setup(r => r.Find("fm_delete_tool")).Returns(DataToolDescriptor("fm_delete_tool",
            new McpToolScope(McpDataType.Contacts, McpRightKind.Delete)));
        registry.Setup(r => r.Find("fm_read_tool")).Returns(DataToolDescriptor("fm_read_tool",
            new McpToolScope(McpDataType.Contacts, McpRightKind.Read)));
        var (svc, db) = CreateWithApprovals(registry);
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell, new[]
        {
            ScopeDto(McpDataType.Contacts, McpRightKind.Read),
            ScopeDto(McpDataType.Contacts, McpRightKind.Delete)
        }), CancellationToken.None);
        var accessId = created.Access.Id;

        var deleteRequest = new AiActionRequest(owner, accessId, "acc", "fm_delete_tool", "delete",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-del");
        var readRequest = new AiActionRequest(owner, accessId, "acc", "fm_read_tool", "list",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-read");
        db.AiActionRequests.AddRange(deleteRequest, readRequest);
        await db.SaveChangesAsync(CancellationToken.None);

        // Drop Contacts:Delete from the individual profile.
        Assert.True(await svc.SetScopesAsync(owner, accessId, new McpScopeSetRequest(new[]
        {
            ScopeDto(McpDataType.Contacts, McpRightKind.Read)
        }), CancellationToken.None));

        var rows = await db.AiActionRequests.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var withdrawn = rows.Single(r => r.Id == deleteRequest.Id);
        Assert.Equal(AiActionRequestStatus.Rejected, withdrawn.Status);
        Assert.Equal("Recht entfallen", withdrawn.DecisionNote);
        Assert.Null(withdrawn.DecidedByUserId); // system transition
        Assert.Equal(AiActionRequestStatus.Pending, rows.Single(r => r.Id == readRequest.Id).Status);
        Assert.Single(await db.McpAuditEntries.AsNoTracking()
            .Where(e => e.EventType == "ApprovalWithdrawn").ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A1 (F-0904): a level switch away from the individual level drops the persisted scope
    /// rows — the same granular „Recht entfallen" withdrawal applies because the effective
    /// profile changed (Analyse grants read-only on every data type).
    /// </summary>
    [Fact]
    public async Task Update_LevelSwitch_WithdrawsUncoveredRequests()
    {
        var registry = new Mock<IMcpToolRegistry>();
        registry.Setup(r => r.Find("fm_delete_tool")).Returns(DataToolDescriptor("fm_delete_tool",
            new McpToolScope(McpDataType.Contacts, McpRightKind.Delete)));
        registry.Setup(r => r.Find("fm_read_tool")).Returns(DataToolDescriptor("fm_read_tool",
            new McpToolScope(McpDataType.Contacts, McpRightKind.Read)));
        var (svc, db) = CreateWithApprovals(registry);
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell, new[]
        {
            ScopeDto(McpDataType.Contacts, McpRightKind.Read),
            ScopeDto(McpDataType.Contacts, McpRightKind.Delete)
        }), CancellationToken.None);
        var accessId = created.Access.Id;

        var deleteRequest = new AiActionRequest(owner, accessId, "acc", "fm_delete_tool", "delete",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-del");
        var readRequest = new AiActionRequest(owner, accessId, "acc", "fm_read_tool", "list",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "k-read");
        db.AiActionRequests.AddRange(deleteRequest, readRequest);
        await db.SaveChangesAsync(CancellationToken.None);

        Assert.True(await svc.UpdateAsync(owner, accessId,
            new McpAccessUpdateRequest("client-a", null, (short)McpAccessLevel.Analyse,
                (short)McpApprovalMode.PerAction, null, null), CancellationToken.None));

        var rows = await db.AiActionRequests.AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(AiActionRequestStatus.Rejected, rows.Single(r => r.Id == deleteRequest.Id).Status);
        Assert.Equal("Recht entfallen", rows.Single(r => r.Id == deleteRequest.Id).DecisionNote);
        Assert.Equal(AiActionRequestStatus.Pending, rows.Single(r => r.Id == readRequest.Id).Status);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task GetLevels_ContainsAllLevels_WithDefaultProfiles()
    {
        var (svc, _, _) = Create();
        var levels = await svc.GetLevelsAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(5, levels.Count);
        Assert.All(levels.Where(l => l.Level != (short)McpAccessLevel.Individuell),
            l => Assert.NotEmpty(l.DefaultScopes));
        Assert.Empty(levels.Single(l => l.Level == (short)McpAccessLevel.Individuell).DefaultScopes);
    }

    /// <summary>
    /// F-0501 hard gate: an explicitly set <c>Administration</c> tuple is rejected for a
    /// non-admin owner — filtered silently only inside the derived standard profiles.
    /// </summary>
    [Fact]
    public async Task SetScopes_AdministrationTuple_NonAdmin_ThrowsAdminRequired()
    {
        var (svc, _, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.SetScopesAsync(owner, created.Access.Id, new McpScopeSetRequest(new[]
            {
                ScopeDto(McpDataType.Administration, McpRightKind.Read)
            }), CancellationToken.None));
        Assert.Equal("Err_Mcp_AdminRequired", ex.Code);
    }

    /// <summary>Admin owners may persist Administration tuples on the individual level.</summary>
    [Fact]
    public async Task SetScopes_AdministrationTuple_AdminOwner_Applies()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        await SeedAdminRoleAsync(db, owner);
        var created = await svc.CreateAsync(owner, NewRequest((short)McpAccessLevel.Individuell), CancellationToken.None);

        Assert.True(await svc.SetScopesAsync(owner, created.Access.Id, new McpScopeSetRequest(new[]
        {
            ScopeDto(McpDataType.Administration, McpRightKind.Read),
            ScopeDto(McpDataType.Administration, McpRightKind.Modify)
        }), CancellationToken.None));

        var persisted = await db.McpAccessScopes.AsNoTracking()
            .Where(s => s.McpAccessId == created.Access.Id && s.DataType == McpDataType.Administration)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, persisted.Count);
    }

    /// <summary>The same hard gate applies to a seeded profile on create.</summary>
    [Fact]
    public async Task Create_IndividualAdministrationScope_NonAdmin_ThrowsAdminRequired()
    {
        var (svc, _, _) = Create();
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.CreateAsync(Guid.NewGuid(), NewRequest((short)McpAccessLevel.Individuell, new[]
            {
                ScopeDto(McpDataType.Administration, McpRightKind.Read)
            }), CancellationToken.None));
        Assert.Equal("Err_Mcp_AdminRequired", ex.Code);
    }

    /// <summary>
    /// A standard-level access carries no persisted rows — the derived effective profile
    /// drops the Administration tuples for non-admin owners and keeps them for admins.
    /// </summary>
    [Fact]
    public async Task Create_StandardLevel_EffectiveProfile_FiltersAdministrationByOwnerRole()
    {
        var (svc, db, _) = Create();
        var nonAdmin = Guid.NewGuid();
        var admin = Guid.NewGuid();
        await SeedAdminRoleAsync(db, admin);

        var nonAdminCreated = await svc.CreateAsync(nonAdmin, NewRequest(), CancellationToken.None);
        Assert.DoesNotContain(nonAdminCreated.Access.EffectiveScopes,
            s => s.DataType == (short)McpDataType.Administration);

        var adminCreated = await svc.CreateAsync(admin, NewRequest(), CancellationToken.None);
        Assert.Contains(adminCreated.Access.EffectiveScopes,
            s => s.DataType == (short)McpDataType.Administration && s.Right == (short)McpRightKind.Read);
    }

    /// <summary>
    /// The options payload drives the rights matrix rows — the Administration row exists
    /// for admin owners only (UI: the line is hidden for everyone else).
    /// </summary>
    [Fact]
    public async Task GetOptions_AdministrationRow_OnlyForAdminOwners()
    {
        var (svc, db, _) = Create();
        var nonAdmin = Guid.NewGuid();
        var admin = Guid.NewGuid();
        await SeedAdminRoleAsync(db, admin);

        var nonAdminOptions = await svc.GetOptionsAsync(nonAdmin, false, CancellationToken.None);
        Assert.DoesNotContain(nonAdminOptions.DataTypes,
            t => t.DataType == (short)McpDataType.Administration);
        Assert.Equal(Enum.GetValues<McpDataType>().Length - 1, nonAdminOptions.DataTypes.Count);

        var adminOptions = await svc.GetOptionsAsync(admin, false, CancellationToken.None);
        Assert.Contains(adminOptions.DataTypes,
            t => t.DataType == (short)McpDataType.Administration);
    }

    /// <summary>The advertised level profiles omit Administration tuples for non-admins.</summary>
    [Fact]
    public async Task GetLevels_NonAdmin_OmitsAdministrationTuples()
    {
        var (svc, db, _) = Create();
        var admin = Guid.NewGuid();
        await SeedAdminRoleAsync(db, admin);

        var nonAdminLevels = await svc.GetLevelsAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.All(nonAdminLevels,
            l => Assert.DoesNotContain(l.DefaultScopes, s => s.DataType == (short)McpDataType.Administration));

        var adminLevels = await svc.GetLevelsAsync(admin, CancellationToken.None);
        Assert.Contains(adminLevels.Single(l => l.Level == (short)McpAccessLevel.Analyse).DefaultScopes,
            s => s.DataType == (short)McpDataType.Administration);
    }

    /// <summary>Minimal options monitor stub.</summary>
    private sealed class TestOptionsMonitor : IOptionsMonitor<McpOptions>
    {
        public TestOptionsMonitor(McpOptions value) => CurrentValue = value;
        public McpOptions CurrentValue { get; }
        public McpOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<McpOptions, string?> listener) => null;
    }
}
