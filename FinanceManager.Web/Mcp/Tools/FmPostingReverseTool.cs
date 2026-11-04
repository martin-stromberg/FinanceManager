using FinanceManager.Application.Mcp;
using FinanceManager.Application.Postings;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the approval-bound tool <c>fm_posting_reverse</c> (T31): reverses a posting
/// by creating counter-postings across the whole posting group — always via the persistent
/// approval queue, never synchronously. Scope: (Buchungen, Löschung), normalized as reversal.
/// </summary>
public sealed class FmPostingReverseDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_posting_reverse";

    /// <summary>Creates the descriptor.</summary>
    public FmPostingReverseDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"postingId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Posting to reverse (required); the whole group is counter-booked.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted, stored as audit field).\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"}},\"required\":[\"postingId\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Reverses a posting: creates counter-postings and links reversal references, transactionally across the whole posting group (check reversibility first via fm_posting_reversal_check). Always requires approval: the synchronous answer is the approval request id, the outcome is reported via fm_task_status. Scope: (Buchungen, Löschung) normalized as reversal.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Genehmigungspflichtig;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Postings, McpRightKind.Delete)
    };
}

/// <summary>
/// Handler of <c>fm_posting_reverse</c> (F-1003): runs <see cref="IPostingReversalService.CanReverseAsync"/>
/// in both phases — the request creation (interactive fallback path) and, normatively, the
/// queue execution — so a posting whose situation changed between approval and execution
/// fails classified (<c>Err_AlreadyReversed</c>/<c>Err_ValidationFailed</c>) instead of a
/// double reversal. Missing/foreign postings collapse to <c>Err_TargetGone</c> at execution
/// (404 semantics). The reversal itself uses the regular
/// <see cref="IPostingReversalService.ReversePostingAsync"/> path and commits atomically
/// with its staged <c>DataChange</c> record (F-0301); the client reason text is kept as
/// untrusted audit-field content only.
/// </summary>
public sealed class FmPostingReverseHandler : IMcpToolHandler
{
    private const string EntityType = "Posting";

    private readonly IPostingReversalService _reversals;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public FmPostingReverseHandler(IPostingReversalService reversals, IMcpAuditWriter auditWriter)
    {
        _reversals = reversals;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public string ToolName => FmPostingReverseDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var postingId = McpToolArguments.RequiredGuid(arguments, "postingId");
        var reasonText = McpToolArguments.OptionalString(arguments, "reasonText", 500);

        // CanReverseAsync pre-check (F-1003): runs at request creation on the interactive
        // path and again at queue execution — a changed situation fails classified.
        var validation = await _reversals.CanReverseAsync(postingId, owner, ct);
        if (!validation.IsValid)
        {
            if (context.Mode == McpToolExecutionMode.ApprovedExecution)
            {
                ThrowClassified(postingId, validation.Errors);
            }
            // Interactive path: the pre-check already failed — a request would be futile.
            ThrowClassified(postingId, validation.Errors);
        }

        if (context.Mode != McpToolExecutionMode.ApprovedExecution)
        {
            return new
            {
                requiresApproval = true,
                code = "Err_Mcp_ApprovalRequired",
                operation = "reverse",
                effect = "Reverses the posting: counter-postings are created for the whole " +
                    "posting group and reversal references are linked. The reversal is final.",
                parameters = new { postingId, reasonText },
                message = "Reversing a posting is final and requires owner approval."
            };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "PostingReversed", ToolName,
            new List<McpAuditFieldChange>
            {
                new(EntityType, postingId, "Reversed", null, reasonText)
            }, 1,
            JsonSerializer.Serialize(new { operation = "reverse", postingId, reason = reasonText }), ct);
        FinanceManager.Shared.Dtos.Postings.ReversalResultDto result;
        try
        {
            result = await _reversals.ReversePostingAsync(postingId, owner, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_ValidationFailed", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new
        {
            operation = "reverse",
            applied = true,
            postingId,
            reversedPostingIds = result.ReversedPostingIds,
            createdReversalIds = result.CreatedReversalIds,
            statementDraftId = result.StatementDraftId
        };
    }

    /// <summary>Maps the validation outcome to the classified failure reason (§6.2).</summary>
    private static void ThrowClassified(Guid postingId, IReadOnlyList<string> errors)
    {
        var joined = string.Join("; ", errors);
        if (errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || e.Contains("not authorized", StringComparison.OrdinalIgnoreCase)))
        {
            throw new McpToolException("Err_TargetGone", "The posting no longer exists.");
        }
        if (errors.Any(e => e.Contains("already", StringComparison.OrdinalIgnoreCase)
            && e.Contains("revers", StringComparison.OrdinalIgnoreCase)))
        {
            throw new McpToolException("Err_AlreadyReversed",
                $"The posting was already reversed: {joined}");
        }
        throw new McpToolException("Err_ValidationFailed",
            $"The posting cannot be reversed: {joined}");
    }
}
