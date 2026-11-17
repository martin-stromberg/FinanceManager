using FinanceManager.Application.Mcp;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the approval-bound tool <c>fm_statement_draft_discard</c> (T21): discards
/// one open statement draft — or every open draft of the owner when <c>all</c> plus the
/// explicit confirmation mark <c>confirmDiscardAll</c> are set — always via the persistent
/// approval queue, never synchronously. Scope: (Kontoauszugsentwürfe, Löschung).
/// </summary>
public sealed class FmStatementDraftDiscardDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_discard";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftDiscardDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to discard (required unless all).\"},\"all\":{\"type\":\"boolean\",\"description\":\"Discard every open draft of the owner (requires confirmDiscardAll).\"},\"confirmDiscardAll\":{\"type\":\"boolean\",\"description\":\"Explicit confirmation mark required for all=true.\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Discards one open statement draft (draftId) or every open draft (all=true plus confirmDiscardAll=true). Always requires approval: the synchronous answer is the approval request id, the outcome is reported via fm_task_status. Scope: (Kontoauszugsentwürfe, Löschung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Genehmigungspflichtig;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Delete)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_discard</c> (F-1002): interactive calls answer the
/// structured <c>requiresApproval</c> marker (defence in depth — the class gate queues
/// every call). The queue execution re-resolves the draft and runs the regular
/// <see cref="IStatementDraftService.CancelAsync"/> — or <c>DeleteAllAsync</c> for the
/// confirmed mass discard. A vanished draft fails classified (<c>Err_TargetGone</c>); each
/// effect commits atomically with its staged <c>DataChange</c> record (F-0301).
/// </summary>
public sealed class FmStatementDraftDiscardHandler : IMcpToolHandler
{
    private const string EntityType = "StatementDraft";

    private readonly IStatementDraftService _drafts;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftDiscardHandler(IStatementDraftService drafts, IMcpAuditWriter auditWriter)
    {
        _drafts = drafts;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftDiscardDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.OptionalGuid(arguments, "draftId");
        var all = McpToolArguments.OptionalBool(arguments, "all") ?? false;
        var confirmAll = McpToolArguments.OptionalBool(arguments, "confirmDiscardAll") ?? false;

        if (all && draftId.HasValue)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'all' cannot be combined with 'draftId'.");
        }
        if (all && !confirmAll)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Discarding all open drafts requires 'confirmDiscardAll' = true.");
        }
        if (!all && !draftId.HasValue)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'draftId' is required.");
        }

        if (context.Mode != McpToolExecutionMode.ApprovedExecution)
        {
            return new
            {
                requiresApproval = true,
                code = "Err_Mcp_ApprovalRequired",
                operation = all ? "discardAll" : "discard",
                effect = all
                    ? "Discards every open statement draft of the owner — the drafts and their unbooked entries are removed."
                    : "Discards the statement draft — the draft and its unbooked entries are removed.",
                parameters = new { draftId, all, confirmDiscardAll = all ? (bool?)confirmAll : null },
                message = "Discarding is final and requires owner approval."
            };
        }

        if (all)
        {
            // Batch effect over every open draft: resolve the drafts first so the delta
            // rows anchor on their real draft ids — the audit writer rejects the former
            // Guid.Empty placeholder of the aggregated batch event.
            var open = await _drafts.GetOpenDraftsAsync(owner, ct);
            var stagedAll = await McpWriteAudit.StageDataChangeAsync(
                _auditWriter, context, "DraftsDiscardedAll", ToolName,
                open.Select(d => new McpAuditFieldChange(
                        EntityType, d.DraftId, "Discarded", d.Status.ToString(), null))
                    .ToList(),
                open.Count,
                JsonSerializer.Serialize(new { operation = "discardAll" }), ct);
            int count;
            try
            {
                count = await _drafts.DeleteAllAsync(owner, ct);
            }
            catch
            {
                McpWriteAudit.DiscardStaged(context, stagedAll);
                throw;
            }
            return new { operation = "discardAll", applied = true, discardedCount = count };
        }

        var header = await _drafts.GetDraftHeaderAsync(draftId!.Value, owner, ct);
        if (header is null)
        {
            throw new McpToolException("Err_TargetGone", "The draft no longer exists.");
        }
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftDiscarded", ToolName,
            new List<McpAuditFieldChange>
            {
                new(EntityType, draftId.Value, "Discarded", header.Status.ToString(), null)
            }, 1,
            JsonSerializer.Serialize(new { operation = "discard", draftId }), ct);
        bool cancelled;
        try
        {
            cancelled = await _drafts.CancelAsync(draftId.Value, owner, ct);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (!cancelled)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_TargetGone", "The draft could not be discarded.");
        }
        return new { operation = "discard", applied = true, draftId };
    }
}
