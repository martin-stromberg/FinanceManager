using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// <see cref="IMcpAuditWriter"/> fake that enforces the real writer's domain guard: every
/// staged or written field delta must carry a real, non-empty record id — the persistent
/// <c>McpAuditWriter</c> runs each change through the <c>McpAuditEntryChange</c> constructor
/// which throws <see cref="ArgumentException"/> for <see cref="Guid.Empty"/> entity ids.
/// The mocked writers used elsewhere never exercise that path, so this fake replays the
/// guard and additionally captures the last request for assertions.
/// </summary>
internal sealed class GuardingAuditWriter : IMcpAuditWriter
{
    /// <summary>Last request passed to <see cref="StageAsync"/> (or <see cref="WriteAsync"/>).</summary>
    public McpAuditWriteRequest? LastRequest { get; private set; }

    /// <inheritdoc />
    public Task WriteAsync(McpAuditWriteRequest request, CancellationToken ct)
    {
        Guard(request);
        LastRequest = request;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<McpAuditEntry?> StageAsync(McpAuditWriteRequest request, CancellationToken ct)
    {
        Guard(request);
        LastRequest = request;
        return Task.FromResult<McpAuditEntry?>(null);
    }

    /// <inheritdoc />
    public Task<McpAuditChainVerificationResult> VerifyAsync(Guid? ownerUserId, CancellationToken ct)
        => Task.FromResult(new McpAuditChainVerificationResult(
            ownerUserId, 0, true, null, null, Array.Empty<McpAuditChainGap>()));

    private static void Guard(McpAuditWriteRequest request)
    {
        foreach (var change in request.Changes ?? Array.Empty<McpAuditFieldChange>())
        {
            if (change.EntityId == Guid.Empty)
            {
                throw new ArgumentException("Audit field deltas require a real record id.", nameof(request));
            }
        }
    }
}
