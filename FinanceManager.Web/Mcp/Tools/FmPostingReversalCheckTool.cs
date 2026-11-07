using FinanceManager.Application.Mcp;
using FinanceManager.Application.Postings;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_posting_reversal_check</c> (T30): checks whether a
/// posting can be reversed and shows the affected group (same GroupId) — the decision
/// template for a later approval-gated reversal. Scope: (Buchungen, Lesen).
/// </summary>
public sealed class FmPostingReversalCheckDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_posting_reversal_check";

    /// <summary>Creates the descriptor.</summary>
    public FmPostingReversalCheckDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"postingId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Posting whose reversibility is checked (required).\"}},\"required\":[\"postingId\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Checks posting reversibility and lists the affected posting group (decision template for a reversal). Scope: (Buchungen, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Postings, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_posting_reversal_check</c>: adapts
/// <see cref="IPostingReversalService.CanReverseAsync"/> and
/// <see cref="IPostingReversalService.GetRelatedPostingsAsync"/>. Postings that do not exist
/// or do not belong to the access owner are normalized to <c>found: false</c> — the
/// service's distinct "not found"/"not authorized" reasons are deliberately collapsed so
/// no existence information leaks (404 semantics). Posting texts of the related group are
/// untrusted content (<c>untrusted_data</c>).
/// </summary>
public sealed class FmPostingReversalCheckHandler : IMcpToolHandler
{
    private readonly IPostingReversalService _reversals;
    private readonly IMcpScopeEvaluator _scopeEvaluator;

    /// <summary>Creates the handler.</summary>
    public FmPostingReversalCheckHandler(IPostingReversalService reversals, IMcpScopeEvaluator scopeEvaluator)
    {
        _reversals = reversals;
        _scopeEvaluator = scopeEvaluator;
    }

    /// <inheritdoc />
    public string ToolName => FmPostingReversalCheckDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var postingId = McpToolArguments.RequiredGuid(arguments, "postingId");

        var validation = await _reversals.CanReverseAsync(postingId, owner, ct);

        // Normalize the existence outcomes: "not found" and "not authorized" are collapsed
        // so the client cannot distinguish a foreign posting from a missing one.
        var inaccessible = validation.Errors.Any(e =>
            e.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || e.Contains("not authorized", StringComparison.OrdinalIgnoreCase));
        if (inaccessible)
        {
            return new
            {
                postingId,
                found = false,
                canReverse = false,
                errors = Array.Empty<string>(),
                relatedPostings = Array.Empty<object>()
            };
        }

        var related = await _reversals.GetRelatedPostingsAsync(postingId, ct);
        var access = context.Access.Access
            ?? throw new McpToolException("Err_Mcp_Unauthorized", "No MCP access context.");
        var relatedOut = new List<object>(related.Count);
        foreach (var p in related)
        {
            // Full record-level check (stages 0–3): a group's related postings may anchor
            // to aggregates the access owner cannot see — those entries are dropped.
            if (!await RelatedVisibleAsync(p, access, ct))
            {
                continue;
            }
            relatedOut.Add(new
            {
                id = p.Id,
                kind = p.Kind.ToString(),
                bookingDate = p.BookingDate,
                valutaDate = p.ValutaDate,
                amount = p.Amount,
                isReversed = p.ReversedByPostingId.HasValue,
                isReversal = p.ReversalForPostingId.HasValue,
                untrusted_data = new
                {
                    subject = p.Subject,
                    recipientName = p.RecipientName
                }
            });
        }

        return new
        {
            postingId,
            found = true,
            canReverse = validation.IsValid,
            errors = validation.Errors,
            relatedPostings = relatedOut
        };
    }

    private async Task<bool> RelatedVisibleAsync(
        FinanceManager.Domain.Postings.Posting posting, FinanceManager.Domain.Mcp.McpAccess access, CancellationToken ct)
    {
        var record = new McpRecordContext
        {
            AccountId = posting.Kind == FinanceManager.Shared.Dtos.Postings.PostingKind.Bank ? posting.AccountId : null,
            ContactId = posting.Kind == FinanceManager.Shared.Dtos.Postings.PostingKind.Contact ? posting.ContactId : null,
            SavingsPlanId = posting.Kind == FinanceManager.Shared.Dtos.Postings.PostingKind.SavingsPlan ? posting.SavingsPlanId : null,
            SecurityId = posting.Kind == FinanceManager.Shared.Dtos.Postings.PostingKind.Security ? posting.SecurityId : null
        };
        var verdict = await _scopeEvaluator.CheckRecordAccessAsync(
            access, McpDataType.Postings, McpRightKind.Read, record, ct);
        return verdict == McpAccessVerdict.Allowed;
    }
}
