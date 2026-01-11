using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// EF Core based implementation of <see cref="IBudgetOverrideService"/>.
/// </summary>
public sealed class BudgetOverrideService : IBudgetOverrideService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">App database context.</param>
    public BudgetOverrideService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<BudgetOverrideDto> CreateAsync(Guid ownerUserId, Guid budgetPurposeId, FinanceManager.Shared.Dtos.Budget.BudgetPeriodKey period, decimal amount, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("OwnerUserId must not be empty", nameof(ownerUserId));
        }

        if (budgetPurposeId == Guid.Empty)
        {
            throw new ArgumentException("BudgetPurposeId must not be empty", nameof(budgetPurposeId));
        }

        period.Validate();

        var purposeExists = await _db.BudgetPurposes.AsNoTracking().AnyAsync(p => p.Id == budgetPurposeId && p.OwnerUserId == ownerUserId, ct);
        if (!purposeExists)
        {
            throw new ArgumentException("Budget purpose not found", nameof(budgetPurposeId));
        }

        var exists = await _db.BudgetOverrides.AsNoTracking()
            .AnyAsync(o => o.OwnerUserId == ownerUserId && o.BudgetPurposeId == budgetPurposeId && o.PeriodYear == period.Year && o.PeriodMonth == period.Month, ct);

        if (exists)
        {
            throw new ArgumentException("Override already exists for the specified period");
        }

        var entity = new BudgetOverride(ownerUserId, budgetPurposeId, period, amount);
        _db.BudgetOverrides.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<BudgetOverrideDto?> UpdateAsync(Guid id, Guid ownerUserId, FinanceManager.Shared.Dtos.Budget.BudgetPeriodKey period, decimal amount, CancellationToken ct)
    {
        period.Validate();

        var entity = await _db.BudgetOverrides.FirstOrDefaultAsync(o => o.Id == id && o.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return null;
        }

        var duplicate = await _db.BudgetOverrides.AsNoTracking()
            .AnyAsync(o => o.OwnerUserId == ownerUserId
                && o.BudgetPurposeId == entity.BudgetPurposeId
                && o.Id != entity.Id
                && o.PeriodYear == period.Year
                && o.PeriodMonth == period.Month, ct);

        if (duplicate)
        {
            throw new ArgumentException("Override already exists for the specified period");
        }

        entity.SetPeriod(period);
        entity.SetAmount(amount);

        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.BudgetOverrides.FirstOrDefaultAsync(o => o.Id == id && o.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return false;
        }

        _db.BudgetOverrides.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<BudgetOverrideDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        return await _db.BudgetOverrides.AsNoTracking()
            .Where(o => o.Id == id && o.OwnerUserId == ownerUserId)
            .Select(o => new BudgetOverrideDto(o.Id, o.OwnerUserId, o.BudgetPurposeId, o.PeriodYear, o.PeriodMonth, o.Amount))
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BudgetOverrideDto>> ListByPurposeAsync(Guid ownerUserId, Guid budgetPurposeId, CancellationToken ct)
    {
        return await _db.BudgetOverrides.AsNoTracking()
            .Where(o => o.OwnerUserId == ownerUserId && o.BudgetPurposeId == budgetPurposeId)
            .OrderBy(o => o.PeriodYear)
            .ThenBy(o => o.PeriodMonth)
            .Select(o => new BudgetOverrideDto(o.Id, o.OwnerUserId, o.BudgetPurposeId, o.PeriodYear, o.PeriodMonth, o.Amount))
            .ToListAsync(ct);
    }

    private static BudgetOverrideDto Map(BudgetOverride o)
        => new(o.Id, o.OwnerUserId, o.BudgetPurposeId, o.PeriodYear, o.PeriodMonth, o.Amount);
}
