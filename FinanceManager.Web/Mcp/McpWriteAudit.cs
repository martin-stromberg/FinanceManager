using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Shared audit plumbing of the write-class tool adapters (EP-07+): stages the
/// <see cref="McpAuditCategory.DataChange"/> record of a write effect in the shared unit of
/// work so it commits atomically with the business change inside the called
/// <c>I*Service</c> save (F-0301: no effect without its record, no record without its
/// effect) and discards a staged bundle again when the effect fails before its save.
/// </summary>
internal static class McpWriteAudit
{
    /// <summary>
    /// Stages one <see cref="McpAuditCategory.DataChange"/> record (plus field deltas) in
    /// the shared <see cref="AppDbContext"/> without saving. The caller must invoke the
    /// mutating service method next; its <c>SaveChangesAsync</c> commits both atomically.
    /// </summary>
    /// <param name="writer">The audit writer of the request scope.</param>
    /// <param name="context">The tool call context.</param>
    /// <param name="eventType">Stable event type code (e.g. <c>DraftImported</c>).</param>
    /// <param name="toolName">The calling <c>fm_*</c> tool.</param>
    /// <param name="changes">Field deltas (before/after) of the effect.</param>
    /// <param name="affectedCount">Optional number of affected records.</param>
    /// <param name="payloadJson">Optional graduated parameter snapshot (never binary content).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The staged entry, or <see langword="null"/> for documented no-op writers.</returns>
    public static Task<McpAuditEntry?> StageDataChangeAsync(
        IMcpAuditWriter writer,
        McpToolCallContext context,
        string eventType,
        string toolName,
        IReadOnlyList<McpAuditFieldChange> changes,
        int? affectedCount,
        string? payloadJson,
        CancellationToken ct)
    {
        var access = context.Access.Access;
        return writer.StageAsync(new McpAuditWriteRequest(
            Category: McpAuditCategory.DataChange,
            EventType: eventType,
            ResultStatus: "Success",
            OwnerUserId: context.Access.OwnerUserId,
            McpAccessId: access?.Id,
            McpAccessName: access?.Name,
            ToolName: toolName,
            Context: "McpEndpoint",
            CorrelationId: context.CorrelationId,
            SourceIp: context.SourceIp,
            AffectedCount: affectedCount,
            PayloadJson: payloadJson,
            Changes: changes), ct);
    }

    /// <summary>
    /// Detaches a previously staged audit bundle (entry plus its field-delta rows) when the
    /// business effect failed before committing — otherwise the next
    /// <c>SaveChangesAsync</c> of the shared scope (e.g. the dispatcher's ToolCall record)
    /// would persist an audit record for an effect that never happened.
    /// </summary>
    /// <param name="context">The tool call context (provides the request services).</param>
    /// <param name="staged">The staged entry returned by <see cref="StageDataChangeAsync"/>.</param>
    public static void DiscardStaged(McpToolCallContext context, McpAuditEntry? staged)
    {
        if (staged is null)
        {
            return;
        }
        var db = context.Services.GetService<AppDbContext>();
        if (db is null)
        {
            return;
        }
        foreach (var change in staged.Changes)
        {
            if (db.Entry(change).State != EntityState.Detached)
            {
                db.Entry(change).State = EntityState.Detached;
            }
        }
        if (db.Entry(staged).State != EntityState.Detached)
        {
            db.Entry(staged).State = EntityState.Detached;
        }
    }
}
