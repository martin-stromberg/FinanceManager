using FinanceManager.Application.Mcp;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_statement_draft_validate</c> (T14): plausibility check
/// of a statement draft or a single entry — returns the pipeline
/// <c>DraftValidationResultDto</c> (warnings/errors). Scope: (Kontoauszugsentwürfe, Lesen).
/// </summary>
public sealed class FmStatementDraftValidateDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_validate";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftValidateDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to validate (required).\"},\"entryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single entry restriction.\"}},\"required\":[\"draftId\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Validates a statement draft (or one entry): pipeline validation result with warnings/errors. Scope: (Kontoauszugsentwürfe, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_validate</c>: adapts
/// <see cref="IStatementDraftService.ValidateAsync"/>. Validation messages are
/// system-generated findings (codes/severities are trusted); message texts can embed record
/// fragments and are therefore emitted inside the marked <c>untrusted_data</c> wrapper.
/// A foreign or filtered draft yields a null result (no forbidden/not-found distinction).
/// </summary>
public sealed class FmStatementDraftValidateHandler : IMcpToolHandler
{
    private readonly IStatementDraftService _drafts;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftValidateHandler(IStatementDraftService drafts, McpRecordScopeFilter recordFilter)
    {
        _drafts = drafts;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftValidateDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.RequiredGuid(arguments, "draftId");
        var entryId = McpToolArguments.OptionalGuid(arguments, "entryId");

        // Record-level check first: a filtered/non-owned draft is invisible.
        var header = await _drafts.GetDraftHeaderAsync(draftId, owner, ct);
        if (header is null || !await _recordFilter.IsVisibleAsync(
                context.Access, McpDataType.StatementDrafts,
                new McpRecordContext { DraftId = draftId }, ct))
        {
            return new { draftId, validation = (object?)null };
        }

        var result = await _drafts.ValidateAsync(draftId, entryId, owner, ct);
        return new
        {
            draftId = result.DraftId,
            validation = new
            {
                isValid = result.IsValid,
                budgetImpact = result.BudgetImpact,
                messages = result.Messages.Select(m => new
                {
                    code = m.Code,
                    severity = m.Severity,
                    draftId = m.DraftId,
                    entryId = m.EntryId,
                    relatedRecordKind = m.RelatedRecordKind,
                    relatedRecordId = m.RelatedRecordId,
                    untrusted_data = new { message = m.Message }
                }).ToList()
            }
        };
    }
}
