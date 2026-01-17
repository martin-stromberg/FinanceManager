using FinanceManager.Application.Budget;
using FinanceManager.Shared.Dtos.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// Implementation of <see cref="IBudgetReportService"/> backed by existing budget overview services.
/// </summary>
public sealed class BudgetReportService : IBudgetReportService
{
    private readonly IBudgetPurposeService _purposes;
    private readonly IBudgetCategoryService _categories;
    private readonly AppDbContext _db;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetReportService(IBudgetPurposeService purposes, IBudgetCategoryService categories, AppDbContext db)
    {
        _purposes = purposes;
        _categories = categories;
        _db = db;
    }

    /// <inheritdoc />
    public async Task<BudgetReportDto> GetAsync(Guid ownerUserId, BudgetReportRequest request, CancellationToken ct)
    {
        var months = Math.Clamp(request.Months, 1, 60);

        var asOf = request.AsOfDate;
        var rangeTo = EndOfMonth(asOf);
        var rangeFrom = StartOfMonth(asOf.AddMonths(-(months - 1)));

        var periods = BuildPeriodBoundaries(rangeFrom, rangeTo, request.Interval);

        var periodDtos = new List<BudgetReportPeriodDto>(periods.Count);

        IReadOnlyList<BudgetPurposeOverviewDto> purposesForDetails = Array.Empty<BudgetPurposeOverviewDto>();
        IReadOnlyList<BudgetCategoryOverviewDto> categoriesForDetails = Array.Empty<BudgetCategoryOverviewDto>();

        for (var i = 0; i < periods.Count; i++)
        {
            var (from, to) = periods[i];

            var purposes = await _purposes.ListOverviewAsync(
                ownerUserId,
                skip: 0,
                take: 5000,
                sourceType: null,
                nameFilter: null,
                from: from,
                to: to,
                budgetCategoryId: null,
                ct: ct,
                dateBasis: request.DateBasis);

            var budget = purposes.Sum(p => p.BudgetSum);
            var actual = purposes.Sum(p => p.ActualSum);
            var delta = budget - actual;
            var pct = budget == 0m ? 0m : (delta / budget) * 100m;

            periodDtos.Add(new BudgetReportPeriodDto(from, to, budget, actual, delta, pct));

            if (i == periods.Count - 1 && request.CategoryValueScope == BudgetReportValueScope.LastInterval)
            {
                purposesForDetails = purposes;
                categoriesForDetails = await _categories.ListOverviewAsync(ownerUserId, from, to, ct);
            }
        }

        if (request.ShowDetailsTable && request.CategoryValueScope == BudgetReportValueScope.TotalRange)
        {
            purposesForDetails = await _purposes.ListOverviewAsync(
                ownerUserId,
                skip: 0,
                take: 5000,
                sourceType: null,
                nameFilter: null,
                from: rangeFrom,
                to: rangeTo,
                budgetCategoryId: null,
                ct: ct,
                dateBasis: request.DateBasis);

            categoriesForDetails = await _categories.ListOverviewAsync(ownerUserId, rangeFrom, rangeTo, ct);
        }

        var categoryDtos = request.ShowDetailsTable
            ? await BuildCategoriesAsync(ownerUserId, categoriesForDetails, purposesForDetails, request.IncludePurposeRows, rangeFrom, rangeTo, request.DateBasis, ct)
            : Array.Empty<BudgetReportCategoryDto>();

        return new BudgetReportDto(rangeFrom, rangeTo, request.Interval, periodDtos, categoryDtos);
    }

    private async Task<IReadOnlyList<BudgetReportCategoryDto>> BuildCategoriesAsync(
        Guid ownerUserId,
        IReadOnlyList<BudgetCategoryOverviewDto> categories,
        IReadOnlyList<BudgetPurposeOverviewDto> purposes,
        bool includePurposeRows,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        var purposeLookup = purposes
            .Where(p => p.BudgetCategoryId.HasValue)
            .GroupBy(p => p.BudgetCategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<BudgetReportCategoryDto>(categories.Count + 1);

        // Add a synthetic row for purposes that are not assigned to any category.
        // (This keeps the details table useful even when users don't use categories.)
        var unassignedPurposes = purposes.Where(p => p.BudgetCategoryId == null).ToList();
        if (unassignedPurposes.Count > 0)
        {
            var purposeRows = includePurposeRows
                ? unassignedPurposes
                    .OrderBy(p => p.Name)
                    .Select(p =>
                    {
                        var delta = p.BudgetSum - p.ActualSum;
                        var pct = p.BudgetSum == 0m ? 0m : (delta / p.BudgetSum) * 100m;
                        return new BudgetReportPurposeDto(p.Id, p.Name, p.BudgetSum, p.ActualSum, delta, pct);
                    })
                    .ToList()
                : new List<BudgetReportPurposeDto>();

            var budget = unassignedPurposes.Sum(p => p.BudgetSum);
            var actual = unassignedPurposes.Sum(p => p.ActualSum);
            var deltaCat = budget - actual;
            var pctCat = budget == 0m ? 0m : (deltaCat / budget) * 100m;

            result.Add(new BudgetReportCategoryDto(
                Id: Guid.Empty,
                Name: "(Unassigned)",
                Kind: BudgetReportCategoryRowKind.Data,
                Budget: budget,
                Actual: actual,
                Delta: deltaCat,
                DeltaPct: pctCat,
                Purposes: purposeRows));
        }

        foreach (var cat in categories.OrderBy(c => c.Name))
        {
            purposeLookup.TryGetValue(cat.Id, out var list);
            list ??= new List<BudgetPurposeOverviewDto>();

            var purposeRows = includePurposeRows
                ? list
                    .OrderBy(p => p.Name)
                    .Select(p =>
                    {
                        var delta = p.BudgetSum - p.ActualSum;
                        var pct = p.BudgetSum == 0m ? 0m : (delta / p.BudgetSum) * 100m;
                        return new BudgetReportPurposeDto(p.Id, p.Name, p.BudgetSum, p.ActualSum, delta, pct);
                    })
                    .ToList()
                : new List<BudgetReportPurposeDto>();

            var deltaCat = cat.Budget - cat.Actual;
            var pctCat = cat.Budget == 0m ? 0m : (deltaCat / cat.Budget) * 100m;

            result.Add(new BudgetReportCategoryDto(
                cat.Id,
                cat.Name,
                BudgetReportCategoryRowKind.Data,
                cat.Budget,
                cat.Actual,
                deltaCat,
                pctCat,
                purposeRows));
        }

        // Unbudgeted postings logic:
        // actualTotal: sum of all contact postings in range (includes self-contact postings)
        // purposeActualTotal: sum of purpose actuals, but correct overlaps:
        // - if ContactGroup purposes exist, their actuals already include member contacts, so add back Contact purposes that are covered
        // - if Self-contact purpose exists, it includes savings plan postings; subtract SavingsPlan purpose actuals

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        // Contact postings total for the report range.
        var actualTotalQuery = _db.Postings.AsNoTracking()
            .Where(p => p.ContactId != null);

        actualTotalQuery = dateBasis == BudgetReportDateBasis.ValutaDate
            ? actualTotalQuery.Where(p => p.ValutaDate != null && p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
            : actualTotalQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

        var actualTotal = await actualTotalQuery
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        // Start with all purpose actuals.
        var purposeActualTotal = purposes.Sum(p => p.ActualSum);

        // ContactGroup overlap: group actuals include contacts
        var groupPurposes = purposes.Where(p => p.SourceType == BudgetSourceType.ContactGroup).ToList();
        if (groupPurposes.Count > 0)
        {
            var groupIds = groupPurposes.Select(p => p.SourceId).Distinct().ToList();

            var groupContactPairs = await _db.Contacts.AsNoTracking()
                .Where(c => c.OwnerUserId == ownerUserId && c.CategoryId != null && groupIds.Contains(c.CategoryId.Value))
                .Select(c => new { GroupId = c.CategoryId!.Value, ContactId = c.Id })
                .ToListAsync(ct);

            var groupContactIds = groupContactPairs.Select(x => x.ContactId).Distinct().ToHashSet();

            var coveredContactActuals = purposes
                .Where(p => p.SourceType == BudgetSourceType.Contact && groupContactIds.Contains(p.SourceId))
                .Sum(p => p.ActualSum);

            // Group purposes already include the member contact actuals.
            // If there are also contact purposes for covered contacts, they would double-count actuals.
            purposeActualTotal -= coveredContactActuals;
        }

        // Self-contact overlap with savings plans: if a self-contact purpose exists, savings plan purpose actuals must be ignored
        // because savings plan postings are also booked on the self-contact.
        var selfContactId = await _db.Contacts.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId && c.Type == FinanceManager.Shared.Dtos.Contacts.ContactType.Self)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (selfContactId.HasValue)
        {
            var hasSelfContactPurpose = purposes.Any(p => p.SourceType == BudgetSourceType.Contact && p.SourceId == selfContactId.Value);
            if (hasSelfContactPurpose)
            {
                var savingsPlanActuals = purposes
                    .Where(p => p.SourceType == BudgetSourceType.SavingsPlan)
                    .Sum(p => p.ActualSum);

                purposeActualTotal -= savingsPlanActuals;
            }
        }

        var unbudgetedActual = actualTotal - purposeActualTotal;

        // Prevent duplicates if this method is called with already augmented data.
        // (Shouldn't happen, but keeps output stable.)
        if (result.Any(r => r.Id == Guid.Empty && (r.Name == "Sum" || r.Name == "Unbudgeted" || r.Name == "Result")))
        {
            return result;
        }

        // If there are no data rows (no categories and no unassigned purposes), keep the output empty.
        if (result.Count == 0)
        {
            return result;
        }

        // 1) Sum row: sum of budgeted categories.
        // Keep delta convention consistent with other rows: Delta = Actual - Budget.
        var sumBudget = result.Sum(r => r.Budget);
        var sumActual = result.Sum(r => r.Actual);
        var sumDelta = sumActual - sumBudget;
        var sumPct = sumBudget == 0m ? 0m : (sumDelta / sumBudget) * 100m;

        result.Add(new BudgetReportCategoryDto(
            Id: Guid.Empty,
            Name: "Sum",
            Kind: BudgetReportCategoryRowKind.Sum,
            Budget: sumBudget,
            Actual: sumActual,
            Delta: sumDelta,
            DeltaPct: sumPct,
            Purposes: new List<BudgetReportPurposeDto>()));

        // 2) Unbudgeted bookings row.
        // This row has no budget, so Delta equals Actual.
        if (unbudgetedActual != 0m)
        {
            result.Add(new BudgetReportCategoryDto(
                Id: Guid.Empty,
                Name: "Unbudgeted",
                Kind: BudgetReportCategoryRowKind.Unbudgeted,
                Budget: 0m,
                Actual: unbudgetedActual,
                Delta: 0m,
                DeltaPct: 0m,
                Purposes: new List<BudgetReportPurposeDto>()));
        }

        // 3) Result row: sum + unbudgeted.
        var totalActual = sumActual + unbudgetedActual;
        var totalDelta = totalActual - sumBudget;
        var totalPct = sumBudget == 0m ? 0m : (totalDelta / sumBudget) * 100m;

        result.Add(new BudgetReportCategoryDto(
            Id: Guid.Empty,
            Name: "Result",
            Kind: BudgetReportCategoryRowKind.Result,
            Budget: sumBudget,
            Actual: totalActual,
            Delta: totalDelta,
            DeltaPct: totalPct,
            Purposes: new List<BudgetReportPurposeDto>()));

        return result;
    }

    private static IReadOnlyList<(DateOnly From, DateOnly To)> BuildPeriodBoundaries(DateOnly from, DateOnly to, BudgetReportInterval interval)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var list = new List<(DateOnly From, DateOnly To)>();
        var cur = StartOfMonth(from);
        var end = EndOfMonth(to);

        var stepMonths = interval switch
        {
            BudgetReportInterval.Month => 1,
            BudgetReportInterval.Quarter => 3,
            BudgetReportInterval.Year => 12,
            _ => 1
        };

        while (cur <= end)
        {
            var pFrom = cur;
            var pTo = EndOfMonth(cur.AddMonths(stepMonths - 1));
            if (pTo > end)
            {
                pTo = end;
            }

            list.Add((pFrom, pTo));
            cur = cur.AddMonths(stepMonths);
        }

        return list;
    }

    private static DateOnly StartOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    private static DateOnly EndOfMonth(DateOnly d)
        => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
}
