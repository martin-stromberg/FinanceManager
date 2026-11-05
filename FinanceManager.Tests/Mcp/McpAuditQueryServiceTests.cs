using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of <see cref="McpAuditQueryService"/> (F-0601/F-0603): strict owner isolation,
/// AND-combined filters, paging clamp, detail incl. field deltas, export projection incl. the
/// <c>AuditExported</c> self-event, the per-access usage aggregation and the chain
/// verification delegation to the hash-chained writer.
/// </summary>
public sealed class McpAuditQueryServiceTests
{
    private static (McpAuditQueryService svc, AppDbContext db, McpAuditWriter writer) Create()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        var writer = new McpAuditWriter(db, NullLogger<McpAuditWriter>.Instance);
        var svc = new McpAuditQueryService(db, writer, NullLogger<McpAuditQueryService>.Instance);
        return (svc, db, writer);
    }

    private static McpAccess Access(Guid owner, string name = "acc")
        => new(owner, name, $"hint-{Guid.NewGuid():N}"[..20], "pbkdf2|1|s|k",
            McpAccessLevel.Analyse, McpApprovalMode.PerAction);

    private static McpAuditWriteRequest ToolCall(
        Guid owner, Guid accessId, string? tool = "fm_postings_query",
        string result = "Success", Guid? correlationId = null,
        string? accessName = "acc")
        => new(McpAuditCategory.ToolCall, "ToolCall", result,
            OwnerUserId: owner, McpAccessId: accessId, McpAccessName: accessName,
            ToolName: tool, CatalogVersion: "katalog-v1", Context: "McpEndpoint",
            CorrelationId: correlationId, SourceIp: "10.0.0.1", DurationMs: 42,
            PayloadJson: "{\"q\":\"x\"}");

    /// <summary>Seeds a small scenario: owner has two accesses with calls; a foreign owner has one.</summary>
    private static async Task<(Guid owner, Guid other, McpAccess a1, McpAccess a2)> SeedAsync(
        McpAuditQueryService _, AppDbContext db, McpAuditWriter writer)
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var a1 = Access(owner, "acc-1");
        var a2 = Access(owner, "acc-2");
        var foreign = Access(other, "foreign");
        db.McpAccesses.AddRange(a1, a2, foreign);
        await db.SaveChangesAsync();

        // owner chain: 2 success calls on a1, 1 denied + 1 failed on a2, 1 access event
        await writer.WriteAsync(ToolCall(owner, a1.Id, "fm_postings_query", "Success", Guid.NewGuid(), a1.Name), CancellationToken.None);
        await writer.WriteAsync(ToolCall(owner, a1.Id, "fm_accounts_overview", "Success", null, a1.Name), CancellationToken.None);
        await writer.WriteAsync(ToolCall(owner, a2.Id, "fm_postings_query", "Denied", null, a2.Name), CancellationToken.None);
        await writer.WriteAsync(ToolCall(owner, a2.Id, "fm_budget_overview", "Failed", null, a2.Name), CancellationToken.None);
        await writer.WriteAsync(new McpAuditWriteRequest(
            McpAuditCategory.AccessEvent, "AccessCreated", "Success",
            OwnerUserId: owner, McpAccessId: a1.Id, McpAccessName: a1.Name, Context: "Ui"), CancellationToken.None);

        // foreign chain must stay invisible
        await writer.WriteAsync(ToolCall(other, foreign.Id, "fm_postings_query", "Success", null, foreign.Name), CancellationToken.None);
        return (owner, other, a1, a2);
    }

    /// <summary>Owner isolation: list/detail never return foreign rows.</summary>
    [Fact]
    public async Task List_OnlyReturnsOwnEntries()
    {
        var (svc, db, writer) = Create();
        var (owner, _, _, _) = await SeedAsync(svc, db, writer);

        var list = await svc.ListAsync(owner, new McpAuditQueryFilter(), 0, 100, CancellationToken.None);

        Assert.Equal(5, list.Count);
        Assert.All(list, e => Assert.True(e.Category >= 0));
        // newest first
        Assert.True(list[0].Utc >= list[^1].Utc);
    }

    /// <summary>Filters combine with AND semantics (category + access + result + tool + correlation + time range).</summary>
    [Fact]
    public async Task List_AppliesAllFilters_WithAndSemantics()
    {
        var (svc, db, writer) = Create();
        var (owner, _, a1, a2) = await SeedAsync(svc, db, writer);

        // category
        var toolCalls = await svc.ListAsync(owner,
            new McpAuditQueryFilter(Category: (short)McpAuditCategory.ToolCall), 0, 100, CancellationToken.None);
        Assert.Equal(4, toolCalls.Count);
        var accessEvents = await svc.ListAsync(owner,
            new McpAuditQueryFilter(Category: (short)McpAuditCategory.AccessEvent), 0, 100, CancellationToken.None);
        Assert.Single(accessEvents);

        // access
        var a1Only = await svc.ListAsync(owner,
            new McpAuditQueryFilter(McpAccessId: a1.Id), 0, 100, CancellationToken.None);
        Assert.Equal(3, a1Only.Count);

        // result
        var denied = await svc.ListAsync(owner,
            new McpAuditQueryFilter(ResultStatus: "Denied"), 0, 100, CancellationToken.None);
        Assert.Single(denied);
        Assert.Equal(a2.Id, denied[0].McpAccessId);

        // tool name
        var byTool = await svc.ListAsync(owner,
            new McpAuditQueryFilter(ToolName: "fm_budget_overview"), 0, 100, CancellationToken.None);
        Assert.Single(byTool);

        // correlation id
        var correlationCalls = await svc.ListAsync(owner,
            new McpAuditQueryFilter(), 0, 100, CancellationToken.None);
        var withCorr = correlationCalls.First(e => e.CorrelationId.HasValue);
        var corrFiltered = await svc.ListAsync(owner,
            new McpAuditQueryFilter(CorrelationId: withCorr.CorrelationId), 0, 100, CancellationToken.None);
        Assert.Single(corrFiltered);

        // time range excludes everything before/after
        var none = await svc.ListAsync(owner,
            new McpAuditQueryFilter(ToUtc: DateTime.UtcNow.AddDays(-1)), 0, 100, CancellationToken.None);
        Assert.Empty(none);
        var all = await svc.ListAsync(owner,
            new McpAuditQueryFilter(FromUtc: DateTime.UtcNow.AddDays(-1), ToUtc: DateTime.UtcNow.AddDays(1)),
            0, 100, CancellationToken.None);
        Assert.Equal(5, all.Count);
    }

    /// <summary>Paging clamps take to 200 and honours skip.</summary>
    [Fact]
    public async Task List_Paging_ClampedAndSkipped()
    {
        var (svc, db, writer) = Create();
        var (owner, _, _, _) = await SeedAsync(svc, db, writer);

        var page1 = await svc.ListAsync(owner, new McpAuditQueryFilter(), 0, 2, CancellationToken.None);
        var page2 = await svc.ListAsync(owner, new McpAuditQueryFilter(), 2, 2, CancellationToken.None);
        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.DoesNotContain(page2, p => page1.Any(x => x.Id == p.Id));

        var oversized = await svc.ListAsync(owner, new McpAuditQueryFilter(), 0, 500, CancellationToken.None);
        Assert.Equal(5, oversized.Count); // clamp does not invent rows
    }

    /// <summary>Detail returns payload + field deltas; foreign entry id → null.</summary>
    [Fact]
    public async Task Get_DetailWithChanges_AndForeignIsolation()
    {
        var (svc, db, writer) = Create();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var access = Access(owner);
        db.McpAccesses.Add(access);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entry = await writer.StageAsync(new McpAuditWriteRequest(
            McpAuditCategory.DataChange, "EntriesModified", "Success",
            OwnerUserId: owner, McpAccessId: access.Id, McpAccessName: access.Name,
            Context: "McpEndpoint", PayloadJson: "{\"batch\":1}",
            Changes: new[]
            {
                new McpAuditFieldChange("StatementDraftEntry", Guid.NewGuid(), "ContactId", null, "abc"),
                new McpAuditFieldChange("StatementDraftEntry", Guid.NewGuid(), "BookingDate", "2026-01-01", "2026-01-02")
            }), CancellationToken.None);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(entry);

        var detail = await svc.GetAsync(owner, entry!.Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal("{\"batch\":1}", detail!.PayloadJson);
        Assert.Equal(2, detail.Changes.Count);
        // detail rows are canonically ordered (EntityType, Field) — deterministic drilling order
        Assert.Equal("BookingDate", detail.Changes[0].Field);
        Assert.Equal("ContactId", detail.Changes[1].Field);
        Assert.False(string.IsNullOrWhiteSpace(detail.EntryHash));

        // foreign owner cannot drill into the same id
        Assert.Null(await svc.GetAsync(other, entry.Id, CancellationToken.None));
    }

    /// <summary>Verify delegates to the chained writer and reports an intact owner chain.</summary>
    [Fact]
    public async Task Verify_ReturnsIntact_ForSeededChain()
    {
        var (svc, db, writer) = Create();
        var (owner, _, _, _) = await SeedAsync(svc, db, writer);

        var result = await svc.VerifyAsync(owner, CancellationToken.None);

        Assert.True(result.IsIntact);
        Assert.Equal(5, result.EntriesChecked);
        Assert.Null(result.FirstBrokenSequenceNo);
        Assert.Empty(result.SequenceGaps);
    }

    /// <summary>Export returns full rows in chronological order and writes the AuditExported self-event.</summary>
    [Fact]
    public async Task Export_ReturnsFullRows_AndAuditsItself()
    {
        var (svc, db, writer) = Create();
        var (owner, _, _, _) = await SeedAsync(svc, db, writer);
        var before = await svc.ListAsync(owner, new McpAuditQueryFilter(), 0, 100, CancellationToken.None);

        var rows = await svc.ExportAsync(owner, new McpAuditQueryFilter(), 1000, CancellationToken.None);

        Assert.Equal(before.Count, rows.Count);
        Assert.True(rows[0].Utc <= rows[^1].Utc); // chronological
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.EntryHash)));

        // the export itself is now an audit event of the owner chain
        var after = await svc.ListAsync(owner, new McpAuditQueryFilter(), 0, 100, CancellationToken.None);
        Assert.Equal(before.Count + 1, after.Count);
        var exportEvent = after.First();
        Assert.Equal("AuditExported", exportEvent.EventType);
        Assert.Equal((short)McpAuditCategory.SecurityEvent, exportEvent.Category);

        // filtered export does not leak other rows (the AuditExported self-event is
        // written after the row capture, so it never contaminates its own export)
        var filtered = await svc.ExportAsync(owner,
            new McpAuditQueryFilter(ResultStatus: "Denied"), 1000, CancellationToken.None);
        Assert.Single(filtered);
        Assert.Equal("Denied", filtered[0].ResultStatus);
    }

    /// <summary>Usage aggregates calls of one access; foreign access id → null.</summary>
    [Fact]
    public async Task Usage_AggregatesCalls_PerAccess()
    {
        var (svc, db, writer) = Create();
        var (owner, other, a1, a2) = await SeedAsync(svc, db, writer);

        var usage = await svc.GetAccessUsageAsync(owner, a2.Id, 10, CancellationToken.None);
        Assert.NotNull(usage);
        Assert.Equal(2, usage!.TotalCalls);
        Assert.Equal(0, usage.SuccessCalls);
        Assert.Equal(2, usage.FailedCalls);
        Assert.Equal(1.0, usage.FailureRate, 3);
        Assert.Equal(2, usage.RecentCalls.Count);
        Assert.NotNull(usage.LastCallUtc);

        var usageA1 = await svc.GetAccessUsageAsync(owner, a1.Id, 10, CancellationToken.None);
        Assert.Equal(2, usageA1!.TotalCalls);
        Assert.Equal(0, usageA1.FailedCalls);
        Assert.Equal(2, usageA1.TopTools.Count);

        // foreign / unknown access → null (404 semantics)
        Assert.Null(await svc.GetAccessUsageAsync(other, a1.Id, 10, CancellationToken.None));
        Assert.Null(await svc.GetAccessUsageAsync(owner, Guid.NewGuid(), 10, CancellationToken.None));
    }

    /// <summary>CSV export emits the header plus one compact row per entry and quotes free text.</summary>
    [Fact]
    public void Csv_WritesHeaderRows_AndEscapes()
    {
        var entries = new[]
        {
            new FinanceManager.Shared.Dtos.Mcp.McpAuditEntryDetailDto(
                Guid.NewGuid(), new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), 1,
                (short)McpAuditCategory.ToolCall, "ToolCall", "Success",
                Guid.NewGuid(), "acc;name", "fm_postings_query", "katalog-v1", "McpEndpoint",
                Guid.NewGuid(), "10.0.0.1", 42, 7, null, null, null, "hash",
                Array.Empty<FinanceManager.Shared.Dtos.Mcp.McpAuditChangeDto>(),
                new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
        };

        var csv = McpAuditCsvExport.Write(entries);

        var lines = csv.Trim().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Utc;SequenceNo;", lines[0]);
        Assert.Contains("\"acc;name\"", lines[1]); // semicolon in free text is quoted
        Assert.Contains("fm_postings_query", lines[1]);
    }
}
