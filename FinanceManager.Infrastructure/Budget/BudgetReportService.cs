using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Postings;
using FinanceManager.Application.Savings;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Application.Securities;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// Implementation of <see cref="IBudgetReportService"/> backed by existing budget overview services.
/// </summary>
/// <summary>
/// Service that produces budget reports and KPIs.
/// Implements <see cref="IBudgetReportService"/> using underlying purpose and category services
/// and the application's <see cref="AppDbContext"/> for postings/contacts data.
/// </summary>
/// <summary>
/// Service that builds budget report data.
/// </summary>
public sealed class BudgetReportService : IBudgetReportService
{
    private readonly IBudgetPurposeService _purposes;
    private readonly IBudgetCategoryService _categories;
    private readonly IBudgetRuleService _rules;
    private readonly IPostingsQueryService _postings;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;

    /// <summary>
    /// Creates a new <see cref="BudgetReportService"/>.
    /// </summary>
    /// <param name="purposes">Service providing budget purpose overviews.</param>
    /// <param name="categories">Service providing budget category overviews.</param>
    /// <param name="rules">Service providing budget rule overviews.</param>
    /// <param name="postings">Service for retrieving individual postings.</param>
    /// <param name="contacts">Service providing contacts for the owner.</param>
    /// <param name="savingsPlans">Service providing savings plans for the owner.</param>
    public BudgetReportService(
        IBudgetPurposeService purposes,
        IBudgetCategoryService categories,
        IBudgetRuleService rules,
        IPostingsQueryService postings,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        FinanceManager.Application.Securities.ISecurityService securities)
    {
        _purposes = purposes;
        _categories = categories;
        _rules = rules;
        _postings = postings;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
    }

    private async Task<List<BudgetReportPostingRawDataDto>> BuildPostingDtosAsync(
        Guid ownerUserId,
        IReadOnlyList<BudgetPurposeOverviewDto> purposes,
        IReadOnlyList<BudgetCategoryOverviewDto> categories,
        IReadOnlyList<ContactDto> contacts,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var contactPostings = new List<PostingServiceDto>();
        foreach (var contact in contacts)
        {
            var postings = await _postings.GetContactPostingsAsync(contact.Id, 0, 5000, null, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MaxValue), ownerUserId, ct);
            contactPostings.AddRange(postings);
        }

        var savingsPlans = await _savingsPlans.ListAsync(ownerUserId, onlyActive: true, ct);
        var savingsPlanPostings = new List<PostingServiceDto>();
        foreach (var plan in savingsPlans)
        {
            var sp = await _postings.GetSavingsPlanPostingsAsync(plan.Id, 0, 5000, null, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MaxValue), ownerUserId, ct);
            savingsPlanPostings.AddRange(sp);
        }

        var securities = await _securities.ListAsync(ownerUserId, false, ct);
        var securityPostings = new List<PostingServiceDto>();
        foreach (var security in securities)
        {     
            var sp = await _postings.GetSecurityPostingsAsync(security.Id, 0, 5000, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MaxValue), ownerUserId, ct);
            securityPostings.AddRange(sp);
        }

        // Deduplicate contact postings by id
        contactPostings = contactPostings.GroupBy(p => p.Id).Select(g => g.First()).ToList();

        // Build lookup for savings-plan postings by GroupId to supplement contact postings
        var spByGroup = savingsPlanPostings
            .Where(p => p.GroupId != Guid.Empty)
            .GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.First());
        var secNyGroup = securityPostings
            .Where(p => p.GroupId != Guid.Empty)
            .GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.First());

        var postingDtos = contactPostings.Select(p =>
        {
            Guid? spId = p.SavingsPlanId;
            if (spId == null && p.GroupId != Guid.Empty && spByGroup.TryGetValue(p.GroupId, out var spPosting))
            {
                spId = spPosting.SavingsPlanId;
            }

            // determine security name using security service when security id present or group points to security
            Guid? secId = p.SecurityId;
            if (secId == null && p.GroupId != Guid.Empty && secNyGroup.TryGetValue(p.GroupId, out var grpPosting) && grpPosting.SecurityId != null)
            {
                secId = grpPosting.SecurityId;
            }

            return new BudgetReportPostingRawDataDto
            {
                PostingId = p.Id,
                BookingDate = p.BookingDate,
                ValutaDate = p.ValutaDate,
                Amount = p.Amount,
                PostingKind = p.Kind,
                Description = p.Description ?? p.Subject ?? string.Empty,
                AccountId = p.AccountId,
                AccountName = p.LinkedPostingAccountName ?? p.BankPostingAccountName,
                ContactId = p.ContactId,
                ContactName = contacts.FirstOrDefault(c => c.Id == p.ContactId)?.Name ?? p.RecipientName,
                SavingsPlanId = spId,
                SavingsPlanName = spId.HasValue ? savingsPlans.FirstOrDefault(sp => sp.Id == spId.Value)?.Name : null,
                SecurityId = secId,
                SecurityName = secId.HasValue ? securities.FirstOrDefault(sec => sec.Id == secId.Value)?.Name : null,

                BudgetCategoryId = null,
                BudgetCategoryName = null,
                BudgetPurposeId = null,
                BudgetPurposeName = null
            };
        }).ToList();

        // annotate postings with budget purpose/category information using provided overviews
        for (int i = 0; i < postingDtos.Count; i++)
        {
            var dto = postingDtos[i];
            var contact = contacts.FirstOrDefault(c => c.Id == dto.ContactId);

            var matched = purposes.FirstOrDefault(p =>
                (p.SourceType == BudgetSourceType.Contact && dto.ContactId == p.SourceId)
                || (p.SourceType == BudgetSourceType.SavingsPlan && dto.SavingsPlanId == p.SourceId)
                || (p.SourceType == BudgetSourceType.ContactGroup && contact != null && contact.CategoryId == p.SourceId)
            );

            if (matched != null)
            {
                string? catName = null;
                if (matched.BudgetCategoryId.HasValue)
                {
                    catName = categories.FirstOrDefault(c => c.Id == matched.BudgetCategoryId.Value)?.Name;
                }

                postingDtos[i] = dto with
                {
                    BudgetPurposeId = matched.Id,
                    BudgetPurposeName = matched.Name,
                    BudgetCategoryId = matched.BudgetCategoryId,
                    BudgetCategoryName = catName
                };
            }
        }

        return postingDtos;
    }

    private async Task<List<BudgetReportPurposeRawDataDto>> BuildUncategorizedPurposeDtosAsync(
        Guid ownerUserId,
        IEnumerable<BudgetPurposeOverviewDto> uncategorizedPurposes,
        List<BudgetReportPostingRawDataDto> postingDtos,
        List<BudgetReportPostingRawDataDto> unbudgetedList,
        IReadOnlyList<ContactDto> contacts,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var result = new List<BudgetReportPurposeRawDataDto>();

        foreach (var pur in uncategorizedPurposes)
        {
            // collect postings that belong to this uncategorized purpose and sort by booking, valuta, amount
            var postingsForPurpose = postingDtos.Where(d =>
                (pur.SourceType == BudgetSourceType.Contact && d.ContactId == pur.SourceId)
                || (pur.SourceType == BudgetSourceType.SavingsPlan && d.SavingsPlanId == pur.SourceId)
                || (pur.SourceType == BudgetSourceType.ContactGroup && contacts.FirstOrDefault(c => c.Id == d.ContactId)?.CategoryId == pur.SourceId)
            )
            .OrderBy(p => p.BookingDate)
            .ThenBy(p => p.ValutaDate ?? DateTime.MinValue)
            .ThenBy(p => p.Amount)
            .ToList();

            // collect current rules for this purpose
            var rules = (await _rules.ListByPurposeAsync(ownerUserId, pur.Id, ct)).ToList();

            var matchedPostings = new List<BudgetReportPostingRawDataDto>();

            // First: exact matches - ensure we remove them from global list
            foreach (var rule in rules.ToList())
            {
                var expected = rule.Amount;
                var candidate = postingsForPurpose.FirstOrDefault(p => Math.Sign(p.Amount) == Math.Sign(expected) && p.Amount == expected);
                if (candidate != null)
                {
                    matchedPostings.Add(candidate);
                    rules.Remove(rule);
                    postingsForPurpose.RemoveAll(x => x.PostingId == candidate.PostingId);
                    postingDtos.RemoveAll(x => x.PostingId == candidate.PostingId);
                }
            }

            // Now process remaining rules: positives (desc) and negatives (asc)
            var positiveRules = rules.Where(r => r.Amount > 0).OrderByDescending(r => r.Amount).ToList();
            var negativeRules = rules.Where(r => r.Amount < 0).OrderBy(r => r.Amount).ToList();

            // helper to allocate postings for a rule
            void AllocateForRule(BudgetRuleDto rule)
            {
                if (rule == null) return;
                var expected = rule.Amount;
                if (expected > 0)
                {
                    var remaining = expected;
                    for (int idx = 0; idx < postingsForPurpose.Count && remaining > 0; )
                    {
                        var p = postingsForPurpose[idx];
                        if (p.Amount <= 0) { idx++; continue; }
                        if (p.Amount <= remaining)
                        {
                            // fully consume posting
                            matchedPostings.Add(p);
                            remaining -= p.Amount;
                            // remove from working lists
                            postingDtos.RemoveAll(x => x.PostingId == p.PostingId);
                            postingsForPurpose.RemoveAt(idx);
                        }
                        else
                        {
                            // split posting: allocate part and reduce original
                            var allocated = p with { Amount = remaining };
                            matchedPostings.Add(allocated);
                            var remainingAmount = p.Amount - remaining;
                            // update in postingDtos and postingsForPurpose
                            for (int j = 0; j < postingDtos.Count; j++)
                            {
                                if (postingDtos[j].PostingId == p.PostingId)
                                {
                                    postingDtos[j] = postingDtos[j] with { Amount = remainingAmount };
                                    break;
                                }
                            }
                            postingsForPurpose[idx] = p with { Amount = remainingAmount };
                            remaining = 0;
                        }
                    }
                }
                else if (expected < 0)
                {
                    var remainingAbs = Math.Abs(expected);
                    for (int idx = 0; idx < postingsForPurpose.Count && remainingAbs > 0; )
                    {
                        var p = postingsForPurpose[idx];
                        if (p.Amount >= 0) { idx++; continue; }
                        var pAbs = Math.Abs(p.Amount);
                        if (pAbs <= remainingAbs)
                        {
                            // fully consume posting
                            matchedPostings.Add(p);
                            remainingAbs -= pAbs;
                            postingDtos.RemoveAll(x => x.PostingId == p.PostingId);
                            postingsForPurpose.RemoveAt(idx);
                        }
                        else
                        {
                            // split posting
                            var allocated = p with { Amount = -remainingAbs };
                            matchedPostings.Add(allocated);
                            var remainingAmount = p.Amount + remainingAbs; // p.Amount is negative
                            for (int j = 0; j < postingDtos.Count; j++)
                            {
                                if (postingDtos[j].PostingId == p.PostingId)
                                {
                                    postingDtos[j] = postingDtos[j] with { Amount = remainingAmount };
                                    break;
                                }
                            }
                            postingsForPurpose[idx] = p with { Amount = remainingAmount };
                            remainingAbs = 0;
                        }
                    }
                }
            }

            foreach (var r in positiveRules) AllocateForRule(r);
            foreach (var r in negativeRules) AllocateForRule(r);

            // any postings left in postingsForPurpose are unbudgeted for this purpose
            foreach (var left in postingsForPurpose)
            {
                unbudgetedList.Add(left);
                postingDtos.RemoveAll(x => x.PostingId == left.PostingId);
            }

            result.Add(new BudgetReportPurposeRawDataDto
            {
                PurposeId = pur.Id,
                PurposeName = pur.Name ?? string.Empty,
                BudgetedIncome = await GetBudgetedIncomeForPurposeAsync(ownerUserId, pur.Id, from, to, ct),
                BudgetedExpense = await GetBudgetedExpenseForPurposeAsync(ownerUserId, pur.Id, from, to, ct),
                BudgetedTarget = await GetBudgetedAmountForPurposeAsync(ownerUserId, pur.Id, from, to, ct),
                BudgetSourceType = pur.SourceType,
                SourceId = pur.SourceId,
                SourceName = pur.SourceName ?? string.Empty,
                Postings = matchedPostings.ToArray()
            });
        }

        return result;
    }

    /// <summary>
    /// Returns the raw data for a budget report for the given date range.
    /// </summary>
    /// <param name="ownerUserId">The owner user id.</param>
    /// <param name="from">Inclusive range start (month boundaries are not enforced).</param>
    /// <param name="to">Inclusive range end.</param>
    /// <param name="dateBasis">Date basis used by the underlying budget services.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A raw data DTO containing categories, purposes and contributing postings.</returns>
    /// <inheritdoc />
    public async Task<BudgetReportRawDataDto> GetRawDataAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        // Step 1: load overviews and related reference data
        var categoryOverviews = await _categories.ListOverviewAsync(ownerUserId, from, to, ct);
        var purposeOverviews = await _purposes.ListOverviewAsync(ownerUserId, 0, 5000, null, null, from, to, null, ct, dateBasis);
        var contacts = await _contacts.ListAsync(ownerUserId, 0, 5000, null, null, ct);

        // build posting DTOs for all contact postings and supplement savings plan info
        var postingDtos = await BuildPostingDtosAsync(ownerUserId, purposeOverviews, categoryOverviews, contacts, from, to, ct);

        // Filter postings according to the requested date basis: when using ValutaDate only
        // postings with a valuta date inside the requested range should be considered. When
        // using BookingDate, filter by booking date. This ensures reports generated using the
        // valuta basis exclude postings whose valuta lies outside the requested month (e.g.
        // dividends with booking in Jan but valuta in previous year).
        if (dateBasis == BudgetReportDateBasis.ValutaDate)
        {
            postingDtos = postingDtos
                .Where(p => p.ValutaDate.HasValue && DateOnly.FromDateTime(p.ValutaDate.Value) >= from && DateOnly.FromDateTime(p.ValutaDate.Value) <= to)
                .ToList();
        }
        else // BookingDate
        {
            postingDtos = postingDtos
                .Where(p => DateOnly.FromDateTime(p.BookingDate) >= from && DateOnly.FromDateTime(p.BookingDate) <= to)
                .ToList();
        }

        // Step 5: build result DTO
        // Collect postings that are not assigned to any budget purpose
        var unbudgetedList = postingDtos.Where(p => p.BudgetPurposeId == null || p.BudgetPurposeId == Guid.Empty).ToList();

        // Remove unbudgeted postings from the working posting list so they are
        // not considered further when constructing category/purpose postings.
        postingDtos = postingDtos.Where(p => p.BudgetPurposeId != null && p.BudgetPurposeId != Guid.Empty).ToList();

        // Categories and UncategorizedPurposes placeholders: purposes empty for now
        var categoryDtos = new List<BudgetReportCategoryRawDataDto>();
        foreach (var cat in categoryOverviews.OrderBy(c => c.Name))
        {
            var purposesInCat = purposeOverviews
                .Where(p => p.BudgetCategoryId == cat.Id)
                .OrderBy(p => p.Name)
                .ToList();

            // determine whether this category has its own rules; if not, the
            // rules are attached to the purposes -> allocate postings to purposes
            var categoryRules = await _rules.ListByCategoryAsync(ownerUserId, cat.Id, ct);

            BudgetReportPurposeRawDataDto[] rawPurposes;
            if (categoryRules == null || categoryRules.Count == 0)
            {
                // rules are on purposes: use the same allocation logic as for uncategorized purposes
                var list = await BuildUncategorizedPurposeDtosAsync(
                    ownerUserId,
                    purposesInCat,
                    postingDtos,
                    unbudgetedList,
                    contacts,
                    from,
                    to,
                    ct);

                rawPurposes = list.ToArray();
            }
            else
            {
                // rules are on the category: allocate category-level rules across postings
                var rulesForCategory = categoryRules.ToList();

                // collect postings that belong to this category (these postings already carry purpose metadata)
                var postingsForCategory = postingDtos.Where(d => d.BudgetCategoryId == cat.Id)
                    .OrderBy(p => p.BookingDate)
                    .ThenBy(p => p.ValutaDate ?? DateTime.MinValue)
                    .ThenBy(p => p.Amount)
                    .ToList();

                // map of purposeId -> allocated postings
                var allocated = new Dictionary<Guid, List<BudgetReportPostingRawDataDto>>();

                // exact matches first
                foreach (var rule in rulesForCategory.ToList())
                {
                    var expected = rule.Amount;
                    var candidate = postingsForCategory.FirstOrDefault(p => Math.Sign(p.Amount) == Math.Sign(expected) && p.Amount == expected);
                    if (candidate != null)
                    {
                        if (candidate.BudgetPurposeId.HasValue)
                        {
                            var key = candidate.BudgetPurposeId.Value;
                            if (!allocated.TryGetValue(key, out var postingsList))
                            {
                                postingsList = new List<BudgetReportPostingRawDataDto>();
                                allocated[key] = postingsList;
                            }
                            postingsList.Add(candidate);
                        }

                        rulesForCategory.Remove(rule);
                        postingsForCategory.RemoveAll(x => x.PostingId == candidate.PostingId);
                        postingDtos.RemoveAll(x => x.PostingId == candidate.PostingId);
                    }
                }

                var positiveRulesCat = rulesForCategory.Where(r => r.Amount > 0).OrderByDescending(r => r.Amount).ToList();
                var negativeRulesCat = rulesForCategory.Where(r => r.Amount < 0).OrderBy(r => r.Amount).ToList();

                void AllocateCategoryRule(BudgetRuleDto rule)
                {
                    if (rule == null) return;
                    var expected = rule.Amount;
                    if (expected > 0)
                    {
                        var remaining = expected;
                        for (int idx = 0; idx < postingsForCategory.Count && remaining > 0; )
                        {
                            var p = postingsForCategory[idx];
                            if (p.Amount <= 0) { idx++; continue; }
                            var targetPurposeId = p.BudgetPurposeId ?? Guid.Empty;
                            if (p.Amount <= remaining)
                            {
                                if (targetPurposeId != Guid.Empty)
                                {
                                if (!allocated.TryGetValue(targetPurposeId, out var postsForPurpose))
                                {
                                    postsForPurpose = new List<BudgetReportPostingRawDataDto>();
                                    allocated[targetPurposeId] = postsForPurpose;
                                }
                                postsForPurpose.Add(p);
                                }

                                remaining -= p.Amount;
                                postingDtos.RemoveAll(x => x.PostingId == p.PostingId);
                                postingsForCategory.RemoveAt(idx);
                            }
                            else
                            {
                                var allocatedPart = p with { Amount = remaining };
                                if (targetPurposeId != Guid.Empty)
                                {
                                if (!allocated.TryGetValue(targetPurposeId, out var postsForPurpose2))
                                {
                                    postsForPurpose2 = new List<BudgetReportPostingRawDataDto>();
                                    allocated[targetPurposeId] = postsForPurpose2;
                                }
                                postsForPurpose2.Add(allocatedPart);
                                }

                                var remainingAmount = p.Amount - remaining;
                                for (int j = 0; j < postingDtos.Count; j++)
                                {
                                    if (postingDtos[j].PostingId == p.PostingId)
                                    {
                                        postingDtos[j] = postingDtos[j] with { Amount = remainingAmount };
                                        break;
                                    }
                                }
                                postingsForCategory[idx] = p with { Amount = remainingAmount };
                                remaining = 0;
                            }
                        }
                    }
                    else if (expected < 0)
                    {
                        var remainingAbs = Math.Abs(expected);
                        for (int idx = 0; idx < postingsForCategory.Count && remainingAbs > 0; )
                        {
                            var p = postingsForCategory[idx];
                            if (p.Amount >= 0) { idx++; continue; }
                            var pAbs = Math.Abs(p.Amount);
                            var targetPurposeId = p.BudgetPurposeId ?? Guid.Empty;
                            if (pAbs <= remainingAbs)
                            {
                                if (targetPurposeId != Guid.Empty)
                                {
                                    if (!allocated.TryGetValue(targetPurposeId, out var list))
                                    {
                                        list = new List<BudgetReportPostingRawDataDto>();
                                        allocated[targetPurposeId] = list;
                                    }
                                    list.Add(p);
                                }

                                remainingAbs -= pAbs;
                                postingDtos.RemoveAll(x => x.PostingId == p.PostingId);
                                postingsForCategory.RemoveAt(idx);
                            }
                            else
                            {
                                var allocatedPart = p with { Amount = -remainingAbs };
                                if (targetPurposeId != Guid.Empty)
                                {
                                    if (!allocated.TryGetValue(targetPurposeId, out var list))
                                    {
                                        list = new List<BudgetReportPostingRawDataDto>();
                                        allocated[targetPurposeId] = list;
                                    }
                                    list.Add(allocatedPart);
                                }

                                var remainingAmount = p.Amount + remainingAbs; // negative
                                for (int j = 0; j < postingDtos.Count; j++)
                                {
                                    if (postingDtos[j].PostingId == p.PostingId)
                                    {
                                        postingDtos[j] = postingDtos[j] with { Amount = remainingAmount };
                                        break;
                                    }
                                }
                                postingsForCategory[idx] = p with { Amount = remainingAmount };
                                remainingAbs = 0;
                            }
                        }
                    }
                }

                foreach (var r in positiveRulesCat) AllocateCategoryRule(r);
                foreach (var r in negativeRulesCat) AllocateCategoryRule(r);

                // remaining postings become unbudgeted within this category
                foreach (var left in postingsForCategory)
                {
                    unbudgetedList.Add(left);
                    postingDtos.RemoveAll(x => x.PostingId == left.PostingId);
                }

                // build purpose dtos populated with allocated postings (if any)
                var purposeDtos = new List<BudgetReportPurposeRawDataDto>(purposesInCat.Count);
                foreach (var pur in purposesInCat)
                {
                    allocated.TryGetValue(pur.Id, out var posts);
                    purposeDtos.Add(new BudgetReportPurposeRawDataDto
                    {
                        PurposeId = pur.Id,
                        PurposeName = pur.Name ?? string.Empty,
                        // no per-purpose budget when rules live on the category
                        BudgetedIncome = 0m,
                        BudgetedExpense = 0m,
                        BudgetedTarget = 0m,
                        BudgetSourceType = pur.SourceType,
                        SourceId = pur.SourceId,
                        SourceName = pur.SourceName ?? string.Empty,
                        Postings = posts?.ToArray() ?? Array.Empty<BudgetReportPostingRawDataDto>()
                    });
                }

                rawPurposes = purposeDtos.ToArray();
            }

            categoryDtos.Add(new BudgetReportCategoryRawDataDto
            {
                CategoryId = cat.Id,
                CategoryName = cat.Name ?? string.Empty,
                BudgetedIncome = await GetBudgetedIncomeForCategoryAsync(ownerUserId, cat.Id, from, to, ct),
                BudgetedExpense = await GetBudgetedExpenseForCategoryAsync(ownerUserId, cat.Id, from, to, ct),
                BudgetedTarget = await GetBudgetedAmountForCategoryAsync(ownerUserId, cat.Id, from, to, ct),
                Purposes = rawPurposes
            });
        }

        var uncategorizedDtos = await BuildUncategorizedPurposeDtosAsync(
            ownerUserId,
            purposeOverviews.Where(p => !p.BudgetCategoryId.HasValue).OrderBy(p => p.Name),
            postingDtos,
            unbudgetedList,
            contacts,
            from,
            to,
            ct);

        return new BudgetReportRawDataDto
        {
            PeriodStart = from.ToDateTime(TimeOnly.MinValue),
            PeriodEnd = to.ToDateTime(TimeOnly.MaxValue),
            Categories = categoryDtos.ToArray(),
            UncategorizedPurposes = uncategorizedDtos.ToArray(),
            UnbudgetedPostings = unbudgetedList.ToArray()
        };
    }

    private async Task<BudgetReportPostingRawDataDto[]> BuildUnbudgetedPostingsAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

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
            dateBasis: dateBasis);

        var budgetedContactIds = purposes
            .Where(p => p.SourceType == BudgetSourceType.Contact)
            .Select(p => p.SourceId)
            .Distinct()
            .ToList();

        var budgetedSavingsPlanIds = purposes
            .Where(p => p.SourceType == BudgetSourceType.SavingsPlan)
            .Select(p => p.SourceId)
            .Distinct()
            .ToList();

        var contacts = await _contacts.ListAsync(ownerUserId, 0, 5000, null, null, ct);
        var savingsPlans = await _savingsPlans.ListAsync(ownerUserId, onlyActive: true, ct);

        var allPostings = new List<PostingServiceDto>();
        foreach (var contact in contacts)
        {
            var postings = await _postings.GetContactPostingsAsync(contact.Id, 0, 5000, null, fromDt, toDt, ownerUserId, ct);
            allPostings.AddRange(postings);
        }

        foreach (var plan in savingsPlans)
        {
            var postings = await _postings.GetSavingsPlanPostingsAsync(plan.Id, 0, 5000, null, fromDt, toDt, ownerUserId, ct);
            allPostings.AddRange(postings);
        }

        var budgetedPostingIds = new HashSet<Guid>();
        foreach (var contactId in budgetedContactIds)
        {
            var postings = await _postings.GetContactPostingsAsync(contactId, 0, 5000, null, fromDt, toDt, ownerUserId, ct);
            foreach (var posting in postings)
            {
                budgetedPostingIds.Add(posting.Id);
            }
        }

        foreach (var planId in budgetedSavingsPlanIds)
        {
            var postings = await _postings.GetSavingsPlanPostingsAsync(planId, 0, 5000, null, fromDt, toDt, ownerUserId, ct);
            foreach (var posting in postings)
            {
                budgetedPostingIds.Add(posting.Id);
            }
        }

        var unbudgetedPostings = allPostings
            .Where(p => !budgetedPostingIds.Contains(p.Id))
            .Where(p => p.Kind == PostingKind.Contact)
            .OrderBy(p => dateBasis == BudgetReportDateBasis.ValutaDate ? p.ValutaDate : p.BookingDate)
            .ThenBy(p => p.Id)
            .ToList();

        if (unbudgetedPostings.Count == 0)
        {
            return Array.Empty<BudgetReportPostingRawDataDto>();
        }

        return unbudgetedPostings.Select(p => new BudgetReportPostingRawDataDto
        {
            PostingId = p.Id,
            BookingDate = p.BookingDate,
            ValutaDate = p.ValutaDate,
            Amount = p.Amount,
            PostingKind = p.Kind,
            Description = p.Description ?? p.Subject ?? string.Empty,
            AccountId = p.AccountId,
            AccountName = p.LinkedPostingAccountName ?? p.BankPostingAccountName,
            ContactId = p.ContactId,
            ContactName = contacts.FirstOrDefault(c => c.Id == p.ContactId)?.Name ?? p.RecipientName,
            SavingsPlanId = p.SavingsPlanId,
            SavingsPlanName = null,
            SecurityId = p.SecurityId,
            SecurityName = null
        }).ToArray();
    }

    private async Task<BudgetReportCategoryRawDataDto[]> BuildCategoriesAsync(Guid ownerUserId, DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis, CancellationToken ct)
    {
        var categoryOverviews = await _categories.ListOverviewAsync(ownerUserId, from, to, ct);
        var purposeOverviews = await _purposes.ListOverviewAsync(ownerUserId, 0, 5000, null, null, from, to, null, ct, dateBasis);

        var categories = new List<BudgetReportCategoryRawDataDto>(categoryOverviews.Count);

        foreach (var cat in categoryOverviews.OrderBy(c => c.Name))
        {
            var purposes = purposeOverviews.Where(p => p.BudgetCategoryId == cat.Id).ToList();
            var rawPurposes = await BuildPurposesAsync(ownerUserId, purposes, from, to, dateBasis, ct);

            categories.Add(new BudgetReportCategoryRawDataDto
            {
                CategoryId = cat.Id,
                CategoryName = cat.Name ?? string.Empty,
                BudgetedAmount = await GetBudgetedAmountForCategoryAsync(ownerUserId, cat.Id, from, to, ct),
                Purposes = rawPurposes
            });
        }

        return categories.ToArray();
    }

    private async Task<BudgetReportPurposeRawDataDto[]> BuildUncategorizedPurposesAsync(Guid ownerUserId, DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis, CancellationToken ct)
    {
        var purposeOverviews = await _purposes.ListOverviewAsync(ownerUserId, 0, 5000, null, null, from, to, null, ct, dateBasis);
        var uncategorized = purposeOverviews.Where(p => !p.BudgetCategoryId.HasValue).ToList();
        return await BuildPurposesAsync(ownerUserId, uncategorized, from, to, dateBasis, ct);
    }

    private async Task<BudgetReportPurposeRawDataDto[]> BuildPurposesAsync(Guid ownerUserId, IReadOnlyList<BudgetPurposeOverviewDto> purposes, DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis, CancellationToken ct)
    {
        var result = new List<BudgetReportPurposeRawDataDto>(purposes.Count);

        foreach (var pur in purposes.OrderBy(p => p.Name))
        {
            var postings = await GetActualPostingsAsync(ownerUserId, pur.SourceType, pur.SourceId, from, to, dateBasis, ct);

            // compute positive and negative budget components separately so mixed purposes
            // (having both income and expense rules) are represented correctly
            var budgetedIncome = await GetBudgetedIncomeForPurposeAsync(ownerUserId, pur.Id, from, to, ct);
            var budgetedExpense = await GetBudgetedExpenseForPurposeAsync(ownerUserId, pur.Id, from, to, ct);
            var budgetedAmount = budgetedIncome + budgetedExpense;

            result.Add(new BudgetReportPurposeRawDataDto
            {
                PurposeId = pur.Id,
                PurposeName = pur.Name ?? string.Empty,
                BudgetedIncome = budgetedIncome,
                BudgetedExpense = budgetedExpense,
                BudgetedTarget = budgetedAmount,
                BudgetSourceType = pur.SourceType,
                SourceId = pur.SourceId,
                SourceName = pur.SourceName ?? string.Empty,
                Postings = postings
            });
        }

        return result.ToArray();
    }

    private async Task<decimal> GetBudgetedAmountForPurposeAsync(Guid ownerUserId, Guid purposeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rules = await _rules.ListByPurposeAsync(ownerUserId, purposeId, ct);
        return ComputeBudgetedOccurrences(rules, from, to).Sum();
    }

    private async Task<decimal> GetBudgetedAmountForCategoryAsync(Guid ownerUserId, Guid categoryId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rules = await _rules.ListByCategoryAsync(ownerUserId, categoryId, ct);
        return ComputeBudgetedOccurrences(rules, from, to).Sum();
    }

    private static decimal ComputeBudgetedAmountForPeriod(IReadOnlyList<BudgetRuleDto> rules, DateOnly from, DateOnly to)
    {
        // compute all occurrences for the rules in the period and sum them
        return ComputeBudgetedOccurrences(rules, from, to).Sum();
    }

    private static List<decimal> ComputeBudgetedOccurrences(IReadOnlyList<BudgetRuleDto> rules, DateOnly from, DateOnly to)
    {
        var occurrences = new List<decimal>();
        if (rules == null || rules.Count == 0) return occurrences;

        foreach (var rule in rules)
        {
            var step = rule.Interval switch
            {
                BudgetIntervalType.Monthly => 1,
                BudgetIntervalType.Quarterly => 3,
                BudgetIntervalType.Yearly => 12,
                BudgetIntervalType.CustomMonths => rule.CustomIntervalMonths ?? 1,
                _ => 1
            };

            var occ = rule.StartDate;
            var ruleEnd = rule.EndDate ?? to;

            while (occ < from)
            {
                occ = occ.AddMonths(step);
                if (occ > ruleEnd)
                {
                    break;
                }
            }

            while (occ <= to && occ <= ruleEnd)
            {
                occurrences.Add(rule.Amount);
                occ = occ.AddMonths(step);
            }
        }

        return occurrences;
    }

    private async Task<decimal> GetBudgetedIncomeForPurposeAsync(Guid ownerUserId, Guid purposeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rules = await _rules.ListByPurposeAsync(ownerUserId, purposeId, ct);
        return ComputeBudgetedOccurrences(rules, from, to).Where(x => x > 0).Sum();
    }

    private async Task<decimal> GetBudgetedExpenseForPurposeAsync(Guid ownerUserId, Guid purposeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rules = await _rules.ListByPurposeAsync(ownerUserId, purposeId, ct);
        return ComputeBudgetedOccurrences(rules, from, to).Where(x => x < 0).Sum();
    }

    private async Task<decimal> GetBudgetedIncomeForCategoryAsync(Guid ownerUserId, Guid categoryId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rules = await _rules.ListByCategoryAsync(ownerUserId, categoryId, ct);
        return ComputeBudgetedOccurrences(rules, from, to).Where(x => x > 0).Sum();
    }

    private async Task<decimal> GetBudgetedExpenseForCategoryAsync(Guid ownerUserId, Guid categoryId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rules = await _rules.ListByCategoryAsync(ownerUserId, categoryId, ct);
        return ComputeBudgetedOccurrences(rules, from, to).Where(x => x < 0).Sum();
    }

    private async Task<BudgetReportPostingRawDataDto[]> GetActualPostingsAsync(
        Guid ownerUserId,
        BudgetSourceType sourceType,
        Guid sourceId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        IReadOnlyList<PostingServiceDto> postings;
        if (sourceType == BudgetSourceType.Contact)
        {
            postings = await _postings.GetContactPostingsAsync(sourceId, 0, 250, null, fromDt, toDt, ownerUserId, ct);
        }
        else if (sourceType == BudgetSourceType.SavingsPlan)
        {
            // Prefer postings returned by the savings-plan query. If the posting service
            // returns plan-specific postings, use them and map to contact-posting DTOs.
            var spPostings = await _postings.GetSavingsPlanPostingsAsync(sourceId, 0, 5000, null, fromDt, toDt, ownerUserId, ct);
            if (spPostings != null && spPostings.Count > 0)
            {
                postings = spPostings
                    .Select(p => p with { Kind = PostingKind.Contact })
                    .ToList();
            }
            else
            {
                // fallback: scan contact postings and pick those that reference the savings plan
                var contacts = await _contacts.ListAsync(ownerUserId, 0, 5000, null, null, ct);
                var list = new List<PostingServiceDto>();
                foreach (var c in contacts)
                {
                    var cp = await _postings.GetContactPostingsAsync(c.Id, 0, 250, null, fromDt, toDt, ownerUserId, ct);
                    list.AddRange(cp.Where(p => p.SavingsPlanId == sourceId));
                }
                postings = list;
            }
        }
        else
        {
            postings = Array.Empty<PostingServiceDto>();
        }

        return postings
            .Select(p => new BudgetReportPostingRawDataDto
            {
                PostingId = p.Id,
                BookingDate = p.BookingDate,
                ValutaDate = p.ValutaDate,
                Amount = p.Amount,
                PostingKind = p.Kind,
                Description = p.Description ?? string.Empty,
                AccountId = p.AccountId,
                AccountName = p.LinkedPostingAccountName ?? p.BankPostingAccountName,
                ContactId = p.ContactId,
                ContactName = p.RecipientName,
                SavingsPlanId = p.SavingsPlanId,
                SavingsPlanName = null,
                SecurityId = p.SecurityId,
                SecurityName = null
            })
            .OrderBy(p => dateBasis == BudgetReportDateBasis.ValutaDate ? p.ValutaDate : p.BookingDate)
            .ThenBy(p => p.PostingId)
            .ToArray();
    }

    /// <summary>
    /// Asynchronously retrieves the monthly budget KPI data for the specified user and month.
    /// </summary>
    /// <param name="userId">The unique identifier of the user for whom to retrieve KPI data.</param>
    /// <param name="date">The month and year for which to retrieve KPI data. If null, the current month is used.</param>
    /// <param name="dateBasis">The date basis to use when determining which postings fall into the month for KPI calculations.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="MonthlyBudgetKpiDto"/>
    /// with the KPI data for the specified user and month.</returns>
    public async Task<MonthlyBudgetKpiDto> GetMonthlyKpiAsync(Guid userId, DateOnly? date, BudgetReportDateBasis dateBasis, CancellationToken ct)
    {
        var from = new DateOnly(date?.Year ?? DateTime.Now.Year, date?.Month ?? DateTime.Now.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var rawData = await GetRawDataAsync(userId, from, to, dateBasis, ct);
        // Include uncategorized purposes in KPI calculations as well.
        var uncategorized = rawData.UncategorizedPurposes ?? Array.Empty<BudgetReportPurposeRawDataDto>();

        // Planned income: sum all positive budget components (purpose-level and category-level).
        var plannedIncome = (rawData.Categories?.Sum(c => c.BudgetedIncome) ?? 0m)
            + (rawData.Categories?.SelectMany(c => c.Purposes).Sum(p => p.BudgetedIncome) ?? 0m)
            + (uncategorized?.Sum(p => p.BudgetedIncome) ?? 0m);
        // Planned expenses: sum absolute values of all negative budget components (purpose-level and category-level).
        var plannedExpenseAbs = (rawData.Categories?.Sum(c => Math.Abs(c.BudgetedExpense)) ?? 0m)
            + (rawData.Categories?.SelectMany(c => c.Purposes).Sum(p => Math.Abs(p.BudgetedExpense)) ?? 0m)
            + (uncategorized?.Sum(p => Math.Abs(p.BudgetedExpense)) ?? 0m);

        var unbudgetedIncome = rawData.UnbudgetedPostings
            .Where(p => p.Amount > 0)
            .Sum(p => p.Amount);
        var unbudgetedExpenseAbs = Math.Abs(rawData.UnbudgetedPostings
            .Where(p => p.Amount < 0)
            .Sum(p => p.Amount));

        var budgetedRealizedIncome= rawData.Categories
                .SelectMany(c => c.Purposes)
                .SelectMany(p => p.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                .Where(p => p.Amount > 0)
                .Sum(p => p.Amount)
            + rawData.UncategorizedPurposes?
                .SelectMany(p => p.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                .Where(p => p.Amount > 0)
                .Sum(p => p.Amount) ?? 0m;
        var budgetedRealizedExpenseAbs = Math.Abs(rawData.Categories
                .SelectMany(c => c.Purposes)
                .SelectMany(p => p.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                .Where(p => p.Amount < 0)
                .Sum(p => p.Amount))
            + Math.Abs(rawData.UncategorizedPurposes?
                .SelectMany(p => p.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                .Where(p => p.Amount < 0)
                .Sum(p => p.Amount) ?? 0m);

        var actualIncome = budgetedRealizedIncome + unbudgetedIncome;
        var actualExpenseAbs = budgetedRealizedExpenseAbs + unbudgetedExpenseAbs;

        return new MonthlyBudgetKpiDto
        {
            PlannedIncome = plannedIncome,
            PlannedExpenseAbs = plannedExpenseAbs,
            PlannedResult = plannedIncome - plannedExpenseAbs,

            UnbudgetedIncome = unbudgetedIncome,
            UnbudgetedExpenseAbs = unbudgetedExpenseAbs,
            BudgetedRealizedIncome = budgetedRealizedIncome,
            BudgetedRealizedExpenseAbs = budgetedRealizedExpenseAbs,

            ActualIncome = actualIncome,
            ActualExpenseAbs = actualExpenseAbs,
            ActualResult = actualIncome - actualExpenseAbs,
            // ExpectedIncome = aktuelle Einnahmen + noch nicht erfüllte, budgetierte Einnahmen
            ExpectedIncome = actualIncome + Math.Max(0, plannedIncome - budgetedRealizedIncome),
            // ExpectedExpenseAbs = aktuelle Ausgaben + noch nicht erfüllte, budgetierte Ausgaben
            ExpectedExpenseAbs = actualExpenseAbs + Math.Max(0, plannedExpenseAbs - budgetedRealizedExpenseAbs),
            // Remaining planned expenses = planned expenses minus budgeted realized expenses
            RemainingPlannedExpenseAbs = Math.Max(0, plannedExpenseAbs - budgetedRealizedExpenseAbs),
            // Remaining planned income = planned income minus budgeted realized income
            RemainingPlannedIncome = Math.Max(0, plannedIncome - budgetedRealizedIncome),
            // ExpectedTargetResult = ExpectedIncome - ExpectedExpenseAbs
            ExpectedTargetResult = (actualIncome + Math.Max(0, plannedIncome - budgetedRealizedIncome))
                - (actualExpenseAbs + Math.Max(0, plannedExpenseAbs - budgetedRealizedExpenseAbs))
        };
    }
}
