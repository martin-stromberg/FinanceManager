using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Application.Users;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Admin;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the administrative notification tool <c>fm_admin_notifications</c> (T37):
/// lists the access owner's active notifications and dismisses (confirms) a single one.
/// Dismissal only marks <c>IsDismissed</c> on an own/global notification — an uncritical,
/// directly executed write effect (no approval); it requires the
/// (Administration, Änderung) scope, evaluated inside the handler.
/// Admin-gated (the owner must hold the admin role, <c>Err_Mcp_AdminRequired</c> otherwise).
/// Scope: (Administration, Lesen).
/// </summary>
public sealed class FmAdminNotificationsDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_admin_notifications";

    /// <summary>Creates the descriptor.</summary>
    public FmAdminNotificationsDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"list\",\"dismiss\"],\"description\":\"Notification operation (default 'list').\"},\"notificationId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Notification to dismiss (required for dismiss).\"},\"skip\":{\"type\":\"integer\",\"description\":\"Paging offset (default 0).\"},\"take\":{\"type\":\"integer\",\"description\":\"Page size 1-200 (default 50).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Lists the access owner's active notifications (default operation 'list', paging " +
           "via skip/take) or dismisses/confirms a single notification ('dismiss' with " +
           "notificationId) — an uncritical direct write (IsDismissed flag on the own/global " +
           "record, no approval). Restricted to owners with administrative privileges. " +
           "Scope: (Administration, Lesen); dismiss additionally (Administration, Änderung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Administration, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_admin_notifications</c>: adapts <see cref="INotificationService"/> —
/// owner-scoped by contract (a foreign notification behaves like a missing one). Titles,
/// messages and trigger keys are untrusted content (<c>untrusted_data</c>). The dismiss
/// effect commits atomically with its staged <c>DataChange</c> record (F-0301).
/// </summary>
public sealed class FmAdminNotificationsHandler : IMcpToolHandler
{
    private const string EntityType = "Notification";

    private readonly IUserAdminService _users;
    private readonly INotificationService _notifications;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public FmAdminNotificationsHandler(
        IUserAdminService users,
        INotificationService notifications,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter)
    {
        _users = users;
        _notifications = notifications;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public string ToolName => FmAdminNotificationsDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        await McpAdminGuards.RequireAdminAsync(_users, owner, ct);

        var operation = McpToolArguments.OptionalString(arguments, "operation", 30) ?? "list";
        return operation switch
        {
            "list" => await ListAsync(arguments, owner, ct),
            "dismiss" => await DismissAsync(arguments, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of list|dismiss.")
        };
    }

    private async Task<object> ListAsync(JsonElement arguments, Guid owner, CancellationToken ct)
    {
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);
        var all = await _notifications.ListActiveAsync(owner, DateTime.UtcNow, ct);
        var page = all.Skip(skip).Take(take).ToList();
        return new
        {
            operation = "list",
            totalCount = all.Count,
            skip,
            take,
            notifications = page.Select(Map).ToList()
        };
    }

    private async Task<object> DismissAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var notificationId = McpToolArguments.RequiredGuid(arguments, "notificationId");
        await McpAdminGuards.RequireRightAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Modify, "dismiss", ct);

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "NotificationDismissed", ToolName,
            new List<McpAuditFieldChange>
            {
                new(EntityType, notificationId, "IsDismissed", "false", "true")
            },
            1,
            JsonSerializer.Serialize(new { operation = "dismiss", notificationId }), ct);
        var dismissed = await _notifications.DismissAsync(notificationId, owner, ct);
        if (!dismissed)
        {
            // The notification does not exist, is already dismissed or is not visible to
            // the owner — 404 semantics, no half state (the staged record is discarded).
            McpWriteAudit.DiscardStaged(context, staged);
            return new { operation = "dismiss", applied = false, notificationId };
        }
        return new { operation = "dismiss", applied = true, notificationId };
    }

    private static object Map(NotificationDto n)
        => new
        {
            id = n.Id,
            type = n.Type,
            target = n.Target,
            scheduledDateUtc = n.ScheduledDateUtc,
            createdUtc = n.CreatedUtc,
            isDismissed = n.IsDismissed,
            untrusted_data = new
            {
                title = n.Title,
                message = n.Message,
                triggerEventKey = n.TriggerEventKey
            }
        };
}
