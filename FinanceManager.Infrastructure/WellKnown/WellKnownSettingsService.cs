using FinanceManager.Application.WellKnown;
using FinanceManager.Domain.WellKnown;
using FinanceManager.Shared.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.WellKnown;

/// <summary>
/// Loads and stores well-known endpoint settings.
/// </summary>
public sealed class WellKnownSettingsService : IWellKnownSettingsService
{
    private readonly AppDbContext _db;

    /// <summary>Creates a new instance.</summary>
    /// <param name="db">The db.</param>
    public WellKnownSettingsService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<WellKnownSettingsDto> GetAsync(CancellationToken ct)
    {
        // Read path must not write: fall back to an unpersisted default when the row is missing.
        var entity = await GetEntityAsync(ct) ?? WellKnownSettings.CreateDefault();
        return new WellKnownSettingsDto
        {
            ChangePasswordUrl = entity.ChangePasswordUrl
        };
    }

    /// <inheritdoc />
    public async Task UpdateAsync(WellKnownSettingsUpdateRequest request, CancellationToken ct)
    {
        var entity = await GetEntityAsync(ct);
        if (entity == null)
        {
            // First write materializes the singleton row. The fixed key prevents duplicates:
            // a concurrent first insert collides on the primary key and is resolved by reloading.
            entity = WellKnownSettings.CreateDefault();
            _db.WellKnownSettings.Add(entity);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _db.Entry(entity).State = EntityState.Detached;
                entity = await GetEntityAsync(ct)
                    ?? throw new InvalidOperationException("The well-known settings row could not be loaded after a concurrent insert.");
            }
        }

        entity.Update(request.ChangePasswordUrl);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<string> GetChangePasswordUrlAsync(CancellationToken ct)
    {
        // Read path must not write: fall back to the default when the row is missing or invalid.
        var entity = await GetEntityAsync(ct);
        return WellKnownSettings.IsValidUrl(entity?.ChangePasswordUrl)
            ? entity!.ChangePasswordUrl
            : WellKnownSettings.DefaultChangePasswordUrl;
    }

    private async Task<WellKnownSettings?> GetEntityAsync(CancellationToken ct)
    {
        // Prefer the fixed singleton key; fall back to any existing row so databases that already
        // contain a settings row with a different key still resolve to it instead of duplicating.
        return await _db.WellKnownSettings.FirstOrDefaultAsync(e => e.Id == WellKnownSettings.SingletonId, ct)
            ?? await _db.WellKnownSettings.FirstOrDefaultAsync(ct);
    }
}
