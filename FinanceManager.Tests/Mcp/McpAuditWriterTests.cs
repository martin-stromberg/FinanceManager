using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the persistent, hash-chained <see cref="McpAuditWriter"/> (F-0301/F-0302):
/// persistence of all five categories, graduated detail depth (secret redaction, length
/// caps, no binary material), per-owner/global <c>SequenceNo</c> chains, <c>PrevHash</c>/
/// <c>EntryHash</c> anchoring, atomic entry+change bundles, staged coupling and the
/// <see cref="IMcpAuditWriter.VerifyAsync"/> integrity check (breach and gap detection).
/// </summary>
public sealed class McpAuditWriterTests
{
    private static (McpAuditWriter writer, AppDbContext db, SqliteConnection connection) CreateSqlite()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance), db, connection);
    }

    private static McpAuditWriteRequest Request(
        McpAuditCategory category = McpAuditCategory.ToolCall,
        string eventType = "ToolCall",
        string result = "Success",
        Guid? owner = null,
        bool globalChain = false,
        string? payload = null,
        IReadOnlyList<McpAuditFieldChange>? changes = null)
        => new(category, eventType, result,
            OwnerUserId: globalChain ? null : (owner ?? Guid.NewGuid()),
            McpAccessId: Guid.NewGuid(),
            McpAccessName: "access-a",
            ToolName: "fm_accounts_overview",
            CatalogVersion: "katalog-v1",
            Context: "McpEndpoint",
            CorrelationId: Guid.NewGuid(),
            SourceIp: "192.168.1.10",
            DurationMs: 12,
            AffectedCount: 3,
            PayloadJson: payload,
            Changes: changes);

    /// <summary>Every audit category of the normative five-value list is persisted.</summary>
    [Fact]
    public async Task WriteAsync_PersistsAllFiveCategories()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();

        foreach (var category in Enum.GetValues<McpAuditCategory>())
        {
            await writer.WriteAsync(Request(category, $"Evt{category}", owner: owner), CancellationToken.None);
        }

        var stored = await db.McpAuditEntries.AsNoTracking()
            .Where(e => e.OwnerUserId == owner).OrderBy(e => e.SequenceNo).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, stored.Count);
        Assert.Equal(Enum.GetValues<McpAuditCategory>(), stored.Select(e => e.Category).ToArray());
    }

    /// <summary>Persisted entries carry the full grundgerüst and a server-side timestamp.</summary>
    [Fact]
    public async Task WriteAsync_PersistsGrundgeruestFields_WithServerTimestamp()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();
        var before = DateTime.UtcNow;

        await writer.WriteAsync(Request(owner: owner), CancellationToken.None);

        var entry = await db.McpAuditEntries.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("ToolCall", entry.EventType);
        Assert.Equal("Success", entry.ResultStatus);
        Assert.Equal(owner, entry.OwnerUserId);
        Assert.Equal("access-a", entry.McpAccessName);
        Assert.Equal("fm_accounts_overview", entry.ToolName);
        Assert.Equal("katalog-v1", entry.CatalogVersion);
        Assert.Equal("McpEndpoint", entry.Context);
        Assert.Equal("192.168.1.10", entry.SourceIp);
        Assert.Equal(12, entry.DurationMs);
        Assert.Equal(3, entry.AffectedCount);
        Assert.NotNull(entry.CorrelationId);
        Assert.InRange(entry.Utc, before.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(64, entry.EntryHash.Length); // sha256 lowercase hex
        Assert.Equal(entry.EntryHash, entry.EntryHash.ToLowerInvariant());
        Assert.Null(entry.PrevHash); // chain head
    }

    /// <summary>Sequence numbers are monotonic and contiguous per owner chain.</summary>
    [Fact]
    public async Task WriteAsync_AssignsMonotonicSequenceNo_PerOwnerChain()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        for (var i = 0; i < 3; i++)
        {
            await writer.WriteAsync(Request(owner: ownerA), CancellationToken.None);
        }
        await writer.WriteAsync(Request(owner: ownerB), CancellationToken.None);
        await writer.WriteAsync(Request(globalChain: true), CancellationToken.None); // global chain

        var seqsA = await db.McpAuditEntries.Where(e => e.OwnerUserId == ownerA)
            .OrderBy(e => e.SequenceNo).Select(e => e.SequenceNo).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1, 2, 3 }, seqsA);
        Assert.Equal(1, await db.McpAuditEntries.Where(e => e.OwnerUserId == ownerB).Select(e => e.SequenceNo).SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.McpAuditEntries.Where(e => e.OwnerUserId == null).Select(e => e.SequenceNo).SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Owner chains and the global (null-owner) chain are independent.</summary>
    [Fact]
    public async Task WriteAsync_GlobalChain_IsIndependentOfOwnerChains()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;

        await writer.WriteAsync(Request(globalChain: true), CancellationToken.None);
        await writer.WriteAsync(Request(globalChain: true), CancellationToken.None);
        await writer.WriteAsync(Request(owner: Guid.NewGuid()), CancellationToken.None);

        var globalSeqs = await db.McpAuditEntries.Where(e => e.OwnerUserId == null)
            .OrderBy(e => e.SequenceNo).Select(e => e.SequenceNo).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1, 2 }, globalSeqs);
    }

    /// <summary>Every entry anchors to the hash of its chain predecessor.</summary>
    [Fact]
    public async Task WriteAsync_LinksPrevHashToPredecessorEntryHash()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();

        await writer.WriteAsync(Request(owner: owner), CancellationToken.None);
        await writer.WriteAsync(Request(owner: owner, eventType: "ScopesChanged"), CancellationToken.None);
        await writer.WriteAsync(Request(owner: owner, eventType: "TokenRotated"), CancellationToken.None);

        var entries = await db.McpAuditEntries.AsNoTracking()
            .OrderBy(e => e.SequenceNo).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(entries[0].PrevHash);
        Assert.Equal(entries[0].EntryHash, entries[1].PrevHash);
        Assert.Equal(entries[1].EntryHash, entries[2].PrevHash);
        Assert.Equal(3, entries.Select(e => e.EntryHash).Distinct().Count());
    }

    /// <summary>Field deltas persist as own rows attached to the entry (atomic bundle).</summary>
    [Fact]
    public async Task WriteAsync_PersistsFieldDeltas_AsChildRows()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var entityId = Guid.NewGuid();
        var changes = new[]
        {
            new McpAuditFieldChange("McpAccess", entityId, "Status", "Active", "Deactivated"),
            new McpAuditFieldChange("McpAccess", entityId, "Level", "Analyse", "Individuell")
        };

        await writer.WriteAsync(Request(
            category: McpAuditCategory.DataChange, eventType: "AccessUpdated", changes: changes),
            CancellationToken.None);

        var entry = await db.McpAuditEntries.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        var stored = await db.McpAuditEntryChanges.AsNoTracking()
            .Where(c => c.EntryId == entry.Id).OrderBy(c => c.Field).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, stored.Count);
        Assert.Equal("Level", stored[0].Field);
        Assert.Equal("Analyse", stored[0].OldValue);
        Assert.Equal("Individuell", stored[0].NewValue);
        Assert.Equal("Deactivated", stored[1].NewValue);
        Assert.Equal(entityId, stored[0].EntityId);
    }

    /// <summary>Token material in payloads is redacted — never stored, never hashed.</summary>
    [Fact]
    public async Task WriteAsync_RedactsTokenMaterial_InPayloadAndChanges()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var token = "fm_mcp_ABCDEFGH_abcdefghijklmnopqrstuvwxyz0123456789_-abcde";
        var changes = new[]
        {
            new McpAuditFieldChange("McpAccess", Guid.NewGuid(), "Note", null, $"Bearer abcdef123456==")
        };

        await writer.WriteAsync(Request(payload: $"{{\"token\":\"{token}\"}}", changes: changes),
            CancellationToken.None);

        var entry = await db.McpAuditEntries.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain("fm_mcp_", entry.PayloadJson);
        Assert.DoesNotContain(token, entry.PayloadJson);
        Assert.Contains("[redacted]", entry.PayloadJson);

        var change = await db.McpAuditEntryChanges.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain("abcdef123456==", change.NewValue);
        Assert.Contains("[redacted]", change.NewValue);
    }

    /// <summary>Oversized payloads are wrapped in a valid-JSON truncation envelope.</summary>
    [Fact]
    public async Task WriteAsync_TruncatesOversizedPayload_KeepingValidJson()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var payload = "{\"data\":\"" + new string('x', McpAuditWriter.MaxPayloadChars + 1000) + "\"}";

        await writer.WriteAsync(Request(payload: payload), CancellationToken.None);

        var entry = await db.McpAuditEntries.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(entry.PayloadJson);
        using var doc = JsonDocument.Parse(entry.PayloadJson!);
        Assert.True(doc.RootElement.GetProperty("truncated").GetBoolean());
        Assert.Equal(payload.Length, doc.RootElement.GetProperty("originalChars").GetInt32());
        Assert.Equal(McpAuditWriter.MaxPayloadChars, doc.RootElement.GetProperty("head").GetString()!.Length);
    }

    /// <summary>Field-delta values are capped at 500 characters with a truncation note.</summary>
    [Fact]
    public async Task WriteAsync_TruncatesOversizedChangeValues()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var longValue = new string('v', McpAuditWriter.MaxChangeValueChars + 200);
        var changes = new[] { new McpAuditFieldChange("Contact", Guid.NewGuid(), "Name", null, longValue) };

        await writer.WriteAsync(Request(changes: changes), CancellationToken.None);

        var change = await db.McpAuditEntryChanges.AsNoTracking().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.StartsWith(new string('v', 100), change.NewValue);
        Assert.EndsWith("…[truncated]", change.NewValue);
        Assert.True(change.NewValue!.Length <= McpAuditWriter.MaxChangeValueChars + 20);
    }

    /// <summary>Missing required fields are rejected before any persistence work.</summary>
    [Fact]
    public async Task WriteAsync_RejectsInvalidRequests()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;

        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.WriteAsync(Request(eventType: " "), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.WriteAsync(Request(result: ""), CancellationToken.None));
        Assert.Equal(0, await db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>StageAsync tracks the bundle without saving; the caller's save commits it.</summary>
    [Fact]
    public async Task StageAsync_CommitsWithCallersSaveChanges()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();

        var staged = await writer.StageAsync(Request(owner: owner), CancellationToken.None);
        Assert.NotNull(staged);
        Assert.Equal(0, await db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken)); // nothing saved yet

        // Simulate the coupled business effect in the same SaveChanges round.
        var access = new McpAccess(owner, "acc", "HINT01", "pbkdf2|1|AA|AA",
            McpAccessLevel.Analyse, McpApprovalMode.PerAction);
        db.McpAccesses.Add(access);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.McpAccesses.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, staged!.SequenceNo);
    }

    /// <summary>Concurrent writes on different contexts get distinct sequence numbers.</summary>
    [Fact]
    public async Task WriteAsync_ConcurrentChains_SerializeWithoutDuplicates()
    {
        var (writer1, db1, conn) = CreateSqlite();
        await using var _ = conn;
        var db2 = new AppDbContext(db1.Database.GetDbConnection() is SqliteConnection c
            ? new DbContextOptionsBuilder<AppDbContext>().UseSqlite(c).Options
            : throw new InvalidOperationException());
        var writer2 = new McpAuditWriter(db2, NullLogger<McpAuditWriter>.Instance);
        var owner = Guid.NewGuid();

        await Task.WhenAll(
            writer1.WriteAsync(Request(owner: owner), CancellationToken.None),
            writer2.WriteAsync(Request(owner: owner), CancellationToken.None));

        var seqs = await db1.McpAuditEntries.AsNoTracking()
            .OrderBy(e => e.SequenceNo).Select(e => e.SequenceNo).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1, 2 }, seqs);
    }

    /// <summary>
    /// Simulates the live defect: a competing scope commits a staged bundle on the same
    /// chain head while the writer's save is in flight — staged saves commit outside the
    /// writer's process-wide chain lock, so the just-computed sequence number is claimed
    /// between the head read and the insert.
    /// </summary>
    /// <param name="inject">The lock-bypassing commit run once per armed save attempt.</param>
    private sealed class CollidingCommitInterceptor(Func<CancellationToken, Task> inject) : SaveChangesInterceptor
    {
        private int _armed;

        /// <summary>Arms <paramref name="count"/> colliding commits, one per save attempt.</summary>
        public void Arm(int count) => _armed = count;

        /// <summary>Number of injected colliding commits so far.</summary>
        public int Fired { get; private set; }

        /// <inheritdoc />
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (_armed > 0
                && eventData.Context!.ChangeTracker.Entries<McpAuditEntry>().Any(e => e.State == EntityState.Added))
            {
                _armed--;
                Fired++;
                await inject(cancellationToken);
            }
            return result;
        }
    }

    /// <summary>
    /// A lock-bypassing commit claiming the computed sequence number does not fail the
    /// write: <see cref="IMcpAuditWriter.WriteAsync"/> re-anchors the bundle to the
    /// now-current head (SequenceNo, PrevHash and EntryHash together) and retries.
    /// </summary>
    [Fact]
    public async Task WriteAsync_RetriesOnSequenceCollision_ReanchorsToNewHead()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        await using var _ = connection;
        var db2 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db2.Database.EnsureCreated();
        var writer2 = new McpAuditWriter(db2, NullLogger<McpAuditWriter>.Instance);
        var injector = new CollidingCommitInterceptor(ct => db2.SaveChangesAsync(ct));
        var db1 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(injector).Options);
        var writer = new McpAuditWriter(db1, NullLogger<McpAuditWriter>.Instance);
        var owner = Guid.NewGuid();

        await writer.WriteAsync(Request(owner: owner), CancellationToken.None); // chain head seq 1

        // The competing scope stages a bundle anchored on the same head — its commit is
        // not serialized by the writer's chain lock (staged saves run outside it).
        await writer2.StageAsync(Request(owner: owner,
            category: McpAuditCategory.ApprovalEvent, eventType: "ApprovalExecuted"), CancellationToken.None);
        injector.Arm(1);

        await writer.WriteAsync(Request(owner: owner), CancellationToken.None);

        var entries = await db1.McpAuditEntries.AsNoTracking()
            .Where(e => e.OwnerUserId == owner).OrderBy(e => e.SequenceNo)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1, 2, 3 }, entries.Select(e => e.SequenceNo).ToArray());
        Assert.Equal("ApprovalExecuted", entries[1].EventType); // the lock-bypassing commit won seq 2
        Assert.Equal(1, injector.Fired);
        Assert.Equal(entries[1].EntryHash, entries[2].PrevHash); // retry re-anchored to the new head

        var verification = await writer.VerifyAsync(owner, CancellationToken.None);
        Assert.True(verification.IsIntact, verification.FailureReason);
    }

    /// <summary>
    /// The retry is bounded: a chain that keeps colliding fails after the last attempt
    /// instead of silently dropping the audit record (F-0301).
    /// </summary>
    [Fact]
    public async Task WriteAsync_FailsAfterBoundedRetries_WhenCollisionsPersist()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        await using var _ = connection;
        var db2 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db2.Database.EnsureCreated();
        var owner = Guid.NewGuid();
        var injector = new CollidingCommitInterceptor(async ct =>
        {
            // Every firing stages and commits a fresh bundle on the current head — the
            // competing writer always wins the just-computed sequence number again.
            var head = await db2.McpAuditEntries.AsNoTracking()
                .Where(e => e.OwnerUserId == owner)
                .OrderByDescending(e => e.SequenceNo)
                .Select(e => new { e.SequenceNo, e.EntryHash })
                .FirstOrDefaultAsync(ct);
            db2.McpAuditEntries.Add(new McpAuditEntry(
                DateTime.UtcNow, (head?.SequenceNo ?? 0) + 1, McpAuditCategory.ApprovalEvent,
                "ApprovalExecuted", "Success", new string('c', 64),
                ownerUserId: owner, prevHash: head?.EntryHash));
            await db2.SaveChangesAsync(ct);
        });
        var db1 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(injector).Options);
        var writer = new McpAuditWriter(db1, NullLogger<McpAuditWriter>.Instance);
        injector.Arm(10); // more collisions than the bounded retry budget

        await Assert.ThrowsAsync<DbUpdateException>(
            () => writer.WriteAsync(Request(owner: owner), CancellationToken.None));

        Assert.Equal(3, injector.Fired); // initial attempt + bounded retries, then it fails
        Assert.Equal(0, await db1.McpAuditEntries
            .CountAsync(e => e.EventType == "ToolCall", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>An intact chain verifies clean — including after a full reload from disk.</summary>
    [Fact]
    public async Task VerifyAsync_IntactChain_SurvivesReload()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();
        var changes = new[] { new McpAuditFieldChange("Posting", Guid.NewGuid(), "ContactId", null, Guid.NewGuid().ToString()) };

        await writer.WriteAsync(Request(owner: owner), CancellationToken.None);
        await writer.WriteAsync(Request(owner: owner, eventType: "DataChange", changes: changes), CancellationToken.None);
        await writer.WriteAsync(Request(owner: owner, eventType: "AccessRevoked"), CancellationToken.None);

        // Fresh context over the same database: hash recomputation must work on reloaded
        // values (tick round-trip, canonical ordering of the loaded change rows).
        var fresh = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn).Options);
        var verifier = new McpAuditWriter(fresh, NullLogger<McpAuditWriter>.Instance);

        var result = await verifier.VerifyAsync(owner, CancellationToken.None);

        Assert.True(result.IsIntact, result.FailureReason);
        Assert.Equal(3, result.EntriesChecked);
        Assert.Null(result.FirstBrokenSequenceNo);
        Assert.Empty(result.SequenceGaps);
    }

    /// <summary>A deleted middle entry shows up as both a sequence gap and a broken link.</summary>
    [Fact]
    public async Task VerifyAsync_DetectsGapFromDeletedEntry()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            await writer.WriteAsync(Request(owner: owner), CancellationToken.None);
        }

        // Simulated tampering: remove the middle entry of the chain.
        await db.McpAuditEntries.Where(e => e.SequenceNo == 2).ExecuteDeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        var result = await writer.VerifyAsync(owner, CancellationToken.None);

        Assert.False(result.IsIntact);
        var gap = Assert.Single(result.SequenceGaps);
        Assert.Equal(2, gap.FirstMissingSequenceNo);
        Assert.Equal(2, gap.LastMissingSequenceNo);
        Assert.Equal(3, result.FirstBrokenSequenceNo); // dangling PrevHash of entry 3
        Assert.NotNull(result.FailureReason);
    }

    /// <summary>Retroactively modified content breaks the recomputed entry hash.</summary>
    [Fact]
    public async Task VerifyAsync_DetectsTamperedContent()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            await writer.WriteAsync(Request(owner: owner), CancellationToken.None);
        }

        // Simulated tampering: alter the payload of entry 2 without fixing the chain.
        await db.McpAuditEntries.Where(e => e.SequenceNo == 2)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.PayloadJson, "{\"tampered\":true}"), cancellationToken: TestContext.Current.CancellationToken);

        var result = await writer.VerifyAsync(owner, CancellationToken.None);

        Assert.False(result.IsIntact);
        Assert.Equal(2, result.FirstBrokenSequenceNo);
        Assert.Contains("hash", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A forged chain link (PrevHash rewrite) is detected.</summary>
    [Fact]
    public async Task VerifyAsync_DetectsForgedPrevHash()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
        {
            await writer.WriteAsync(Request(owner: owner), CancellationToken.None);
        }

        await db.McpAuditEntries.Where(e => e.SequenceNo == 2)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.PrevHash, "deadbeef"), cancellationToken: TestContext.Current.CancellationToken);

        var result = await writer.VerifyAsync(owner, CancellationToken.None);

        Assert.False(result.IsIntact);
        Assert.Equal(2, result.FirstBrokenSequenceNo);
    }

    /// <summary>Verification is chain-scoped: owner verification ignores the global chain.</summary>
    [Fact]
    public async Task VerifyAsync_IsScopedToTheRequestedChain()
    {
        var (writer, db, conn) = CreateSqlite();
        await using var _ = conn;
        var owner = Guid.NewGuid();

        await writer.WriteAsync(Request(globalChain: true), CancellationToken.None);
        await writer.WriteAsync(Request(owner: owner), CancellationToken.None);

        var globalResult = await writer.VerifyAsync(null, CancellationToken.None);
        var ownerResult = await writer.VerifyAsync(owner, CancellationToken.None);

        Assert.True(globalResult.IsIntact);
        Assert.Equal(1, globalResult.EntriesChecked);
        Assert.True(ownerResult.IsIntact);
        Assert.Equal(1, ownerResult.EntriesChecked);
    }

    /// <summary>An empty chain verifies as intact.</summary>
    [Fact]
    public async Task VerifyAsync_EmptyChain_IsIntact()
    {
        var (writer, _, conn) = CreateSqlite();
        await using var _ = conn;

        var result = await writer.VerifyAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsIntact);
        Assert.Equal(0, result.EntriesChecked);
    }
}
