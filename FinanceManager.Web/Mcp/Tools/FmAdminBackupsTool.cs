using FinanceManager.Application.Backups;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Users;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Backups;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the administrative backup tool <c>fm_admin_backups</c> (T36): lists the
/// owner's stored backups and triggers a new backup snapshot — the create sub-operation
/// is final/effective (one-shot effect on the installation storage) and runs through the
/// approval queue (<c>Err_Mcp_ApprovalRequired</c> → <c>AiActionRequest</c>). Restore,
/// upload, download and delete are deliberately not exposed.
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
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"list\",\"create\"],\"description\":\"Backup operation (default 'list'); create requires approval.\"},\"skip\":{\"type\":\"integer\",\"description\":\"Paging offset (default 0).\"},\"take\":{\"type\":\"integer\",\"description\":\"Page size 1-200 (default 50).\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Lists the owner's stored backups (default operation 'list', paging via skip/take) " +
           "or triggers a new backup snapshot ('create' — final/effective, requires approval " +
           "and runs through the AiActionRequest queue). Restore, upload, download and delete " +
           "are not exposed. Restricted to owners with administrative privileges. " +
           "Scope: (Administration, Lesen); create additionally (Administration, Erstellung).";

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

    private readonly IUserAdminService _users;
    private readonly IBackupService _backups;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public FmAdminBackupsHandler(
        IUserAdminService users,
        IBackupService backups,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter)
    {
        _users = users;
        _backups = backups;
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
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of list|create.")
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
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BackupCreated", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new(EntityType, Guid.Empty, "Created", null, null)
                    },
                    1,
                    JsonSerializer.Serialize(new { operation = "create" }), ct2);
                BackupDto created;
                try
                {
                    created = await _backups.CreateAsync(owner, ct2);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_Mcp_InvalidState", ex.Message);
                }
                catch
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw;
                }
                return new { operation = "create", applied = true, backup = Map(created) };
            });

    private static object Map(BackupDto b)
        => new
        {
            id = b.Id,
            createdUtc = b.CreatedUtc,
            sizeBytes = b.SizeBytes,
            untrusted_data = new { fileName = b.FileName, source = b.Source }
        };
}
