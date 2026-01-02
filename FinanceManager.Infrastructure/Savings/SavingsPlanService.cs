using System.Linq;
using FinanceManager.Application.Savings;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Savings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Savings;

/// <summary>
/// Service providing operations for managing savings plans (CRUD, analysis and attachments).
/// </summary>
public sealed class SavingsPlanService : ISavingsPlanService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsPlanService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query and persist savings plans and related entities.</param>
    public SavingsPlanService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Lists savings plans for a user, optionally filtering to only active plans.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="onlyActive">When <c>true</c> only active plans are returned; otherwise all plans are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="SavingsPlanDto"/> matching the criteria (may be empty).</returns>
    public async Task<IReadOnlyList<SavingsPlanDto>> ListAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct)
    {
        var plans = from p in _db.SavingsPlans.AsNoTracking()
                    where p.OwnerUserId == ownerUserId && (!onlyActive || p.IsActive)
                    join c in _db.SavingsPlanCategories.AsNoTracking() on p.CategoryId equals c.Id into pcs
                    from cat in pcs.DefaultIfEmpty()
                    orderby p.Name
                    select new SavingsPlanDto(
                        p.Id,
                        p.Name,
                        p.Type,
                        p.Type == SavingsPlanType.Open ? null : p.TargetAmount,
                        p.TargetDate,
                        p.Interval,
                        p.IsActive,
                        p.CreatedUtc,
                        p.ArchivedUtc,
                        p.CategoryId,
                        p.ContractNumber,
                        p.SymbolAttachmentId ?? (cat != null ? cat.SymbolAttachmentId : null),
                        0m,
                        0m
                    );

        return await plans.ToListAsync(ct);
    }

    /// <summary>
    /// Gets a savings plan by id for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the savings plan.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="SavingsPlanDto"/> when found; otherwise <c>null</c>.</returns>
    public async Task<SavingsPlanDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return null; }

        var today = DateTime.Today;
        // sum of postings for this savings plan up to today
        var accumulated = await _db.Postings.AsNoTracking()
            .Where(p => p.SavingsPlanId == id && p.Kind == PostingKind.SavingsPlan && p.BookingDate <= today)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        decimal remaining = 0m;
        if (plan.TargetAmount is not null)
        {
            remaining = Math.Max(0m, plan.TargetAmount.Value - accumulated);
        }

        return new SavingsPlanDto(plan.Id, plan.Name, plan.Type, plan.TargetAmount, plan.TargetDate, plan.Interval, plan.IsActive, plan.CreatedUtc, plan.ArchivedUtc, plan.CategoryId, plan.ContractNumber, plan.SymbolAttachmentId, remaining, accumulated);
    }

    /// <summary>
    /// Creates a new savings plan for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Name of the savings plan. Must be unique per user when provided.</param>
    /// <param name="type">Type of savings plan.</param>
    /// <param name="targetAmount">Optional target amount for closed plans.</param>
    /// <param name="targetDate">Optional target date for reaching the target amount.</param>
    /// <param name="interval">Optional contribution interval.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <param name="contractNumber">Optional contract number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="SavingsPlanDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when a plan with the same name already exists for the user.</exception>
    public async Task<SavingsPlanDto> CreateAsync(Guid ownerUserId, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId, string? contractNumber, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var exists = await _db.SavingsPlans.AnyAsync(p => p.OwnerUserId == ownerUserId && p.Name == name, ct);
            if (exists) { throw new ArgumentException("Savings plan name must be unique per user", nameof(name)); }
        }
        var plan = new SavingsPlan(ownerUserId, name, type, targetAmount, targetDate, interval, categoryId);
        plan.SetContractNumber(contractNumber);
        _db.SavingsPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return new SavingsPlanDto(plan.Id, plan.Name, plan.Type, plan.TargetAmount, plan.TargetDate, plan.Interval, plan.IsActive, plan.CreatedUtc, plan.ArchivedUtc, plan.CategoryId, plan.ContractNumber, plan.SymbolAttachmentId);
    }

    /// <summary>
    /// Updates an existing savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to update.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="name">New name of the plan.</param>
    /// <param name="type">New type.</param>
    /// <param name="targetAmount">New target amount.</param>
    /// <param name="targetDate">New target date.</param>
    /// <param name="interval">New interval.</param>
    /// <param name="categoryId">New category id.</param>
    /// <param name="contractNumber">New contract number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="SavingsPlanDto"/>, or <c>null</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the new name conflicts with another plan of the same user.</exception>
    public async Task<SavingsPlanDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId, string? contractNumber, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return null; }
        if (!string.Equals(plan.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _db.SavingsPlans.AnyAsync(p => p.OwnerUserId == ownerUserId && p.Name == name && p.Id != id, ct);
            if (exists) { throw new ArgumentException("Savings plan name must be unique per user", nameof(name)); }
        }
        plan.Rename(name);
        plan.ChangeType(type);
        plan.SetTarget(targetAmount, targetDate);
        plan.SetInterval(interval);
        plan.SetCategory(categoryId);
        plan.SetContractNumber(contractNumber);
        await _db.SaveChangesAsync(ct);
        return new SavingsPlanDto(plan.Id, plan.Name, plan.Type, plan.TargetAmount, plan.TargetDate, plan.Interval, plan.IsActive, plan.CreatedUtc, plan.ArchivedUtc, plan.CategoryId, plan.ContractNumber, plan.SymbolAttachmentId);
    }

    /// <summary>
    /// Archives a savings plan (marks it as inactive).
    /// </summary>
    /// <param name="id">Identifier of the plan to archive.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the plan was found and archived; otherwise <c>false</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the plan is already archived.</exception>
    public async Task<bool> ArchiveAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return false; }
        if (!plan.IsActive) { throw new ArgumentException("Savings plan is already archived", "ArchiveState"); }
        plan.Archive();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Deletes a savings plan if business rules allow it (archived and not referenced).
    /// </summary>
    /// <param name="id">Identifier of the plan to delete.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the plan existed and was deleted; otherwise <c>false</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when deletion is blocked by business rules (active, referenced by entries/postings/drafts).</exception>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) { return false; }

        // Business rules:
        // 1. Only archived plans can be deleted (avoid accidental data loss of active plans).
        if (plan.IsActive) { throw new ArgumentException("Savings plan must be archived before it can be deleted", "IsActive"); }

        // 2. Prevent deletion if referenced by committed statement entries.
        bool hasStatementEntries = await _db.StatementEntries.AsNoTracking().AnyAsync(e => e.SavingsPlanId == id, ct);
        if (hasStatementEntries) { throw new ArgumentException("Savings plan is referenced by committed statement entries and cannot be deleted", "StatementEntries"); }

        // 3. Prevent deletion if referenced by draft entries (user must remove assignments first).
        bool hasDraftEntries = await _db.StatementDraftEntries.AsNoTracking().AnyAsync(e => e.SavingsPlanId == id, ct);
        if (hasDraftEntries) { throw new ArgumentException("Savings plan is referenced by draft entries and cannot be deleted", "DraftEntries"); }

        // 4. Prevent deletion if postings reference the plan (future feature; safety for already created postings).
        bool hasPostings = await _db.Postings.AsNoTracking().AnyAsync(p => p.SavingsPlanId == id, ct);
        if (hasPostings) { throw new ArgumentException("Savings plan is referenced by postings and cannot be deleted", "Postings"); }

        // Delete attachments of this savings plan
        await _db.Attachments
            .Where(a => a.OwnerUserId == ownerUserId && a.EntityKind == AttachmentEntityKind.SavingsPlan && a.EntityId == plan.Id)
            .ExecuteDeleteAsync(ct);

        _db.SavingsPlans.Remove(plan);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Computes an analysis for the savings plan (accumulated contributions, forecast and required monthly amount).
    /// </summary>
    /// <param name="id">Identifier of the plan to analyze.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SavingsPlanAnalysisDto"/> containing the analysis results. When the plan is not found a default result with Exists=false is returned.</returns>
    public async Task<SavingsPlanAnalysisDto> AnalyzeAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null)
        {
            return new SavingsPlanAnalysisDto(id, false, null, null, 0m, 0m, 0);
        }

        var today = DateTime.Today;

        // Past contributions up to today for this plan (always compute accumulated amount)
        var history = await _db.Postings.AsNoTracking()
            .Where(p => p.SavingsPlanId == id && p.Kind == PostingKind.SavingsPlan && p.BookingDate <= today)
            .Select(p => new { p.BookingDate, p.Amount })
            .ToListAsync(ct);

        var accumulated = history.Sum(x => x.Amount);

        // If no target defined, return accumulated amount and no forecast
        if (plan.TargetAmount is null || plan.TargetDate is null)
        {
            return new SavingsPlanAnalysisDto(id, true, plan.TargetAmount, plan.TargetDate, accumulated, 0m, 0);
        }

        var endDate = plan.TargetDate.Value.Date;
        var monthsRemaining = Math.Max(0, ((endDate.Year - today.Year) * 12 + endDate.Month - today.Month));

        var target = plan.TargetAmount.Value;

        if (monthsRemaining <= 0)
        {
            var reached = accumulated >= target;
            return new SavingsPlanAnalysisDto(id, reached, target, endDate, accumulated, 0m, 0);
        }

        // Average monthly saving based on history grouped by month
        var monthlyTotals = history
            .GroupBy(x => new { x.BookingDate.Year, x.BookingDate.Month })
            .Select(g => g.Sum(x => x.Amount))
            .ToList();
        var averagePerMonth = monthlyTotals.Count > 0 ? monthlyTotals.Average() : 0m;

        var forecast = averagePerMonth * monthsRemaining;
        var totalExpected = accumulated + forecast;
        var reachable = totalExpected >= target;

        var remaining = Math.Max(0m, target - accumulated);
        var requiredMonthly = monthsRemaining > 0 ? remaining / monthsRemaining : 0m;

        return new SavingsPlanAnalysisDto(id, reachable, target, endDate, accumulated, requiredMonthly, monthsRemaining);
    }

    /// <summary>
    /// Counts savings plans for the owner, optionally filtering to active plans.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="onlyActive">When <c>true</c> only active plans are counted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of matching savings plans.</returns>
    public Task<int> CountAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct)
    {
        var q = _db.SavingsPlans.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId);
        if (onlyActive) { q = q.Where(p => p.IsActive); }
        return q.CountAsync(ct);
    }

    /// <summary>
    /// Sets or clears a symbol attachment reference for the savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan.</param>
    /// <param name="ownerUserId">Owner user identifier for ownership validation.</param>
    /// <param name="attachmentId">Attachment identifier to set, or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the change has been persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when the savings plan is not found or not owned by the user.</exception>
    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var plan = await _db.SavingsPlans.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (plan == null) throw new ArgumentException("Savings plan not found", nameof(id));
        plan.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }
}