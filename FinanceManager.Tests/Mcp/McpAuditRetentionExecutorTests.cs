using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of <see cref="McpAuditRetentionExecutor"/> (F-1104): the tiered deletion
/// windows, the <c>RetentionPurged</c> anchor entry per truncated chain, the
/// "ab diesem Eintrag" verify mode of truncated chains and the terminal-request sweep.
/// </summary>
public sealed class McpAuditRetentionExecutorTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public AppDbContext Db { get; }
        public McpAuditWriter Writer { get; }
        public McpAuditRetentionExecutor Retention { get; }

        public Fixture(int toolCallMonths = 12, int approvalMonths = 36, int requestMonths = 12)
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            Db = new AppDbContext(options);
            Db.Database.EnsureCreated();
            Writer = new McpAuditWriter(Db, NullLogger<McpAuditWriter>.Instance);
            var monitor = new TestMonitor(new McpOptions
            {
                Retention = new McpRetentionOptions
                {
                    ToolCallMonths = toolCallMonths,
                    ApprovalMonths = approvalMonths,
                    RequestMonths = requestMonths
                }
            });
            Retention = new McpAuditRetentionExecutor(
                Db, Writer, monitor, NullLogger<McpAuditRetentionExecutor>.Instance);
        }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class TestMonitor : IOptionsMonitor<McpOptions>
    {
        public TestMonitor(McpOptions value) => CurrentValue = value;
        public McpOptions CurrentValue { get; }
        public McpOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<McpOptions, string?> listener) => null;
    }

    private static Task WriteAsync(McpAuditWriter writer, Guid? owner, McpAuditCategory category, string eventType)
        => writer.WriteAsync(new McpAuditWriteRequest(
            category, eventType, "Success", OwnerUserId: owner, Context: "Test"), CancellationToken.None);

    /// <summary>Retention removes short-tier entries beyond ToolCallMonths, keeps long-tier.</summary>
    [Fact]
    public async Task RunAsync_DeletesShortTier_KeepsLongTier()
    {
        using var f = new Fixture();
        var owner = Guid.NewGuid();
        await WriteAsync(f.Writer, owner, McpAuditCategory.ToolCall, "ToolCall");       // seq 1
        await WriteAsync(f.Writer, owner, McpAuditCategory.ApprovalEvent, "ApprovalRequested"); // seq 2
        await WriteAsync(f.Writer, owner, McpAuditCategory.ToolCall, "ToolCall");       // seq 3
        await WriteAsync(f.Writer, owner, McpAuditCategory.DataChange, "DataChange");   // seq 4

        var result = await f.Retention.RunAsync(DateTime.UtcNow.AddMonths(13), CancellationToken.None);

        Assert.Equal(2, result.EntriesRemoved);
        Assert.Equal(1, result.CutMarkersWritten);
        var remaining = await f.Db.McpAuditEntries.OrderBy(e => e.SequenceNo).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, remaining.Count); // ApprovalRequested, DataChange, RetentionPurged marker
        Assert.Equal(McpAuditCategory.ApprovalEvent, remaining[0].Category);
        Assert.Equal(McpAuditCategory.DataChange, remaining[1].Category);
        Assert.Equal(McpAuditRetentionExecutor.CutEventType, remaining[2].EventType);
        Assert.Contains("\"from\":1", remaining[2].PayloadJson);
    }

    /// <summary>A truncated chain stays verifiable via the documented-cut excuse.</summary>
    [Fact]
    public async Task RunAsync_TruncatedChain_StaysVerifiable()
    {
        using var f = new Fixture();
        var owner = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await WriteAsync(f.Writer, owner, McpAuditCategory.ToolCall, "ToolCall");
        }
        await WriteAsync(f.Writer, owner, McpAuditCategory.ApprovalEvent, "ApprovalExecuted"); // seq 6 stays
        for (var i = 0; i < 2; i++)
        {
            await WriteAsync(f.Writer, owner, McpAuditCategory.ToolCall, "ToolCall"); // seq 7-8
        }

        await f.Retention.RunAsync(DateTime.UtcNow.AddMonths(13), CancellationToken.None);

        var verify = await f.Writer.VerifyAsync(owner, CancellationToken.None);
        Assert.True(verify.IsIntact, verify.FailureReason);
        Assert.Empty(verify.SequenceGaps);
    }

    /// <summary>An undocumented gap (manual delete) is still reported as a breach.</summary>
    [Fact]
    public async Task VerifyAsync_UndocumentedGap_ReportsBreach()
    {
        using var f = new Fixture();
        var owner = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            await WriteAsync(f.Writer, owner, McpAuditCategory.ToolCall, "ToolCall");
        }
        // Tamper-style removal without a RetentionPurged marker.
        var middle = await f.Db.McpAuditEntries.SingleAsync(e => e.SequenceNo == 2, cancellationToken: TestContext.Current.CancellationToken);
        f.Db.McpAuditEntries.Remove(middle);
        await f.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var verify = await f.Writer.VerifyAsync(owner, CancellationToken.None);
        Assert.False(verify.IsIntact);
        Assert.NotNull(verify.FailureReason);
    }

    /// <summary>Entries inside the retention window are never touched — and no marker is written.</summary>
    [Fact]
    public async Task RunAsync_InsideWindow_DeletesNothing()
    {
        using var f = new Fixture();
        var owner = Guid.NewGuid();
        await WriteAsync(f.Writer, owner, McpAuditCategory.ToolCall, "ToolCall");
        await WriteAsync(f.Writer, owner, McpAuditCategory.SecurityEvent, "AuthFailed");

        var result = await f.Retention.RunAsync(DateTime.UtcNow.AddMonths(11), CancellationToken.None);

        Assert.Equal(0, result.EntriesRemoved);
        Assert.Equal(0, result.CutMarkersWritten);
        Assert.Equal(2, await f.Db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>The anchor entry itself is never removed by a later pass.</summary>
    [Fact]
    public async Task RunAsync_CutMarker_SurvivesLaterPasses()
    {
        using var f = new Fixture();
        var owner = Guid.NewGuid();
        await WriteAsync(f.Writer, owner, McpAuditCategory.ToolCall, "ToolCall");
        await f.Retention.RunAsync(DateTime.UtcNow.AddMonths(13), CancellationToken.None);
        Assert.Equal(1, await f.Db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        // Even a pass far beyond every window keeps the anchor (it is excluded from expiry).
        var result = await f.Retention.RunAsync(DateTime.UtcNow.AddMonths(50), CancellationToken.None);
        Assert.Equal(0, result.EntriesRemoved);
        Assert.Equal(1, await f.Db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        var verify = await f.Writer.VerifyAsync(owner, CancellationToken.None);
        Assert.True(verify.IsIntact, verify.FailureReason);
    }

    /// <summary>Terminal requests beyond RequestMonths are swept; open requests stay.</summary>
    [Fact]
    public async Task RunAsync_RemovesTerminalRequests_KeepsOpen()
    {
        using var f = new Fixture();
        var owner = Guid.NewGuid();
        var access = new McpAccess(owner, "acc", "HINT01", "pbkdf2|1|AA|AA",
            McpAccessLevel.Buchhalter, McpApprovalMode.PerAction);
        f.Db.McpAccesses.Add(access);

        var executed = AiActionRequest.CreatePreApproved(owner, access.Id, access.Name,
            "fm_x", "call", "katalog-v1", "{}", null, DateTime.UtcNow.AddDays(1),
            Guid.NewGuid(), "k1", "note");
        executed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow);
        executed.MarkExecuted("{}");
        var pending = new AiActionRequest(owner, access.Id, access.Name, "fm_y", "call",
            "katalog-v1", "{}", null, DateTime.UtcNow.AddDays(1), Guid.NewGuid(), "k2");
        f.Db.AiActionRequests.AddRange(executed, pending);
        await f.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await f.Retention.RunAsync(DateTime.UtcNow.AddMonths(13), CancellationToken.None);

        Assert.Equal(1, result.RequestsRemoved);
        var remaining = await f.Db.AiActionRequests.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(AiActionRequestStatus.Pending, remaining.Status);
    }

    /// <summary>Chains of different owners are truncated independently.</summary>
    [Fact]
    public async Task RunAsync_PerOwnerChains_Independent()
    {
        using var f = new Fixture();
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        await WriteAsync(f.Writer, ownerA, McpAuditCategory.ToolCall, "ToolCall");
        await WriteAsync(f.Writer, ownerB, McpAuditCategory.ApprovalEvent, "ApprovalRequested");

        var result = await f.Retention.RunAsync(DateTime.UtcNow.AddMonths(13), CancellationToken.None);

        Assert.Equal(1, result.EntriesRemoved); // only A's ToolCall
        Assert.Equal(2, await f.Db.McpAuditEntries.CountAsync(cancellationToken: TestContext.Current.CancellationToken)); // B's entry + A's marker
        var verifyB = await f.Writer.VerifyAsync(ownerB, CancellationToken.None);
        Assert.True(verifyB.IsIntact, verifyB.FailureReason);
        var verifyA = await f.Writer.VerifyAsync(ownerA, CancellationToken.None);
        Assert.True(verifyA.IsIntact, verifyA.FailureReason);
    }

    /// <summary>Range compression merges contiguous sequence runs.</summary>
    [Fact]
    public void CompressRanges_MergesContiguousRuns()
    {
        var ranges = McpAuditRetentionExecutor.CompressRanges([1, 2, 3, 5, 7, 8, 9]);
        Assert.Equal([(1L, 3L), (5L, 5L), (7L, 9L)], ranges);
    }
}
