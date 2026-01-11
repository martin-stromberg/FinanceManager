using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// EF Core based implementation of <see cref="IBudgetPurposeService"/>.
/// </summary>
public sealed class BudgetPurposeService : IBudgetPurposeService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">App database context.</param>
    public BudgetPurposeService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<BudgetPurposeDto> CreateAsync(Guid ownerUserId, string name, FinanceManager.Shared.Dtos.Budget.BudgetSourceType sourceType, Guid sourceId, string? description, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("OwnerUserId must not be empty", nameof(ownerUserId));
        }

        var entity = new BudgetPurpose(ownerUserId, name, (FinanceManager.Shared.Dtos.Budget.BudgetSourceType)sourceType, sourceId, description);
        _db.BudgetPurposes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<BudgetPurposeDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, FinanceManager.Shared.Dtos.Budget.BudgetSourceType sourceType, Guid sourceId, string? description, CancellationToken ct)
    {
        var entity = await _db.BudgetPurposes.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return null;
        }

        entity.Rename(name);
        entity.SetSource((FinanceManager.Shared.Dtos.Budget.BudgetSourceType)sourceType, sourceId);
        entity.SetDescription(description);

        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.BudgetPurposes.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return false;
        }

        // Delete dependent rules and overrides (no FK defined by design; keep deterministic behavior).
        var rules = _db.BudgetRules.Where(r => r.OwnerUserId == ownerUserId && r.BudgetPurposeId == id);
        var overrides = _db.BudgetOverrides.Where(o => o.OwnerUserId == ownerUserId && o.BudgetPurposeId == id);

        if (_db.Database.IsRelational())
        {
            await rules.ExecuteDeleteAsync(ct);
            await overrides.ExecuteDeleteAsync(ct);
        }
        else
        {
            _db.BudgetRules.RemoveRange(await rules.ToListAsync(ct));
            _db.BudgetOverrides.RemoveRange(await overrides.ToListAsync(ct));
        }

        _db.BudgetPurposes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<BudgetPurposeDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        return await _db.BudgetPurposes.AsNoTracking()
            .Where(p => p.Id == id && p.OwnerUserId == ownerUserId)
            .Select(p => new BudgetPurposeDto(p.Id, p.OwnerUserId, p.Name, p.Description, p.SourceType, p.SourceId))
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BudgetPurposeDto>> ListAsync(Guid ownerUserId, int skip, int take, FinanceManager.Shared.Dtos.Budget.BudgetSourceType? sourceType, string? nameFilter, CancellationToken ct)
    {
        var query = _db.BudgetPurposes.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId);

        if (sourceType.HasValue)
        {
            query = query.Where(p => (FinanceManager.Shared.Dtos.Budget.BudgetSourceType)p.SourceType == sourceType.Value);
        }

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            var pattern = $"%{nameFilter.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        return await query
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .Select(p => new BudgetPurposeDto(p.Id, p.OwnerUserId, p.Name, p.Description, (FinanceManager.Shared.Dtos.Budget.BudgetSourceType)p.SourceType, p.SourceId))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(Guid ownerUserId, CancellationToken ct)
    {
        return _db.BudgetPurposes.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId).CountAsync(ct);
    }

    private static BudgetPurposeDto Map(BudgetPurpose p)
        => new(p.Id, p.OwnerUserId, p.Name, p.Description, (FinanceManager.Shared.Dtos.Budget.BudgetSourceType)p.SourceType, p.SourceId);
}
