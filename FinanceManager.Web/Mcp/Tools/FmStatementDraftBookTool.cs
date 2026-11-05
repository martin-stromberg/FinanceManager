using FinanceManager.Application;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Statements;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the approval-bound tool <c>fm_statement_draft_book</c> (T20): books a
/// statement draft (whole draft or a single entry), commits a preliminary draft into an
/// account (<c>accountId</c>+<c>importFormat</c>) or enqueues the mass booking of all open
/// drafts (<c>bookAll</c>) — always via the persistent approval queue, never synchronously.
/// Scope: (Kontoauszugsentwürfe, Buchung).
/// </summary>
public sealed class FmStatementDraftBookDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_book";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftBookDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to book (required unless bookAll).\"},\"entryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Restrict booking to a single entry.\"},\"bookAll\":{\"type\":\"boolean\",\"description\":\"Enqueue mass booking of all open drafts as a background task instead of a single draft.\"},\"accountId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Commit target account (commit mode, requires importFormat).\"},\"importFormat\":{\"type\":\"string\",\"description\":\"Import format name for commit mode (e.g. the parser format of the draft).\"},\"forceWarnings\":{\"type\":\"boolean\",\"description\":\"Book despite validation warnings.\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Books a statement draft or single entry (creates postings, statement import, balance and budget effects), commits a preliminary draft into an account (accountId+importFormat) or enqueues mass booking of all open drafts (bookAll) as a background task. Always requires approval: the synchronous answer is the approval request id, the outcome is reported via fm_task_status. Scope: (Kontoauszugsentwürfe, Buchung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Genehmigungspflichtig;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Book)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_book</c> (F-1002): interactive calls answer the
/// structured <c>requiresApproval</c> marker — the class-level gate already redirects
/// every call into an <c>AiActionRequest</c>, so the interactive path is a defence-in-depth
/// fallback only. The queue execution (<see cref="McpToolExecutionMode.ApprovedExecution"/>)
/// re-evaluates the draft state and performs the regular service effect:
/// <c>BookAsync</c> (draft/entry), <c>CommitAsync</c> (account+importFormat) or
/// <see cref="BackgroundTaskType.BookAllDrafts"/> via <see cref="IBackgroundTaskManager"/>.
/// A vanished draft fails classified (<c>Err_TargetGone</c>); a failed booking surfaces as
/// <c>Err_ValidationFailed</c>. Each effect commits atomically with its staged
/// <c>DataChange</c> record (F-0301).
/// </summary>
public sealed class FmStatementDraftBookHandler : IMcpToolHandler
{
    private const string EntityType = "StatementDraft";

    private readonly IStatementDraftService _drafts;
    private readonly IBackgroundTaskManager _taskManager;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftBookHandler(
        IStatementDraftService drafts,
        IBackgroundTaskManager taskManager,
        IMcpAuditWriter auditWriter)
    {
        _drafts = drafts;
        _taskManager = taskManager;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftBookDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.OptionalGuid(arguments, "draftId");
        var entryId = McpToolArguments.OptionalGuid(arguments, "entryId");
        var bookAll = McpToolArguments.OptionalBool(arguments, "bookAll") ?? false;
        var accountId = McpToolArguments.OptionalGuid(arguments, "accountId");
        var importFormat = McpToolArguments.OptionalString(arguments, "importFormat", 100);
        var forceWarnings = McpToolArguments.OptionalBool(arguments, "forceWarnings") ?? false;

        if (bookAll && (draftId.HasValue || entryId.HasValue || accountId.HasValue))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'bookAll' cannot be combined with draftId/entryId/accountId.");
        }
        if (!bookAll && !draftId.HasValue)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'draftId' is required.");
        }
        if ((accountId.HasValue ? 1 : 0) + (importFormat is not null ? 1 : 0) == 1)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Commit mode requires both 'accountId' and 'importFormat'.");
        }

        if (context.Mode != McpToolExecutionMode.ApprovedExecution)
        {
            // Defence in depth: the class gate queues every interactive call anyway — this
            // fallback keeps a synchronous effect impossible even on a direct handler call.
            return new
            {
                requiresApproval = true,
                code = "Err_Mcp_ApprovalRequired",
                operation = bookAll ? "bookAll" : accountId.HasValue ? "commit" : "book",
                effect = "Books the draft entries (creates postings, a statement import, " +
                    "balance and budget effects) or enqueues the mass booking of all open drafts.",
                parameters = new { draftId, entryId, bookAll, accountId, importFormat, forceWarnings },
                message = "Booking is final and requires owner approval."
            };
        }

        if (bookAll)
        {
            return await BookAllAsync(owner, context, forceWarnings, ct);
        }

        var header = await _drafts.GetDraftHeaderAsync(draftId!.Value, owner, ct);
        if (header is null)
        {
            throw new McpToolException("Err_TargetGone", "The draft no longer exists.");
        }

        return accountId.HasValue
            ? await CommitAsync(draftId.Value, owner, accountId.Value, importFormat!, context, ct)
            : await BookAsync(draftId.Value, entryId, owner, forceWarnings, context, ct);
    }

    private async Task<object> BookAsync(
        Guid draftId, Guid? entryId, Guid owner, bool forceWarnings,
        McpToolCallContext context, CancellationToken ct)
    {
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftBooked", ToolName,
            new List<McpAuditFieldChange>
            {
                new(EntityType, draftId, "Booked", null, entryId?.ToString() ?? "all")
            },
            null,
            JsonSerializer.Serialize(new { operation = "book", draftId, entryId, forceWarnings }), ct);
        BookingResult result;
        try
        {
            result = await _drafts.BookAsync(draftId, entryId, owner, forceWarnings, ct);
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
        if (!result.Success)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            var firstError = result.Validation.Messages
                .FirstOrDefault(m => string.Equals(m.Severity, "Error", StringComparison.OrdinalIgnoreCase));
            throw new McpToolException(
                "Err_ValidationFailed",
                firstError?.Message ?? "The draft could not be booked.");
        }
        return new
        {
            operation = "book",
            applied = true,
            draftId,
            entryId,
            statementImportId = result.StatementImportId,
            totalEntries = result.TotalEntries,
            nextDraftId = result.nextDraftId
        };
    }

    private async Task<object> CommitAsync(
        Guid draftId, Guid owner, Guid accountId, string importFormat,
        McpToolCallContext context, CancellationToken ct)
    {
        if (!Enum.TryParse<ImportFormat>(importFormat, ignoreCase: true, out var format))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument 'importFormat' must be one of {string.Join("|", Enum.GetNames<ImportFormat>())}.");
        }
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftCommitted", ToolName,
            new List<McpAuditFieldChange>
            {
                new(EntityType, draftId, "Committed", null, $"{accountId}|{format}")
            },
            null,
            JsonSerializer.Serialize(new { operation = "commit", draftId, accountId, importFormat = format.ToString() }), ct);
        CommitResult? result;
        try
        {
            result = await _drafts.CommitAsync(draftId, owner, accountId, format, ct);
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
        if (result is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_ValidationFailed", "The draft could not be committed.");
        }
        return new
        {
            operation = "commit",
            applied = true,
            draftId,
            statementImportId = result.StatementImportId,
            totalEntries = result.TotalEntries
        };
    }

    private async Task<object> BookAllAsync(
        Guid owner, McpToolCallContext context, bool forceWarnings, CancellationToken ct)
    {
        var existing = _taskManager.GetAll().FirstOrDefault(t =>
            t.UserId == owner && t.Type == BackgroundTaskType.BookAllDrafts
            && (t.Status == BackgroundTaskStatus.Running || t.Status == BackgroundTaskStatus.Queued));
        if (existing is not null)
        {
            return new
            {
                operation = "bookAll",
                applied = true,
                alreadyRunning = true,
                backgroundTaskId = existing.Id
            };
        }

        // Enqueue first, then stage the DataChange with the real id of the created
        // background task — bookAll touches every open draft through that task (there is
        // no single draft to anchor on), so the task record is the fachlich nächste
        // anchor. The audit writer rejects placeholder entity ids (Guid.Empty).
        var info = _taskManager.Enqueue(BackgroundTaskType.BookAllDrafts, owner,
            new { IgnoreWarnings = forceWarnings, AbortOnFirstIssue = false, BookEntriesIndividually = false },
            allowDuplicate: false);
        await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftBookAllEnqueued", ToolName,
            new List<McpAuditFieldChange>
            {
                new("BackgroundTask", info.Id, "BookAllEnqueued", null, info.Id.ToString())
            },
            null,
            JsonSerializer.Serialize(new { operation = "bookAll", forceWarnings }), ct);
        return new
        {
            operation = "bookAll",
            applied = true,
            alreadyRunning = false,
            backgroundTaskId = info.Id
        };
    }
}
