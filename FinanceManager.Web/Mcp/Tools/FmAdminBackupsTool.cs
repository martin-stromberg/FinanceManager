using FinanceManager.Application;
using FinanceManager.Application.Backups;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Users;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Backups;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the administrative backup tool <c>fm_admin_backups</c> (T36): lists the
/// owner's stored backups, triggers a new backup snapshot, issues a short-lived download
/// URL, deletes one backup, registers a server-side zip file (upload) or applies a
/// backup — create/delete/upload/apply are final/effective (one-shot effects on the
/// installation storage or, for apply, a destructive restore of the live data) and run
/// through the approval queue (<c>Err_Mcp_ApprovalRequired</c> →
/// <c>AiActionRequest</c>); download needs no approval (read-only, it only returns a
/// ticketed URL — the file stream itself is served by <c>GET /mcp/download/{token}</c>).
/// The approved apply enqueues a <c>BackupRestore</c> background task (track it via
/// <c>fm_task_status</c>) instead of restoring synchronously.
/// Admin-gated (the owner must hold the admin role, <c>Err_Mcp_AdminRequired</c> otherwise).
/// Scope: (Administration, Lesen); create/upload additionally (Administration,
/// Erstellung), delete/apply additionally (Administration, Änderung) — evaluated inside
/// the handler.
/// </summary>
public sealed class FmAdminBackupsDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_admin_backups";

    /// <summary>Creates the descriptor.</summary>
    public FmAdminBackupsDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"list\",\"create\",\"download\",\"delete\",\"upload\",\"apply\"],\"description\":\"Backup operation (default 'list'); create/delete/upload/apply require approval; download returns a short-lived download URL (no approval).\"},\"id\":{\"type\":\"string\",\"description\":\"Backup id (UUID) — required for 'download', 'delete' and 'apply'.\"},\"path\":{\"type\":\"string\",\"description\":\"Server-side filesystem path of the backup zip — required for 'upload'.\"},\"confirmationText\":{\"type\":\"string\",\"description\":\"Restore confirmation — required for 'apply', must equal the backup file name exactly (untrusted).\"},\"skip\":{\"type\":\"integer\",\"description\":\"Paging offset (default 0).\"},\"take\":{\"type\":\"integer\",\"description\":\"Page size 1-200 (default 50).\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Lists the owner's stored backups (default operation 'list', paging via skip/take), " +
           "triggers a new backup snapshot ('create' — final/effective, requires approval " +
           "and runs through the AiActionRequest queue) or issues a short-lived, ticket-bound " +
           "download URL for one backup ('download' — (Administration, Lesen), no approval " +
           "needed; the zip is served by GET /mcp/download/{token} within ~10 minutes), " +
           "or deletes one backup ('delete' — final/destructive, requires approval), " +
           "or registers a server-side zip file as backup ('upload' — requires approval; " +
           "'path' is read on the server filesystem), " +
           "or applies a backup ('apply' — final/destructive restore of the live data; " +
           "requires approval plus 'confirmationText' matching the backup file name and " +
           "enqueues a background task — track it via fm_task_status). " +
           "Restricted to owners with " +
           "administrative privileges. Scope: (Administration, Lesen); create/upload " +
           "additionally (Administration, Erstellung), delete/apply additionally " +
           "(Administration, Änderung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Administration, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_admin_backups</c>: adapts <see cref="IBackupService"/> — the same
/// contract the backup REST endpoint uses. The destructive restore is never run inside
/// the tool call: like <c>apply/start</c> it is enqueued as a
/// <see cref="BackgroundTaskType.BackupRestore"/> task via
/// <see cref="IBackgroundTaskManager"/> (a synchronous restore of a large backup would
/// risk the approved-execution timeout). Backup file names/sources are untrusted
/// content (<c>untrusted_data</c>).
/// </summary>
public sealed class FmAdminBackupsHandler : IMcpToolHandler
{
    private const string EntityType = "Backup";

    private readonly UserManager<User> _users;
    private readonly IBackupService _backups;
    private readonly IMcpDownloadTicketService _tickets;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly IBackgroundTaskManager _taskManager;

    /// <summary>Creates the handler.</summary>
    public FmAdminBackupsHandler(
        UserManager<User> users,
        IBackupService backups,
        IMcpDownloadTicketService tickets,
        IHttpContextAccessor httpContextAccessor,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        IBackgroundTaskManager taskManager)
    {
        _users = users;
        _backups = backups;
        _tickets = tickets;
        _httpContextAccessor = httpContextAccessor;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _taskManager = taskManager;
    }

    /// <inheritdoc />
    public string ToolName => FmAdminBackupsDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        await McpAdminGuards.RequireAdminAsync(_users, owner, ct);

        var operation = McpToolArguments.OptionalString(arguments, "operation", 30) ?? "list";
        return operation switch
        {
            "list" => await ListAsync(arguments, owner, ct),
            "create" => await CreateAsync(owner, context, ct),
            "download" => await DownloadAsync(arguments, owner, context, ct),
            "delete" => await DeleteAsync(arguments, owner, context, ct),
            "upload" => await UploadAsync(arguments, owner, context, ct),
            "apply" => await ApplyAsync(arguments, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of list|create|download|delete|upload|apply.")
        };
    }

    private async Task<object> ListAsync(JsonElement arguments, Guid owner, CancellationToken ct)
    {
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);
        var all = await _backups.ListAsync(owner, ct);
        var page = all.OrderByDescending(b => b.CreatedUtc).Skip(skip).Take(take).ToList();
        return new
        {
            operation = "list",
            totalCount = all.Count,
            skip,
            take,
            backups = page.Select(Map).ToList()
        };
    }

    private async Task<object> CreateAsync(Guid owner, McpToolCallContext context, CancellationToken ct)
        => await McpAdminGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Create,
            "create",
            "Creates a new backup snapshot of the owner's data (zip container in the backup " +
            "storage); the snapshot is a one-shot effect that consumes storage.",
            new { operation = "create" }, ct,
            executeApproved: async ct2 =>
            {
                // Create first, then stage the DataChange with the real record id —
                // the audit writer rejects placeholder entity ids (Guid.Empty).
                BackupDto created;
                try
                {
                    created = await _backups.CreateAsync(owner, "KI", ct2);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
                {
                    throw new McpToolException("Err_Mcp_InvalidState", ex.Message);
                }
                await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BackupCreated", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new(EntityType, created.Id, "Created", null, created.Id.ToString())
                    },
                    1,
                    JsonSerializer.Serialize(new { operation = "create" }), ct2);
                return new { operation = "create", applied = true, backup = Map(created) };
            });

    /// <summary>
    /// Issues a short-lived download ticket for one of the owner's backups and answers the
    /// absolute (when an HTTP request context exists) or relative <c>/mcp/download/{token}</c>
    /// URL. Read-only — no approval; the file stream itself is only ever served through the
    /// ticketed endpoint, never inside the JSON-RPC result.
    /// </summary>
    private async Task<object> DownloadAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var id = McpToolArguments.RequiredGuid(arguments, "id");
        var backup = await _backups.GetAsync(owner, id, ct);
        if (backup is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "The backup was not found.");
        }

        var ticket = _tickets.Issue(owner, id, context.Access.Access?.Id);
        return new
        {
            operation = "download",
            downloadUrl = BuildDownloadUrl(ticket.Token),
            sizeBytes = backup.SizeBytes,
            expiresUtc = ticket.ExpiresUtc,
            untrusted_data = new { fileName = backup.FileName }
        };
    }

    /// <summary>
    /// Deletes one of the owner's backups — final/destructive, (Administration, Änderung)
    /// (the Administration installation scope knows Read/Modify/Create — Löschung is
    /// reserved for the fachliche pflege data types),
    /// approval-required. The DataChange anchors on the real record id.
    /// </summary>
    private async Task<object> DeleteAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var id = McpToolArguments.RequiredGuid(arguments, "id");
        var backup = await _backups.GetAsync(owner, id, ct);
        if (backup is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "The backup was not found.");
        }
        return await McpAdminGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Modify,
            "delete",
            "Deletes the owner's backup record and its zip file — final/destructive, " +
            "the snapshot can no longer be restored afterwards.",
            new { operation = "delete", id }, ct,
            executeApproved: async ct2 =>
            {
                var deleted = await _backups.DeleteAsync(owner, id, ct2);
                if (!deleted)
                {
                    throw new McpToolException("Err_Mcp_InvalidState", "The backup could not be deleted.");
                }
                await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BackupDeleted", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new(EntityType, id, "Deleted", backup.FileName, null)
                    },
                    1,
                    JsonSerializer.Serialize(new { operation = "delete", id }), ct2);
                return new { operation = "delete", deleted = true, id };
            });
    }

    /// <summary>
    /// Registers a server-side zip file as one of the owner's backups — (Administration,
    /// Erstellung), approval-required. 'path' is read on the server filesystem (the admin
    /// scope owner controls the installation); the service itself validates the zip
    /// container and payload. The uploaded file name is untrusted content.
    /// </summary>
    private async Task<object> UploadAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var path = McpToolArguments.OptionalString(arguments, "path", 1024);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'path' is required for 'upload'.");
        }
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(path))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "The path does not point to an existing file on the server.");
        }
        return await McpAdminGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Create,
            "upload",
            "Registers a server-side zip file as backup of the owner — the file is copied " +
            "into the backup storage; source is recorded as Upload.",
            new { operation = "upload", fileName }, ct,
            executeApproved: async ct2 =>
            {
                BackupDto uploaded;
                try
                {
                    await using var stream = File.OpenRead(path);
                    uploaded = await _backups.UploadAsync(owner, stream, fileName, ct2);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                    or IOException or FileLoadException)
                {
                    throw new McpToolException("Err_Mcp_InvalidState", ex.Message);
                }
                await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BackupUploaded", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new(EntityType, uploaded.Id, "Created", null, uploaded.FileName)
                    },
                    1,
                    JsonSerializer.Serialize(new { operation = "upload", fileName }), ct2);
                return new { operation = "upload", applied = true, backup = Map(uploaded) };
            });
    }

    /// <summary>
    /// Enqueues the restore of one of the owner's backups — the same path
    /// <c>POST /api/setup/backups/{id}/apply/start</c> uses: a
    /// <see cref="BackgroundTaskType.BackupRestore"/> task whose executor calls
    /// <see cref="IBackupService.ApplyAsync"/> (a synchronous restore would risk the
    /// approved-execution timeout for large backups). Final/destructive — the live data
    /// is replaced; (Administration, Änderung), approval-required. Guard validations
    /// run before queueing: the backup must exist and 'confirmationText' must equal its
    /// file name — the service re-checks the match inside the task (the
    /// confirmationAlreadyValidated flag only steers the audit reason). The staged
    /// DataChange anchors on the enqueued task record (bookAll precedent). The backup
    /// file name is untrusted content.
    /// </summary>
    private async Task<object> ApplyAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var id = McpToolArguments.RequiredGuid(arguments, "id");
        var confirmationText = McpToolArguments.OptionalString(arguments, "confirmationText", 500);
        if (string.IsNullOrWhiteSpace(confirmationText))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'confirmationText' is required for 'apply' and must equal the backup file name.");
        }
        var backup = await _backups.GetAsync(owner, id, ct);
        if (backup is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "The backup was not found.");
        }
        if (!string.Equals(confirmationText, backup.FileName, StringComparison.Ordinal))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'confirmationText' must equal the backup file name.");
        }
        return await McpAdminGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Modify,
            "apply",
            "Restores the backup into the owner's live database — final/destructive, " +
            "all current data is replaced by the snapshot; the restore runs as a " +
            "background task (track it via fm_task_status).",
            new { operation = "apply", id, confirmationText }, ct,
            executeApproved: async ct2 =>
            {
                var existing = _taskManager.GetAll()
                    .Where(t => t.UserId == owner && t.Type == BackgroundTaskType.BackupRestore
                        && (t.Status == BackgroundTaskStatus.Running || t.Status == BackgroundTaskStatus.Queued))
                    .OrderByDescending(t => t.EnqueuedUtc)
                    .FirstOrDefault();
                if (existing is not null)
                {
                    return new
                    {
                        operation = "apply",
                        enqueued = false,
                        alreadyRunning = true,
                        taskId = existing.Id,
                        untrusted_data = new { fileName = backup.FileName }
                    };
                }

                // Enqueue first, then stage the DataChange with the real id of the
                // created background task — the task record is the fachlich nächste
                // anchor (bookAll precedent); the audit writer rejects placeholder
                // entity ids (Guid.Empty). The payload mirrors apply/start: the
                // executor requires Confirmed + ExpectedFileName and re-validates the
                // file-name match against the stored record when it runs.
                var info = _taskManager.Enqueue(BackgroundTaskType.BackupRestore, owner,
                    new { BackupId = id, Confirmed = true, ExpectedFileName = backup.FileName },
                    allowDuplicate: false);
                await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BackupRestoreEnqueued", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new("BackgroundTask", info.Id, "RestoreEnqueued", null, info.Id.ToString())
                    },
                    null,
                    JsonSerializer.Serialize(new { operation = "apply", id }), ct2);
                return new
                {
                    operation = "apply",
                    enqueued = true,
                    taskId = info.Id,
                    untrusted_data = new { fileName = backup.FileName }
                };
            });
    }

    private string BuildDownloadUrl(string token)
    {
        var path = $"/mcp/download/{token}";
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            return path;
        }
        return $"{request.Scheme}://{request.Host.ToUriComponent()}{request.PathBase.ToUriComponent()}{path}";
    }

    private static object Map(BackupDto b)
        => new
        {
            id = b.Id,
            createdUtc = b.CreatedUtc,
            sizeBytes = b.SizeBytes,
            untrusted_data = new { fileName = b.FileName, source = b.Source }
        };
}
