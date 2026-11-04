using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Notifications;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Persistent approval-request queue service (F-0902; approval-konzept.md §3–§6,
/// datenmodell-erweiterungen.md §5). Request creation deduplicates via the
/// owner-unique <see cref="AiActionRequest.IdempotencyKey"/> (a retry returns the existing
/// request), enforces the per-access pending limit and couples the
/// <see cref="McpAuditCategory.ApprovalEvent"/> record transactionally to each state
/// transition. Owner isolation is strict — a foreign request id behaves like a missing one.
/// </summary>
public sealed class McpApprovalService : IMcpApprovalService
{
    /// <summary>Decision note recorded on pre-approved requests (approval-konzept.md §3.4).</summary>
    public const string PreApprovedDecisionNote = "Vorab-Genehmigung Zugangseinstellung";

    /// <summary>Maximum accepted client idempotency-key length (the composed key is capped at 200).</summary>
    private const int MaxClientKeyLength = 140;

    /// <summary>Maximum accepted reason-text length (audit-konzept.md §4.2 style cap).</summary>
    private const int MaxReasonTextLength = 1000;

    private readonly AppDbContext _db;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly INotificationWriter _notifications;
    private readonly IMcpToolRegistry _registry;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly ILogger<McpApprovalService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="db">Shared scoped database context.</param>
    /// <param name="auditWriter">Hash-chained audit writer (shared unit of work).</param>
    /// <param name="notifications">Owner notification writer.</param>
    /// <param name="registry">Compiled tool registry (descriptor lookup for the granular
    /// scope-coverage withdrawal).</param>
    /// <param name="scopeEvaluator">Scope evaluator resolving the effective profile for the
    /// coverage check.</param>
    /// <param name="options">MCP options (expiry default, pending limit).</param>
    /// <param name="logger">Logger.</param>
    public McpApprovalService(
        AppDbContext db,
        IMcpAuditWriter auditWriter,
        INotificationWriter notifications,
        IMcpToolRegistry registry,
        IMcpScopeEvaluator scopeEvaluator,
        IOptionsMonitor<McpOptions> options,
        ILogger<McpApprovalService> logger)
    {
        _db = db;
        _auditWriter = auditWriter;
        _notifications = notifications;
        _registry = registry;
        _scopeEvaluator = scopeEvaluator;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiActionRequest> EnqueueAsync(
        McpAccess access,
        string toolName,
        string actionKey,
        string catalogVersion,
        string parameterSnapshotJson,
        string? reasonText,
        Guid correlationId,
        string? clientIdempotencyKey,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (string.IsNullOrWhiteSpace(toolName)) throw new ArgumentException("Tool name is required.", nameof(toolName));
        if (string.IsNullOrWhiteSpace(parameterSnapshotJson)) throw new ArgumentException("Parameter snapshot is required.", nameof(parameterSnapshotJson));

        var key = BuildIdempotencyKey(access.Id, clientIdempotencyKey, toolName, actionKey, parameterSnapshotJson);

        // Idempotent enqueue: a repeated call with the same key returns the existing request.
        var existing = await _db.AiActionRequests
            .FirstOrDefaultAsync(r => r.OwnerUserId == access.OwnerUserId && r.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            return existing;
        }

        var approval = _options.CurrentValue.Approval;
        var pending = await _db.AiActionRequests
            .CountAsync(r => r.McpAccessId == access.Id && r.Status == AiActionRequestStatus.Pending, ct);
        if (pending >= approval.MaxPendingPerAccess)
        {
            throw new DomainValidationException("Err_Mcp_PendingLimit",
                "The limit of open approval requests for this access is reached.");
        }

        if (reasonText is not null && reasonText.Length > MaxReasonTextLength)
        {
            reasonText = reasonText[..MaxReasonTextLength];
        }

        var expiresUtc = DateTime.UtcNow.AddHours(approval.DefaultExpiryHours);
        var key2 = actionKey;
        AiActionRequest request = access.ApprovalMode == McpApprovalMode.PreApproved
            ? AiActionRequest.CreatePreApproved(
                access.OwnerUserId, access.Id, access.Name, toolName, key2, catalogVersion,
                parameterSnapshotJson, reasonText, expiresUtc, correlationId, key,
                PreApprovedDecisionNote)
            : new AiActionRequest(
                access.OwnerUserId, access.Id, access.Name, toolName, key2, catalogVersion,
                parameterSnapshotJson, reasonText, expiresUtc, correlationId, key);

        // Request row + approval event commit atomically (transactional coupling).
        _db.AiActionRequests.Add(request);
        await _auditWriter.StageAsync(new McpAuditWriteRequest(
            McpAuditCategory.ApprovalEvent, "ApprovalRequested", "RedirectedToApproval",
            OwnerUserId: access.OwnerUserId, McpAccessId: access.Id, McpAccessName: access.Name,
            ToolName: toolName, CatalogVersion: catalogVersion, Context: "McpEndpoint",
            CorrelationId: correlationId, RelatedRequestId: request.Id,
            PayloadJson: JsonSerializer.Serialize(new
            {
                requestId = request.Id,
                actionKey = request.ActionKey,
                initialStatus = request.Status.ToString(),
                expiresUtc = request.ExpiresUtc
            })), ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent enqueue with the same key: the unique index decided — return the winner.
            _db.Entry(request).State = EntityState.Detached;
            var winner = await _db.AiActionRequests
                .FirstOrDefaultAsync(r => r.OwnerUserId == access.OwnerUserId && r.IdempotencyKey == key, ct);
            if (winner is not null)
            {
                return winner;
            }
            throw;
        }

        if (request.Status == AiActionRequestStatus.Pending)
        {
            await _notifications.CreateForUserAsync(
                access.OwnerUserId,
                "Neue MCP-Genehmigungsanfrage",
                $"Der Zugang '{access.Name}' beantragt die Ausführung von '{toolName}'.",
                NotificationType.EventDriven, NotificationTarget.Dashboard, DateTime.UtcNow,
                $"mcp-approval-request:{request.Id}", ct);
        }
        return request;
    }

    /// <inheritdoc />
    public async Task<bool> ApproveAsync(Guid ownerUserId, Guid requestId, string? note, CancellationToken ct)
    {
        var request = await _db.AiActionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.OwnerUserId == ownerUserId, ct);
        if (request is null)
        {
            return false;
        }
        try
        {
            request.Approve(ownerUserId, note);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainValidationException("Err_Mcp_InvalidState", ex.Message);
        }
        await StageDecisionAsync(request, "Approved", note, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RejectAsync(Guid ownerUserId, Guid requestId, string? note, CancellationToken ct)
    {
        var request = await _db.AiActionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.OwnerUserId == ownerUserId, ct);
        if (request is null)
        {
            return false;
        }
        try
        {
            request.Reject(ownerUserId, note);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainValidationException("Err_Mcp_InvalidState", ex.Message);
        }
        await StageDecisionAsync(request, "Rejected", note, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(Guid ownerUserId, Guid requestId, CancellationToken ct)
    {
        var request = await _db.AiActionRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.OwnerUserId == ownerUserId, ct);
        if (request is null)
        {
            return false;
        }
        try
        {
            request.Reject(ownerUserId, "Withdrawn by owner");
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainValidationException("Err_Mcp_InvalidState", ex.Message);
        }
        await StageDecisionAsync(request, "Withdrawn", null, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<AiActionRequestDetailDto?> GetAsync(Guid ownerUserId, Guid requestId, CancellationToken ct)
    {
        var request = await _db.AiActionRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.OwnerUserId == ownerUserId, ct);
        return request is null ? null : ToDetail(request);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiActionRequestListItemDto>> ListAsync(
        Guid ownerUserId, AiActionRequestStatus? status, Guid? accessId, int skip, int take, CancellationToken ct)
    {
        var query = _db.AiActionRequests.AsNoTracking().Where(r => r.OwnerUserId == ownerUserId);
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }
        if (accessId.HasValue)
        {
            query = query.Where(r => r.McpAccessId == accessId.Value);
        }
        var list = await query
            .OrderByDescending(r => r.RequestedUtc)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
        return list.Select(ToListItem).ToList();
    }

    /// <inheritdoc />
    public Task<int> CountPendingAsync(Guid mcpAccessId, CancellationToken ct)
        => _db.AiActionRequests.AsNoTracking()
            .CountAsync(r => r.McpAccessId == mcpAccessId && r.Status == AiActionRequestStatus.Pending, ct);

    /// <inheritdoc />
    public async Task<int> WithdrawOpenForAccessAsync(Guid mcpAccessId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A documented reason is required.", nameof(reason));

        var open = await _db.AiActionRequests
            .Where(r => r.McpAccessId == mcpAccessId
                && (r.Status == AiActionRequestStatus.Pending
                    || (r.Status == AiActionRequestStatus.Approved && r.ClaimToken == null)))
            .ToListAsync(ct);
        foreach (var request in open)
        {
            request.WithdrawBySystem(reason);
            await StageWithdrawalAsync(request, reason, ct);
        }
        if (open.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Withdrew {Count} open approval requests of access {AccessId}: {Reason}",
                open.Count, mcpAccessId, reason);
        }
        return open.Count;
    }

    /// <inheritdoc />
    public async Task<int> WithdrawScopeUncoveredForAccessAsync(Guid mcpAccessId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A documented reason is required.", nameof(reason));

        var access = await _db.McpAccesses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == mcpAccessId, ct);
        if (access is null)
        {
            return 0; // a gone access withdraws nothing here — the executor re-check covers it
        }
        // The effective profile *after* the profile-changing write: level default profile or
        // the persisted individual scope rows.
        var granted = (await _scopeEvaluator.GetEffectiveScopesAsync(access, ct))
            .Select(s => new McpScopeTuple(s.DataType, s.Right))
            .ToHashSet();

        var open = await _db.AiActionRequests
            .Where(r => r.McpAccessId == mcpAccessId
                && (r.Status == AiActionRequestStatus.Pending
                    || (r.Status == AiActionRequestStatus.Approved && r.ClaimToken == null)))
            .ToListAsync(ct);

        var withdrawn = 0;
        foreach (var request in open)
        {
            var descriptor = _registry.Find(request.ToolName);
            if (descriptor is null || descriptor.MetaScope is not null)
            {
                // The meta scope is inherent to every access and never lost; an unknown tool
                // keeps its request — it fails classified (Err_Mcp_UnknownTool) at execution.
                continue;
            }
            // Granular „Recht entfallen" (approval-konzept.md §3.3): only a request whose
            // declared scope tuples the new profile no longer fully covers is withdrawn —
            // every still-covered request stays open for a normal decision.
            if (descriptor.RequiredScopes.All(s => granted.Contains(new McpScopeTuple(s.DataType, s.Right))))
            {
                continue;
            }
            request.WithdrawBySystem(reason);
            await StageWithdrawalAsync(request, reason, ct);
            withdrawn++;
        }
        if (withdrawn > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Withdrew {Count} open approval requests of access {AccessId} — required scope no longer covered: {Reason}",
                withdrawn, mcpAccessId, reason);
        }
        return withdrawn;
    }

    /// <summary>Stages the <c>ApprovalWithdrawn</c> audit record of a system withdrawal.</summary>
    private Task<McpAuditEntry?> StageWithdrawalAsync(AiActionRequest request, string reason, CancellationToken ct)
        => _auditWriter.StageAsync(new McpAuditWriteRequest(
            McpAuditCategory.ApprovalEvent, "ApprovalWithdrawn", "Success",
            OwnerUserId: request.OwnerUserId, McpAccessId: request.McpAccessId,
            McpAccessName: request.McpAccessName, ToolName: request.ToolName,
            CatalogVersion: request.CatalogVersion, Context: "System/Job",
            CorrelationId: request.CorrelationId, RelatedRequestId: request.Id,
            PayloadJson: JsonSerializer.Serialize(new { requestId = request.Id, reason })), ct);

    /// <summary>
    /// Stages the <c>ApprovalDecided</c> audit record of a user decision.
    /// </summary>
    private async Task StageDecisionAsync(AiActionRequest request, string decision, string? note, CancellationToken ct)
    {
        await _auditWriter.StageAsync(new McpAuditWriteRequest(
            McpAuditCategory.ApprovalEvent, "ApprovalDecided", "Success",
            OwnerUserId: request.OwnerUserId, McpAccessId: request.McpAccessId,
            McpAccessName: request.McpAccessName, ToolName: request.ToolName,
            CatalogVersion: request.CatalogVersion, Context: "Ui",
            CorrelationId: request.CorrelationId, RelatedRequestId: request.Id,
            PayloadJson: JsonSerializer.Serialize(new { requestId = request.Id, decision, note })), ct);
    }

    /// <summary>
    /// Builds the deduplication key: a client key is namespaced by the access id; without a
    /// client key a content hash of tool + action + parameters deduplicates identical retries.
    /// </summary>
    private static string BuildIdempotencyKey(Guid accessId, string? clientKey, string toolName, string actionKey, string parameterSnapshotJson)
    {
        if (!string.IsNullOrWhiteSpace(clientKey))
        {
            var trimmed = clientKey.Trim();
            if (trimmed.Length > MaxClientKeyLength)
            {
                trimmed = trimmed[..MaxClientKeyLength];
            }
            return $"{accessId:N}:{trimmed}";
        }
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{toolName}|{actionKey}|{parameterSnapshotJson}")))
            .ToLowerInvariant()[..32];
        return $"{accessId:N}:h:{hash}";
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
            || ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);

    private static AiActionRequestListItemDto ToListItem(AiActionRequest r)
        => new(r.Id, r.McpAccessId, r.McpAccessName, r.ToolName, r.ActionKey, (short)r.Status,
            r.RequestedUtc, r.ExpiresUtc, r.DecidedUtc, r.ExecutedUtc, r.ExecutionResult,
            r.FailureReason, r.CorrelationId, r.AttemptCount);

    private static AiActionRequestDetailDto ToDetail(AiActionRequest r)
        => new(r.Id, r.McpAccessId, r.McpAccessName, r.ToolName, r.ActionKey, r.CatalogVersion,
            r.ParameterSnapshot, r.ReasonText, (short)r.Status, r.RequestedUtc, r.ExpiresUtc,
            r.DecidedUtc, r.DecidedByUserId, r.DecisionNote, r.ExecutedUtc, r.ExecutionResult,
            r.FailureReason, r.CorrelationId, r.IdempotencyKey, r.AttemptCount);
}
