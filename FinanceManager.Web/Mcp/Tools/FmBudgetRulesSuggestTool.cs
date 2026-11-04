using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Postings;
using System.Text.Json;
using System.Text.RegularExpressions;
using IPostingsQueryService = FinanceManager.Application.Postings.IPostingsQueryService;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the suggestion tool <c>fm_budget_rules_suggest</c> (T12): budget-rule
/// suggestions — <c>PurposePattern</c>/<c>PurposePatternIsRegex</c> derived from posting
/// text history (same semantics as the automatic budget matching), amount/interval
/// proposals, purpose/category assignments and review findings (missing rules, expired
/// rules, exceeded budgets). Suggestion output only — never persists.
/// Scope: (Budgetregeln, Vorschlag).
/// </summary>
public sealed class FmBudgetRulesSuggestDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_budget_rules_suggest";

    /// <summary>Creates the descriptor.</summary>
    public FmBudgetRulesSuggestDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"budgetPurposeId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single purpose restriction.\"},\"budgetCategoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional budget category filter.\"},\"from\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Analysis period start (UTC); default: last 12 months.\"},\"to\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Analysis period end (UTC).\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200},\"maxSuggestions\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Budget rule suggestions without persistence: purpose patterns from posting history, amount/interval proposals, missing/expired rules, exceeded budgets with effect descriptions. Scope: (Budgetregeln, Vorschlag).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Vorschlagend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.BudgetRules, McpRightKind.Suggest)
    };
}

/// <summary>
/// Handler of <c>fm_budget_rules_suggest</c>: reads purposes/rules/overrides through the
/// budget service contracts and posting history through <see cref="IPostingsQueryService"/>.
/// The declared scope stays (Budgetregeln, Vorschlag) as catalogued; the data sources the
/// suggestions build on are gated at runtime: without (Budgets, Lesen) no purposes are
/// visible, without (Buchungen, Lesen) no history-derived proposals are produced — the
/// result then reports the degraded coverage instead of leaking records. Record-level
/// filtering applies the declared (Budgetregeln, Vorschlag) rows to the primary data
/// (purposes/rules/overrides) and the (Buchungen, Lesen) rows to the posting history —
/// classification values derive through the purpose's actuals source (contact/contact-
/// group category, savings-plan category). Purpose names, patterns and derived rule
/// texts are untrusted content (<c>untrusted_data</c>); no write method is invoked.
/// </summary>
public sealed class FmBudgetRulesSuggestHandler : IMcpToolHandler
{
    /// <summary>Default history window when no period is given (months).</summary>
    private const int DefaultHistoryMonths = 12;

    /// <summary>Divergence ratio above which an amount proposal is emitted.</summary>
    private const decimal AmountDivergenceRatio = 0.25m;

    private readonly IBudgetPurposeService _purposes;
    private readonly IBudgetRuleService _rules;
    private readonly IBudgetOverrideService _overrides;
    private readonly IBudgetCategoryService _categories;
    private readonly IPostingsQueryService _postings;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmBudgetRulesSuggestHandler(
        IBudgetPurposeService purposes,
        IBudgetRuleService rules,
        IBudgetOverrideService overrides,
        IBudgetCategoryService categories,
        IPostingsQueryService postings,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        IMcpScopeEvaluator scopeEvaluator,
        McpRecordScopeFilter recordFilter)
    {
        _purposes = purposes;
        _rules = rules;
        _overrides = overrides;
        _categories = categories;
        _postings = postings;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _scopeEvaluator = scopeEvaluator;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmBudgetRulesSuggestDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var budgetPurposeId = McpToolArguments.OptionalGuid(arguments, "budgetPurposeId");
        var budgetCategoryId = McpToolArguments.OptionalGuid(arguments, "budgetCategoryId");
        var to = McpToolArguments.OptionalDateTime(arguments, "to") ?? DateTime.UtcNow;
        var from = McpToolArguments.OptionalDateTime(arguments, "from") ?? to.AddMonths(-DefaultHistoryMonths);
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);
        var maxSuggestions = Math.Clamp(
            McpToolArguments.OptionalInt(arguments, "maxSuggestions") ?? 50, 1, McpToolArguments.MaxTake);

        // Runtime coverage gates — the declared scope is (Budgetregeln, Vorschlag); the
        // underlying data needs its own read scopes, otherwise the tool degrades instead
        // of leaking records the access may not see (toolkatalog §3.2 Nebenbedingung).
        var access = context.Access.Access;
        var budgetsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Budgets, McpRightKind.Read, ct);
        var postingsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Postings, McpRightKind.Read, ct);
        var categoriesAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.BudgetCategories, McpRightKind.Read, ct);

        var degraded = new List<string>();
        if (!budgetsAllowed) { degraded.Add("budgets"); }
        if (!postingsAllowed) { degraded.Add("postings"); }
        if (!categoriesAllowed) { degraded.Add("budgetCategories"); }

        if (!budgetsAllowed)
        {
            return new { suggestions = Array.Empty<object>(), degraded };
        }

        // Record-level filters: the suggestion output binds to the declared
        // (Budgetregeln, Vorschlag) rows of the tool; the posting history inherits the
        // record filters of (Buchungen, Lesen) — like T15 on the same source. Both are
        // only costly when an individual profile actually carries filter CSVs.
        var ruleScopes = await _recordFilter.GetScopesAsync(
            context.Access, McpDataType.BudgetRules, McpRightKind.Suggest, ct);
        var postingScopes = await _recordFilter.GetReadScopesAsync(context.Access, McpDataType.Postings, ct);
        var purposesFiltered = McpRecordScopeFilter.RequiresRecordFilter(ruleScopes);
        var postingsFiltered = McpRecordScopeFilter.RequiresRecordFilter(postingScopes);

        IReadOnlyList<BudgetPurposeOverviewDto> purposes;
        if (budgetPurposeId.HasValue)
        {
            var single = await _purposes.GetAsync(budgetPurposeId.Value, owner, ct);
            purposes = single is null
                ? Array.Empty<BudgetPurposeOverviewDto>()
                : new[]
                {
                    new BudgetPurposeOverviewDto(
                        single.Id, single.OwnerUserId, single.Name, single.Description,
                        single.SourceType, single.SourceId, 0, 0m, 0m, 0m,
                        null, null, single.BudgetCategoryId, null, single.ValuationType)
                };
        }
        else
        {
            purposes = await _purposes.ListOverviewAsync(
                owner, skip, take, null, null,
                DateOnly.FromDateTime(from), DateOnly.FromDateTime(to),
                budgetCategoryId, ct);
        }

        var categoryNames = new Dictionary<Guid, string>();
        if (categoriesAllowed)
        {
            foreach (var cat in await _categories.ListAsync(owner, ct))
            {
                categoryNames[cat.Id] = cat.Name;
            }
        }

        var suggestions = new List<object>();
        foreach (var p in purposes)
        {
            if (suggestions.Count >= maxSuggestions) { break; }

            // Filter through the actuals source (Kontakt-/Kontaktgruppen- bzw.
            // Sparplankategorie) — an invisible purpose yields no suggestions at all.
            McpRecordContext? sourceContext = null;
            if (purposesFiltered || postingsFiltered)
            {
                sourceContext = await SourceRecordContextAsync(p, owner, ct);
                if (purposesFiltered && !McpRecordScopeFilter.IsAllowed(ruleScopes, sourceContext))
                {
                    continue;
                }
            }

            var rules = await _rules.ListByPurposeAsync(owner, p.Id, ct);
            var overrides = await _overrides.ListByPurposeAsync(owner, p.Id, ct);
            var postings = postingsAllowed
                ? await LoadSourcePostingsAsync(p, from, to, owner, ct)
                : Array.Empty<PostingServiceDto>();
            if (postingsFiltered && postings.Count > 0)
            {
                // Same source anchor for the whole batch — contact postings classify
                // through the contact category, plan postings through the plan category.
                postings = postings
                    .Where(post => McpRecordScopeFilter.IsAllowed(postingScopes, sourceContext!))
                    .ToList();
            }

            // --- Review finding: budget exceeded in the analyzed period ---
            if (p.BudgetSum > 0m && p.Variance > 0m)
            {
                suggestions.Add(new
                {
                    kind = "budget_exceeded",
                    targetBudgetPurposeId = p.Id,
                    field = (string?)null,
                    proposedValue = (object?)null,
                    confidence = "high",
                    reason = $"Actual sum {p.ActualSum:0.##} exceeds the budget sum {p.BudgetSum:0.##} by {p.Variance:0.##} in the analyzed period.",
                    impact = "Review finding only — adjusting rules or overrides is a separate privileged action.",
                    untrusted_data = new { purposeName = p.Name }
                });
            }

            // --- Missing rules on a purpose that produced actuals ---
            if (rules.Count == 0)
            {
                decimal? proposedAmount = null;
                string? proposedInterval = null;
                if (postings.Count > 0)
                {
                    proposedAmount = MedianAmount(postings);
                    proposedInterval = DominantInterval(postings).ToString();
                }
                suggestions.Add(new
                {
                    kind = "missing_rule",
                    targetBudgetPurposeId = p.Id,
                    field = "Rules",
                    proposedValue = proposedAmount.HasValue
                        ? (object)new { amount = proposedAmount.Value, interval = proposedInterval }
                        : null,
                    confidence = proposedAmount.HasValue ? "medium" : "low",
                    reason = p.ActualSum != 0m
                        ? $"Purpose has no budget rules but {p.ActualSum:0.##} actual sum in the period."
                        : "Purpose has no budget rules; no amount derivable without posting history.",
                    impact = proposedAmount.HasValue
                        ? $"Would add an expected budget of ~{proposedAmount.Value:0.##} per {proposedInterval} to this purpose."
                        : "Would make the purpose plannable; amount not derivable.",
                    untrusted_data = new { purposeName = p.Name }
                });
            }
            else
            {
                // --- Expired rules while the purpose still produces actuals ---
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                foreach (var r in rules.Where(r => r.EndDate.HasValue && r.EndDate.Value < today))
                {
                    if (suggestions.Count >= maxSuggestions) { break; }
                    if (p.ActualSum == 0m) { continue; }
                    suggestions.Add(new
                    {
                        kind = "rule_expired",
                        targetBudgetPurposeId = p.Id,
                        targetBudgetRuleId = r.Id,
                        field = "EndDate",
                        proposedValue = (object)new { endDate = (DateOnly?)null, note = "Extend or renew the rule period." },
                        confidence = "medium",
                        reason = $"Rule ended {r.EndDate:yyyy-MM-dd} but the purpose still shows actuals ({p.ActualSum:0.##}).",
                        impact = $"Would resume expected-budget coverage of ~{r.Amount:0.##} per {r.Interval}.",
                        untrusted_data = new { purposeName = p.Name, purposePattern = r.PurposePattern }
                    });
                }

                // --- Pattern proposals for rules without a purpose pattern ---
                if (postings.Count > 0 && rules.Any(r => string.IsNullOrWhiteSpace(r.PurposePattern)))
                {
                    var candidate = DerivePattern(postings);
                    if (candidate is not null)
                    {
                        var coverage = postings.Count(post =>
                            BudgetRulePatternMatcher.MatchesPosting(post.Subject, post.Description, candidate, useRegex: false));
                        var confidence = coverage >= Math.Max(1, (int)Math.Ceiling(postings.Count * 0.8m)) ? "high"
                            : coverage >= Math.Max(1, (int)Math.Ceiling(postings.Count * 0.5m)) ? "medium" : "low";
                        foreach (var r in rules.Where(r => string.IsNullOrWhiteSpace(r.PurposePattern)))
                        {
                            if (suggestions.Count >= maxSuggestions) { break; }
                            suggestions.Add(new
                            {
                                kind = "purpose_pattern",
                                targetBudgetPurposeId = p.Id,
                                targetBudgetRuleId = r.Id,
                                field = "PurposePattern",
                                proposedValue = (object)new { purposePattern = candidate, useRegex = false },
                                confidence,
                                reason = $"Candidate token matches {coverage} of {postings.Count} analyzed postings of the purpose source.",
                                impact = $"Would restrict the rule's matching to postings containing the pattern ({coverage}/{postings.Count} coverage).",
                                untrusted_data = new { purposeName = p.Name, purposePattern = candidate }
                            });
                        }
                    }
                }

                // --- Amount proposal when rules diverge from actuals ---
                if (postings.Count >= 3)
                {
                    var median = MedianAmount(postings);
                    foreach (var r in rules)
                    {
                        if (suggestions.Count >= maxSuggestions) { break; }
                        if (r.Amount == 0m || median == 0m) { continue; }
                        var divergence = Math.Abs(Math.Abs(median) - Math.Abs(r.Amount)) / Math.Abs(r.Amount);
                        if (divergence > AmountDivergenceRatio)
                        {
                            suggestions.Add(new
                            {
                                kind = "rule_amount",
                                targetBudgetPurposeId = p.Id,
                                targetBudgetRuleId = r.Id,
                                field = "Amount",
                                proposedValue = (object)new { amount = median },
                                confidence = "low",
                                reason = $"Rule amount {r.Amount:0.##} diverges from the median actual amount {median:0.##} by {divergence:P0}.",
                                impact = $"Would change the expected budget of this rule to ~{median:0.##} per {r.Interval}.",
                                untrusted_data = new { purposeName = p.Name, purposePattern = r.PurposePattern }
                            });
                        }
                    }
                }
            }

            // --- Missing purpose category (Zuordnung, toolkatalog T12) ---
            if (p.BudgetCategoryId is null && categoriesAllowed && suggestions.Count < maxSuggestions)
            {
                var dominant = purposes
                    .Where(x => x.SourceType == p.SourceType && x.BudgetCategoryId.HasValue)
                    .GroupBy(x => x.BudgetCategoryId!.Value)
                    .OrderByDescending(g => g.Count())
                    .Select(g => (Guid?)g.Key)
                    .FirstOrDefault();
                categoryNames.TryGetValue(dominant ?? Guid.Empty, out var dominantName);
                suggestions.Add(new
                {
                    kind = "missing_purpose_category",
                    targetBudgetPurposeId = p.Id,
                    field = "BudgetCategoryId",
                    proposedValue = (object?)new { budgetCategoryId = dominant },
                    confidence = dominant.HasValue ? "low" : "low",
                    reason = dominant.HasValue
                        ? "Purpose has no budget category; the proposal is the most-used category of the same source type."
                        : "Purpose has no budget category; no dominant category of the same source type is derivable.",
                    impact = "Would group the purpose under the proposed budget category in overviews and reports.",
                    untrusted_data = new { purposeName = p.Name, budgetCategoryName = dominantName }
                });
            }
        }

        return new
        {
            suggestions = suggestions.Take(maxSuggestions).ToList(),
            degraded,
            period = new { from, to }
        };
    }

    /// <summary>
    /// Resolves the record classification of a budget purpose through its actuals source
    /// (derivation table: filters act through the linked aggregate — contact purposes over
    /// the contact category, contact-group purposes over the group itself, savings-plan
    /// purposes over the plan category). The same context classifies the purpose's posting
    /// history, which is anchored on that aggregate (<c>GetContactPostingsAsync</c> →
    /// contact postings, <c>GetSavingsPlanPostingsAsync</c> → plan postings).
    /// </summary>
    private async Task<McpRecordContext> SourceRecordContextAsync(
        BudgetPurposeOverviewDto purpose, Guid owner, CancellationToken ct)
        => purpose.SourceType switch
        {
            BudgetSourceType.Contact => new McpRecordContext
            {
                ContactCategoryId = (await _contacts.GetAsync(purpose.SourceId, owner, ct))?.CategoryId
            },
            // The contact group is the contact category itself — it classifies by its id.
            BudgetSourceType.ContactGroup => new McpRecordContext { ContactCategoryId = purpose.SourceId },
            BudgetSourceType.SavingsPlan => new McpRecordContext
            {
                SavingsPlanCategoryId = (await _savingsPlans.GetAsync(purpose.SourceId, owner, ct))?.CategoryId
            },
            _ => new McpRecordContext()
        };

    private async Task<IReadOnlyList<PostingServiceDto>> LoadSourcePostingsAsync(
        BudgetPurposeOverviewDto purpose, DateTime from, DateTime to, Guid owner, CancellationToken ct)
        => purpose.SourceType switch
        {
            BudgetSourceType.Contact => await _postings.GetContactPostingsAsync(
                purpose.SourceId, 0, McpToolArguments.MaxTake, null, from, to, owner, ct),
            BudgetSourceType.SavingsPlan => await _postings.GetSavingsPlanPostingsAsync(
                purpose.SourceId, 0, McpToolArguments.MaxTake, null, from, to, owner, ct),
            // ContactGroup purposes have no posting query contract — no history basis.
            _ => Array.Empty<PostingServiceDto>()
        };

    /// <summary>
    /// Derives a candidate purpose pattern: the most frequent alphanumeric token
    /// (length ≥ 5, umlaut-normalized) across the source postings' subjects/descriptions.
    /// </summary>
    private static string? DerivePattern(IReadOnlyList<PostingServiceDto> postings)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in postings)
        {
            var text = McpSuggestionMatching.NormalizeUmlauts($"{p.Subject} {p.Description}");
            foreach (var token in Regex.Matches(text, "[A-Za-z0-9]{5,}").Select(m => m.Value))
            {
                counts[token] = counts.GetValueOrDefault(token) + 1;
            }
        }
        return counts.OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();
    }

    /// <summary>Median of the absolute posting amounts.</summary>
    private static decimal MedianAmount(IReadOnlyList<PostingServiceDto> postings)
    {
        var amounts = postings.Select(p => Math.Abs(p.Amount)).Where(a => a > 0m).OrderBy(a => a).ToList();
        if (amounts.Count == 0) { return 0m; }
        var mid = amounts.Count / 2;
        return amounts.Count % 2 == 1
            ? amounts[mid]
            : (amounts[mid - 1] + amounts[mid]) / 2m;
    }

    /// <summary>Dominant recurrence interval derived from the posting date spacing.</summary>
    private static BudgetIntervalType DominantInterval(IReadOnlyList<PostingServiceDto> postings)
    {
        var days = postings.Select(p => p.BookingDate.Date).Distinct().OrderBy(d => d).ToList();
        if (days.Count < 2) { return BudgetIntervalType.Monthly; }
        var gaps = days.Zip(days.Skip(1), (a, b) => (b - a).Days).OrderBy(g => g).ToList();
        var medianGap = gaps[gaps.Count / 2];
        return medianGap <= 45 ? BudgetIntervalType.Monthly
            : medianGap <= 120 ? BudgetIntervalType.Quarterly
            : BudgetIntervalType.Yearly;
    }
}
