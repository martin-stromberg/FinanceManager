using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Loads and stores the database-backed MCP switches (kill switch and write-stop lid).
/// Follows the <c>WellKnownSettingsService</c> singleton-row pattern: the read path never
/// writes and falls back to an unpersisted default; the first update materializes the
/// single row under the fixed <see cref="McpSettings.SingletonId"/> key.
/// </summary>
public sealed class McpSettingsService : IMcpSettingsService
{
    private readonly AppDbContext _db;

    /// <summary>Creates a new instance.</summary>
    /// <param name="db">The db.</param>
    public McpSettingsService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<McpSettingsDto> GetAsync(CancellationToken ct)
    {
        // Read path must not write: fall back to an unpersisted default when the row is missing.
        var entity = await GetEntityAsync(ct) ?? McpSettings.CreateDefault();
        return new McpSettingsDto(entity.Enabled, entity.WriteAccessEnabled);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(McpSettingsUpdateRequest request, CancellationToken ct)
    {
        var entity = await GetEntityAsync(ct);
        if (entity == null)
        {
            // First write materializes the singleton row. The fixed key prevents duplicates:
            // a concurrent first insert collides on the primary key and is resolved by reloading.
            entity = McpSettings.CreateDefault();
            _db.McpSettings.Add(entity);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _db.Entry(entity).State = EntityState.Detached;
                entity = await GetEntityAsync(ct)
                    ?? throw new InvalidOperationException("The MCP settings row could not be loaded after a concurrent insert.");
            }
        }

        entity.Update(request.Enabled, request.WriteAccessEnabled);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(CancellationToken ct)
    {
        // Read path must not write: missing row means the safe off state.
        var entity = await GetEntityAsync(ct);
        return entity?.Enabled ?? false;
    }

    /// <inheritdoc />
    public async Task<bool> IsWriteAccessEnabledAsync(CancellationToken ct)
    {
        // Read path must not write: missing row means the safe off state.
        var entity = await GetEntityAsync(ct);
        return entity?.WriteAccessEnabled ?? false;
    }

    private async Task<McpSettings?> GetEntityAsync(CancellationToken ct)
    {
        // Prefer the fixed singleton key; fall back to any existing row so databases that already
        // contain a settings row with a different key still resolve to it instead of duplicating.
        return await _db.McpSettings.FirstOrDefaultAsync(e => e.Id == McpSettings.SingletonId, ct)
            ?? await _db.McpSettings.FirstOrDefaultAsync(ct);
    }
}
