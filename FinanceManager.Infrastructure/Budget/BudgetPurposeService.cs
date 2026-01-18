using FinanceManager.Application.Budget;
using FinanceManager.Application.Exceptions;
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
    public async Task<BudgetPurposeDto> CreateAsync(Guid ownerUserId, string name, FinanceManager.Shared.Dtos.Budget.BudgetSourceType sourceType, Guid sourceId, string? description, Guid? budgetCategoryId, CancellationToken ct)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("OwnerUserId must not be empty", nameof(ownerUserId));
        }

        if (budgetCategoryId.HasValue && budgetCategoryId.Value != Guid.Empty)
        {
            var exists = await _db.BudgetCategories.AsNoTracking().AnyAsync(c => c.OwnerUserId == ownerUserId && c.Id == budgetCategoryId.Value, ct);
            if (!exists)
            {
                throw new ArgumentException("Budget category not found", nameof(budgetCategoryId));
            }
        }

        var entity = new BudgetPurpose(ownerUserId, name, (FinanceManager.Shared.Dtos.Budget.BudgetSourceType)sourceType, sourceId, description);
        entity.SetCategory(budgetCategoryId);

        _db.BudgetPurposes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<BudgetPurposeDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, FinanceManager.Shared.Dtos.Budget.BudgetSourceType sourceType, Guid sourceId, string? description, Guid? budgetCategoryId, CancellationToken ct)
    {
        var entity = await _db.BudgetPurposes.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            return null;
        }

        if (budgetCategoryId.HasValue && budgetCategoryId.Value != Guid.Empty)
        {
            var exists = await _db.BudgetCategories.AsNoTracking().AnyAsync(c => c.OwnerUserId == ownerUserId && c.Id == budgetCategoryId.Value, ct);
            if (!exists)
            {
                throw new ArgumentException("Budget category not found", nameof(budgetCategoryId));
            }

            // Invariant: category-scoped rules and purpose-scoped rules must not overlap for the same effective set.
            var hasPurposeRules = await _db.BudgetRules.AsNoTracking()
                .AnyAsync(r => r.OwnerUserId == ownerUserId && r.BudgetPurposeId == id, ct);

            var hasCategoryRules = await _db.BudgetRules.AsNoTracking()
                .AnyAsync(r => r.OwnerUserId == ownerUserId && r.BudgetCategoryId == budgetCategoryId.Value, ct);

            if (hasPurposeRules && hasCategoryRules)
            {
                throw new DomainValidationException(
                    "Err_Conflict_CategoryAndPurposeRules",
                    "Cannot assign category because both purpose-scoped and category-scoped rules would apply.");
            }
        }

        entity.Rename(name);
        entity.SetSource((FinanceManager.Shared.Dtos.Budget.BudgetSourceType)sourceType, sourceId);
        entity.SetDescription(description);
        entity.SetCategory(budgetCategoryId);

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
            .Select(p => new BudgetPurposeDto(p.Id, p.OwnerUserId, p.Name, p.Description, p.SourceType, p.SourceId, p.BudgetCategoryId))
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
            .Select(p => new BudgetPurposeDto(p.Id, p.OwnerUserId, p.Name, p.Description, (FinanceManager.Shared.Dtos.Budget.BudgetSourceType)p.SourceType, p.SourceId, p.BudgetCategoryId))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(Guid ownerUserId, CancellationToken ct)
    {
        return _db.BudgetPurposes.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId).CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BudgetPurposeOverviewDto>> ListOverviewAsync(
        Guid ownerUserId,
        int skip,
        int take,
        BudgetSourceType? sourceType,
        string? nameFilter,
        DateOnly? from,
        DateOnly? to,
        Guid? budgetCategoryId,
        CancellationToken ct,
        FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis dateBasis = FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate)
    {
        var query = _db.BudgetPurposes.AsNoTracking().Where(p => p.OwnerUserId == ownerUserId);

        if (budgetCategoryId.HasValue)
        {
            if (budgetCategoryId.Value == Guid.Empty)
            {
                query = query.Where(p => p.BudgetCategoryId == null);
            }
            else
            {
                query = query.Where(p => p.BudgetCategoryId == budgetCategoryId.Value);
            }
        }

        if (sourceType.HasValue)
        {
            query = query.Where(p => (BudgetSourceType)p.SourceType == sourceType.Value);
        }

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            var pattern = $"%{nameFilter.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        // Apply ordering for infinite list: first items with a category, then without; then by category name; then by purpose name.
        var orderedQuery =
            from p in query
            join c in _db.BudgetCategories.AsNoTracking().Where(x => x.OwnerUserId == ownerUserId)
                on p.BudgetCategoryId equals c.Id into catJoin
            from c in catJoin.DefaultIfEmpty()
            orderby (p.BudgetCategoryId != null) descending,
                    c.Name,
                    p.Name
            select new
            {
                p.Id,
                p.OwnerUserId,
                p.Name,
                p.Description,
                p.SourceType,
                p.SourceId,
                p.BudgetCategoryId,
                BudgetCategoryName = c != null ? c.Name : null
            };

        var purposes = await orderedQuery
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        if (purposes.Count == 0)
        {
            return Array.Empty<BudgetPurposeOverviewDto>();
        }

        // Normalize range
        var effectiveFrom = from;
        var effectiveTo = to;
        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo.Value < effectiveFrom.Value)
        {
            (effectiveFrom, effectiveTo) = (effectiveTo, effectiveFrom);
        }

        var purposeIds = purposes.Select(p => p.Id).ToList();

        // Load rules for purposes in the current page (single query)
        var rules = await _db.BudgetRules.AsNoTracking()
            .Where(r => r.OwnerUserId == ownerUserId && r.BudgetPurposeId != null && purposeIds.Contains(r.BudgetPurposeId.Value))
            .Select(r => new { r.BudgetPurposeId, r.Amount, r.Interval, r.CustomIntervalMonths, r.StartDate, r.EndDate })
            .ToListAsync(ct);

        var ruleCounts = rules
            .GroupBy(r => r.BudgetPurposeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var budgetSums = new Dictionary<Guid, decimal>();
        if (effectiveFrom.HasValue && effectiveTo.HasValue)
        {
            foreach (var g in rules.GroupBy(r => r.BudgetPurposeId))
            {
                if (g.Key == null || g.Key == Guid.Empty)
                {
                    continue;
                }

                var sum = 0m;
                foreach (var rule in g)
                {
                    sum += rule.Amount * CountOccurrencesInRange(rule.Interval, rule.CustomIntervalMonths, rule.StartDate, rule.EndDate, effectiveFrom.Value, effectiveTo.Value);
                }
                budgetSums[g.Key.Value] = sum;
            }
        }

        // Resolve source names + symbols for purposes in current page
        var contactIds = purposes.Where(p => (BudgetSourceType)p.SourceType == BudgetSourceType.Contact).Select(p => p.SourceId).Distinct().ToList();
        var groupIds = purposes.Where(p => (BudgetSourceType)p.SourceType == BudgetSourceType.ContactGroup).Select(p => p.SourceId).Distinct().ToList();
        var savingsPlanIds = purposes.Where(p => (BudgetSourceType)p.SourceType == BudgetSourceType.SavingsPlan).Select(p => p.SourceId).Distinct().ToList();

        var contactLookup = await _db.Contacts.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId && contactIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.SymbolAttachmentId })
            .ToDictionaryAsync(c => c.Id, c => (Name: c.Name, SymbolId: c.SymbolAttachmentId), ct);

        var groupLookup = await _db.ContactCategories.AsNoTracking()
            .Where(g => g.OwnerUserId == ownerUserId && groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.SymbolAttachmentId })
            .ToDictionaryAsync(g => g.Id, g => (Name: g.Name, SymbolId: g.SymbolAttachmentId), ct);

        var savingsPlanLookup = await _db.SavingsPlans.AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId && savingsPlanIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.SymbolAttachmentId })
            .ToDictionaryAsync(s => s.Id, s => (Name: s.Name, SymbolId: s.SymbolAttachmentId), ct);

        (string? Name, Guid? SymbolId) ResolveSource(object srcTypeObj, Guid srcId)
        {
            var st = (BudgetSourceType)srcTypeObj;
            return st switch
            {
                BudgetSourceType.Contact => contactLookup.TryGetValue(srcId, out var c) ? (c.Name, c.SymbolId) : (null, null),
                BudgetSourceType.ContactGroup => groupLookup.TryGetValue(srcId, out var g) ? (g.Name, g.SymbolId) : (null, null),
                BudgetSourceType.SavingsPlan => savingsPlanLookup.TryGetValue(srcId, out var sp) ? (sp.Name, sp.SymbolId) : (null, null),
                _ => (null, null)
            };
        }

        // Compute actuals for selected range
        var actuals = new Dictionary<Guid, decimal>();
        if (effectiveFrom.HasValue && effectiveTo.HasValue)
        {
            var fromDt = effectiveFrom.Value.ToDateTime(TimeOnly.MinValue);
            var toDt = effectiveTo.Value.ToDateTime(TimeOnly.MaxValue);

            // Contact
            if (contactIds.Count > 0)
            {
                var contactQuery = _db.Postings.AsNoTracking()
                    .Where(p => p.ContactId != null && contactIds.Contains(p.ContactId.Value));

                contactQuery = dateBasis == FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.ValutaDate
                     ? contactQuery.Where(p => p.ValutaDate != null && p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
                     : contactQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

                var contactActuals = await contactQuery
                     .GroupBy(p => p.ContactId!.Value)
                     .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Amount) })
                     .ToListAsync(ct);

                foreach (var a in contactActuals)
                {
                    actuals[a.Id] = a.Sum;
                }
            }

            // SavingsPlan
            if (savingsPlanIds.Count > 0)
            {
                var planQuery = _db.Postings.AsNoTracking()
                    .Where(p => p.SavingsPlanId != null && savingsPlanIds.Contains(p.SavingsPlanId.Value));

                planQuery = dateBasis == FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.ValutaDate
                     ? planQuery.Where(p => p.ValutaDate != null && p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
                     : planQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

                var planActuals = await planQuery
                     .GroupBy(p => p.SavingsPlanId!.Value)
                     .Select(g => new { Id = g.Key, Sum = g.Sum(x => -x.Amount) })
                     .ToListAsync(ct);

                foreach (var a in planActuals)
                {
                    actuals[a.Id] = a.Sum;
                }
            }

            // ContactGroup -> aggregate postings for contacts in that category
            if (groupIds.Count > 0)
            {
                var groupContactIds = await _db.Contacts.AsNoTracking()
                    .Where(c => c.OwnerUserId == ownerUserId && c.CategoryId != null && groupIds.Contains(c.CategoryId.Value))
                    .Select(c => new { GroupId = c.CategoryId!.Value, ContactId = c.Id })
                    .ToListAsync(ct);

                var grouped = groupContactIds
                    .GroupBy(x => x.GroupId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ContactId).ToList());

                foreach (var group in grouped)
                {
                    var ids = group.Value;
                    if (ids.Count == 0)
                    {
                        actuals[group.Key] = 0m;
                        continue;
                    }

                    var groupQuery = _db.Postings.AsNoTracking()
                        .Where(p => p.ContactId != null && ids.Contains(p.ContactId.Value));

                    groupQuery = dateBasis == FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.ValutaDate
                         ? groupQuery.Where(p => p.ValutaDate != null && p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
                         : groupQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

                    var sum = await groupQuery.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

                    actuals[group.Key] = sum;
                }
            }
        }

        return purposes
            .Select(p =>
            {
                ruleCounts.TryGetValue(p.Id, out var rc);
                budgetSums.TryGetValue(p.Id, out var bs);
                var src = ResolveSource(p.SourceType, p.SourceId);
                actuals.TryGetValue(p.SourceId, out var actual);

                var variance = actual - bs;

                return new BudgetPurposeOverviewDto(
                    p.Id,
                    p.OwnerUserId,
                    p.Name,
                    p.Description,
                    (BudgetSourceType)p.SourceType,
                    p.SourceId,
                    rc,
                    bs,
                    actual,
                    variance,
                    src.Name,
                    src.SymbolId,
                    p.BudgetCategoryId,
                    p.BudgetCategoryName);
            })
            .ToList();
    }

    private static int CountOccurrencesInRange(
        BudgetIntervalType interval,
        int? customIntervalMonths,
        DateOnly start,
        DateOnly? end,
        DateOnly from,
        DateOnly to)
    {
        var actualEnd = end ?? DateOnly.MaxValue;
        if (start > to || actualEnd < from)
        {
            return 0;
        }

        var effectiveStart = start > from ? start : from;
        var effectiveEnd = actualEnd < to ? actualEnd : to;

        // Align to the rule's day-of-month when stepping.
        DateOnly AddMonthsSafe(DateOnly d, int months)
        {
            var dt = d.ToDateTime(TimeOnly.MinValue);
            var next = dt.AddMonths(months);
            return DateOnly.FromDateTime(next);
        }

        int occurrences = 0;

        switch (interval)
        {
            case BudgetIntervalType.Monthly:
                occurrences = CountByMonthStep(start, effectiveStart, effectiveEnd, stepMonths: 1);
                break;

            case BudgetIntervalType.Yearly:
                occurrences = CountByMonthStep(start, effectiveStart, effectiveEnd, stepMonths: 12);
                break;

            case BudgetIntervalType.CustomMonths:
                var step = Math.Max(1, customIntervalMonths ?? 1);
                occurrences = CountByMonthStep(start, effectiveStart, effectiveEnd, stepMonths: step);
                break;

            default:
                // Fallback: treat as monthly to avoid returning misleading sums.
                occurrences = CountByMonthStep(start, effectiveStart, effectiveEnd, stepMonths: 1);
                break;
        }

        return occurrences;

        int CountByMonthStep(DateOnly ruleStart, DateOnly rangeStart, DateOnly rangeEnd, int stepMonths)
        {
            // Find the first occurrence >= rangeStart
            var occ = ruleStart;
            if (occ < rangeStart)
            {
                // advance in chunks
                var monthsDiff = (rangeStart.Year - occ.Year) * 12 + (rangeStart.Month - occ.Month);
                var stepsToAdvance = monthsDiff / stepMonths;
                if (stepsToAdvance > 0)
                {
                    occ = AddMonthsSafe(occ, stepsToAdvance * stepMonths);
                }

                while (occ < rangeStart)
                {
                    occ = AddMonthsSafe(occ, stepMonths);
                }
            }

            var count = 0;
            while (occ <= rangeEnd)
            {
                if (occ >= rangeStart && occ <= rangeEnd)
                {
                    count++;
                }
                occ = AddMonthsSafe(occ, stepMonths);
            }

            return count;
        }
    }

    private static BudgetPurposeDto Map(BudgetPurpose p)
        => new(p.Id, p.OwnerUserId, p.Name, p.Description, (FinanceManager.Shared.Dtos.Budget.BudgetSourceType)p.SourceType, p.SourceId, p.BudgetCategoryId);
}
