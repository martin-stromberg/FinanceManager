using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
            NullLogger<McpAccessService>.Instance);
        return (svc, db, issuer);
    }

    private static McpAccessCreateRequest NewRequest(short level = (short)McpAccessLevel.Analyse, IReadOnlyList<McpAccessScopeDto>? scopes = null)
        => new("client-a", "note", level, (short)McpApprovalMode.PerAction, null, null, scopes);

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Create_ReturnsPlaintextToken_OnlyOnce_AndStoresOnlyHash()
    {
        var (svc, db, _) = Create();
        var owner = Guid.NewGuid();
        var created = await svc.CreateAsync(owner, NewRequest(), CancellationToken.None);

        Assert.StartsWith("fm_mcp_", created.PlainTextToken);
        var entity = await db.McpAccesses.FirstAsync();
        Assert.DoesNotContain(created.PlainTextToken, entity.TokenHash);
        Assert.DoesNotContain(created.PlainTextToken, entity.AccessKeyHint);
        Assert.Equal(McpAccessStatus.Active, entity.Status);
        Assert.Empty(await db.McpAccesses.Where(a => a.OwnerUserId != owner).ToListAsync());
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
        var oldHash = (await db.McpAccesses.FirstAsync()).TokenHash;

        var rotated = await svc.RotateTokenAsync(owner, created.Access.Id, CancellationToken.None);
        Assert.NotNull(rotated);
        Assert.StartsWith("fm_mcp_", rotated!.PlainTextToken);
        Assert.NotEqual(created.PlainTextToken, rotated.PlainTextToken);

        var entity = await db.McpAccesses.AsNoTracking().FirstAsync();
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
        var entity = await db.McpAccesses.AsNoTracking().FirstAsync();
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
        Assert.Equal(McpAccessStatus.Deactivated, (await db.McpAccesses.AsNoTracking().FirstAsync()).Status);

        Assert.True(await svc.ActivateAsync(owner, created.Access.Id, CancellationToken.None));
        Assert.Equal(McpAccessStatus.Active, (await db.McpAccesses.AsNoTracking().FirstAsync()).Status);
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
            new McpAccessScopeDto(Guid.Empty, (short)McpDataType.StatementDrafts, (short)McpRightKind.Book, null, null, null, null, null)
        });
        Assert.True(await svc.SetScopesAsync(owner, created.Access.Id, scopes, CancellationToken.None));

        var persisted = await db.McpAccessScopes.Where(s => s.McpAccessId == created.Access.Id).ToListAsync();
        Assert.Equal(2, persisted.Count);

        var scope = persisted.First(s => s.DataType == McpDataType.Contacts);
        var filters = new McpFilterSetRequest("0", Guid.NewGuid().ToString(), null, null, null);
        Assert.True(await svc.SetFiltersAsync(owner, created.Access.Id, scope.Id, filters, CancellationToken.None));
        var updated = await db.McpAccessScopes.AsNoTracking().FirstAsync(s => s.Id == scope.Id);
        Assert.Equal("0", updated.AccountTypesCsv);
        Assert.NotNull(updated.ContactCategoryIdsCsv);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task GetLevels_ContainsAllLevels_WithDefaultProfiles()
    {
        var (svc, _, _) = Create();
        var levels = await svc.GetLevelsAsync(CancellationToken.None);
        Assert.Equal(5, levels.Count);
        Assert.All(levels.Where(l => l.Level != (short)McpAccessLevel.Individuell),
            l => Assert.NotEmpty(l.DefaultScopes));
        Assert.Empty(levels.Single(l => l.Level == (short)McpAccessLevel.Individuell).DefaultScopes);
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
