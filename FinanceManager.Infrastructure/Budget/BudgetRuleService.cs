using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// EF Core based implementation of <see cref="IBudgetRuleService"/>.
/// </summary>
public sealed class BudgetRuleService : IBudgetRuleService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">App database context.</param>
    public BudgetRuleService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<BudgetRuleDto> CreateAsync(Guid ownerUserId, Guid budgetPurposeId, decimal amount, FinanceManager.Shared.Dtos.Budget.BudgetIntervalType interval, int? customIntervalMonths, DateOnly startDate, DateOnly? endDate, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("OwnerUserId must not be empty", nameof(ownerUserId));
        }

        if (budgetPurposeId == Guid.Empty)
        {
            throw new ArgumentException("BudgetPurposeId must not be empty", nameof(budgetPurposeId));
        }

        var purposeExists = await _db.BudgetPurposes.AsNoTracking().AnyAsync(p => p.Id == budgetPurposeId && p.OwnerUserId == ownerUserId, ct);
        if (!purposeExists)
        {
            throw new ArgumentException("Budget purpose not found", nameof(budgetPurposeId));
        }

        var entity = new BudgetRule(ownerUserId, budgetPurposeId, amount, (FinanceManager.Shared.Dtos.Budget.BudgetIntervalType)interval, startDate, endDate, customIntervalMonths);
        _db.BudgetRules.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<BudgetRuleDto?> UpdateAsync(Guid id, Guid ownerUserId, decimal amount, FinanceManager.Shared.Dtos.Budget.BudgetIntervalType interval, int? customIntervalMonths, DateOnly startDate, DateOnly? endDate, CancellationToken ct)
    {
        var entity = await _db.BudgetRules.FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return null;
        }

        entity.SetAmount(amount);
        entity.SetSchedule((FinanceManager.Shared.Dtos.Budget.BudgetIntervalType)interval, startDate, endDate, customIntervalMonths);

        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.BudgetRules.FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return false;
        }

        _db.BudgetRules.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<BudgetRuleDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        return await _db.BudgetRules.AsNoTracking()
            .Where(r => r.Id == id && r.OwnerUserId == ownerUserId)
            .Select(r => new BudgetRuleDto(r.Id, r.OwnerUserId, r.BudgetPurposeId, r.Amount, (FinanceManager.Shared.Dtos.Budget.BudgetIntervalType)r.Interval, r.CustomIntervalMonths, r.StartDate, r.EndDate))
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BudgetRuleDto>> ListByPurposeAsync(Guid ownerUserId, Guid budgetPurposeId, CancellationToken ct)
    {
        return await _db.BudgetRules.AsNoTracking()
            .Where(r => r.OwnerUserId == ownerUserId && r.BudgetPurposeId == budgetPurposeId)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.Interval)
            .Select(r => new BudgetRuleDto(r.Id, r.OwnerUserId, r.BudgetPurposeId, r.Amount, (FinanceManager.Shared.Dtos.Budget.BudgetIntervalType)r.Interval, r.CustomIntervalMonths, r.StartDate, r.EndDate))
            .ToListAsync(ct);
    }

    private static BudgetRuleDto Map(BudgetRule r)
        => new(r.Id, r.OwnerUserId, r.BudgetPurposeId, r.Amount, (FinanceManager.Shared.Dtos.Budget.BudgetIntervalType)r.Interval, r.CustomIntervalMonths, r.StartDate, r.EndDate);
}
