using FinanceManager.Application.Budget;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Budget;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_budget_overview</c> (T05): budget situation — purposes,
/// rules (incl. purpose patterns), overrides and utilization per budget category.
/// Scopes: (Budgets, Lesen), (Budgetregeln, Lesen), (Budgetkategorien, Lesen).
/// </summary>
public sealed class FmBudgetOverviewDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_budget_overview";

    /// <summary>Creates the descriptor.</summary>
    public FmBudgetOverviewDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"budgetPurposeId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single purpose; its rules and overrides are included.\"},\"budgetCategoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional budget category filter; also selects rules by category.\"},\"from\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Period start (UTC).\"},\"to\":{\"type\":\"string\",\"format\":\"date\",\"description\":\"Period end (UTC).\"},\"q\":{\"type\":\"string\",\"description\":\"Optional purpose name search text.\"},\"includeRules\":{\"type\":\"boolean\",\"description\":\"Include rules per purpose (default true).\"},\"includeCategories\":{\"type\":\"boolean\",\"description\":\"Include the budget category list (default true).\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Budget overview: purposes with utilization, rules incl. purpose patterns, overrides, budget categories and the inherited category-level budget (parentBudget) so purposes without own rules are not mistaken for unbudgeted. Scopes: (Budgets, Lesen), (Budgetregeln, Lesen), (Budgetkategorien, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Budgets, McpRightKind.Read),
        new McpToolScope(McpDataType.BudgetRules, McpRightKind.Read),
        new McpToolScope(McpDataType.BudgetCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_budget_overview</c>: adapts <see cref="IBudgetPurposeService"/>,
/// <see cref="IBudgetRuleService"/>, <see cref="IBudgetOverrideService"/> and
/// <see cref="IBudgetCategoryService"/>. Names, descriptions and purpose patterns are
/// untrusted content (<c>untrusted_data</c>).
/// </summary>
public sealed class FmBudgetOverviewHandler : IMcpToolHandler
{
    private readonly IBudgetPurposeService _purposes;
    private readonly IBudgetRuleService _rules;
    private readonly IBudgetOverrideService _overrides;
    private readonly IBudgetCategoryService _categories;

    /// <summary>Creates the handler.</summary>
    public FmBudgetOverviewHandler(
        IBudgetPurposeService purposes,
        IBudgetRuleService rules,
        IBudgetOverrideService overrides,
        IBudgetCategoryService categories)
    {
        _purposes = purposes;
        _rules = rules;
        _overrides = overrides;
        _categories = categories;
    }

    /// <inheritdoc />
    public string ToolName => FmBudgetOverviewDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var budgetPurposeId = McpToolArguments.OptionalGuid(arguments, "budgetPurposeId");
        var budgetCategoryId = McpToolArguments.OptionalGuid(arguments, "budgetCategoryId");
        var from = McpToolArguments.OptionalDateTime(arguments, "from");
        var to = McpToolArguments.OptionalDateTime(arguments, "to");
        var q = McpToolArguments.OptionalString(arguments, "q");
        var includeRules = McpToolArguments.OptionalBool(arguments, "includeRules") ?? true;
        var includeCategories = McpToolArguments.OptionalBool(arguments, "includeCategories") ?? true;
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);

        IReadOnlyList<BudgetPurposeOverviewDto> purposes;
        if (budgetPurposeId.HasValue)
        {
            // Real Kennzahlen instead of fabricated zeros: ListOverviewAsync has no id
            // filter, so the lookup pre-selects by the master-data name (LIKE) and the
            // result is then filtered down to the exact record.
            var single = await _purposes.GetAsync(budgetPurposeId.Value, owner, ct);
            purposes = single is null
                ? Array.Empty<BudgetPurposeOverviewDto>()
                : (await _purposes.ListOverviewAsync(
                        owner, 0, int.MaxValue, null, single.Name,
                        from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
                        to.HasValue ? DateOnly.FromDateTime(to.Value) : null,
                        null, ct))
                    .Where(p => p.Id == single.Id)
                    .ToList();
        }
        else
        {
            purposes = await _purposes.ListOverviewAsync(
                owner, skip, take, null, q,
                from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
                to.HasValue ? DateOnly.FromDateTime(to.Value) : null,
                budgetCategoryId, ct);
        }

        var purposeOut = new List<object>(purposes.Count);
        Dictionary<Guid, object>? parentBudgets = null;
        if (includeRules)
        {
            // Category-level budgets apply to every purpose bound to the category. Surface them
            // per purpose so readers can distinguish "no own rule" from "no budget at all".
            var catIds = purposes
                .Select(p => p.BudgetCategoryId)
                .Where(id => id.HasValue && id.Value != Guid.Empty)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            if (catIds.Count > 0)
            {
                parentBudgets = new Dictionary<Guid, object>(catIds.Count);
                foreach (var catId in catIds)
                {
                    var catRules = await _rules.ListByCategoryAsync(owner, catId, ct);
                    parentBudgets[catId] = new
                    {
                        budgetCategoryId = catId,
                        rules = catRules.Select(r => new
                        {
                            id = r.Id,
                            budgetPurposeId = r.BudgetPurposeId,
                            budgetCategoryId = r.BudgetCategoryId,
                            amount = r.Amount,
                            interval = r.Interval.ToString(),
                            customIntervalMonths = r.CustomIntervalMonths,
                            startDate = r.StartDate,
                            endDate = r.EndDate,
                            useRegex = r.UseRegex,
                            untrusted_data = new { purposePattern = r.PurposePattern }
                        }).ToList()
                    };
                }
            }
        }
        foreach (var p in purposes)
        {
            object? rules = null;
            object? overridesOut = null;
            if (includeRules)
            {
                var ruleList = await _rules.ListByPurposeAsync(owner, p.Id, ct);
                rules = ruleList.Select(r => new
                {
                    id = r.Id,
                    budgetPurposeId = r.BudgetPurposeId,
                    budgetCategoryId = r.BudgetCategoryId,
                    amount = r.Amount,
                    interval = r.Interval.ToString(),
                    customIntervalMonths = r.CustomIntervalMonths,
                    startDate = r.StartDate,
                    endDate = r.EndDate,
                    useRegex = r.UseRegex,
                    untrusted_data = new { purposePattern = r.PurposePattern }
                }).ToList();
                var overrideList = await _overrides.ListByPurposeAsync(owner, p.Id, ct);
                overridesOut = overrideList.Select(o => new
                {
                    id = o.Id,
                    budgetPurposeId = o.BudgetPurposeId,
                    periodYear = o.PeriodYear,
                    periodMonth = o.PeriodMonth,
                    amount = o.Amount
                }).ToList();
            }
            purposeOut.Add(new
            {
                id = p.Id,
                sourceType = p.SourceType.ToString(),
                sourceId = p.SourceId,
                budgetCategoryId = p.BudgetCategoryId,
                ruleCount = p.RuleCount,
                budgetSum = p.BudgetSum,
                actualSum = p.ActualSum,
                variance = p.Variance,
                sourceSymbolAttachmentId = p.SourceSymbolAttachmentId,
                valuationType = p.ValuationType.ToString(),
                rules,
                overrides = overridesOut,
                parentBudget = includeRules && p.BudgetCategoryId.HasValue && parentBudgets is not null
                    && parentBudgets.TryGetValue(p.BudgetCategoryId.Value, out var pb)
                        ? pb
                        : null,
                untrusted_data = new
                {
                    name = p.Name,
                    description = p.Description,
                    sourceName = p.SourceName,
                    budgetCategoryName = p.BudgetCategoryName
                }
            });
        }

        object? categoryRules = null;
        if (budgetCategoryId.HasValue)
        {
            var ruleList = await _rules.ListByCategoryAsync(owner, budgetCategoryId.Value, ct);
            categoryRules = ruleList.Select(r => new
            {
                id = r.Id,
                budgetPurposeId = r.BudgetPurposeId,
                budgetCategoryId = r.BudgetCategoryId,
                amount = r.Amount,
                interval = r.Interval.ToString(),
                customIntervalMonths = r.CustomIntervalMonths,
                startDate = r.StartDate,
                endDate = r.EndDate,
                useRegex = r.UseRegex,
                untrusted_data = new { purposePattern = r.PurposePattern }
            }).ToList();
        }

        object? categories = null;
        if (includeCategories)
        {
            categories = (await _categories.ListAsync(owner, ct))
                .Select(c => new
                {
                    id = c.Id,
                    untrusted_data = new { name = c.Name }
                }).ToList();
        }

        return new { purposes = purposeOut, categoryRules, categories };
    }
}
