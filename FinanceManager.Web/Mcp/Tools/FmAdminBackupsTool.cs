using FinanceManager.Application.Backups;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Users;
using FinanceManager.Shared.Dtos.Backups;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the administrative backup tool <c>fm_admin_backups</c> (T36): lists the
/// owner's stored backups, triggers a new backup snapshot and issues a short-lived download
/// URL — the create sub-operation is final/effective (one-shot effect on the installation
/// storage) and runs through the approval queue (<c>Err_Mcp_ApprovalRequired</c> →
/// <c>AiActionRequest</c>); download needs no approval (read-only, it only returns a
/// ticketed URL — the file stream itself is served by <c>GET /mcp/download/{token}</c>).
/// Restore, upload and delete are deliberately not exposed.
/// Admin-gated (the owner must hold the admin role, <c>Err_Mcp_AdminRequired</c> otherwise).
/// Scope: (Administration, Lesen); create additionally (Administration, Erstellung) —
/// evaluated inside the handler.
/// </summary>
public sealed class FmAdminBackupsDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_admin_backups";

    /// <summary>Creates the descriptor.</summary>
    public FmAdminBackupsDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"list\",\"create\",\"download\",\"delete\"],\"description\":\"Backup operation (default 'list'); create/delete require approval; download returns a short-lived download URL (no approval).\"},\"id\":{\"type\":\"string\",\"description\":\"Backup id (UUID) — required for 'download' and 'delete'.\"},\"skip\":{\"type\":\"integer\",\"description\":\"Paging offset (default 0).\"},\"take\":{\"type\":\"integer\",\"description\":\"Page size 1-200 (default 50).\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted).\"}},\"additionalProperties\":false}")
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
           "or deletes one backup ('delete' — final/destructive, requires approval). " +
           "Restore and upload are not exposed. Restricted to owners with " +
           "administrative privileges. Scope: (Administration, Lesen); create additionally " +
           "(Administration, Erstellung), delete additionally (Administration, Änderung).";

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
/// contract the backup REST endpoint uses. Backup file names/sources are untrusted
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

    /// <summary>Creates the handler.</summary>
    public FmAdminBackupsHandler(
        UserManager<User> users,
        IBackupService backups,
        IMcpDownloadTicketService tickets,
        IHttpContextAccessor httpContextAccessor,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter)
    {
        _users = users;
        _backups = backups;
        _tickets = tickets;
        _httpContextAccessor = httpContextAccessor;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
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
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of list|create|download|delete.")
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
