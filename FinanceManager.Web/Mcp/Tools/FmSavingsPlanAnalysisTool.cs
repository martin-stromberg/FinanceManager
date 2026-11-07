using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.SavingsPlans;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_savings_plan_analysis</c> (T06): savings plan stock and
/// progress analysis (target amount/date, interval, active/archived) incl. categories.
/// Scopes: (Sparpläne, Lesen), (Sparplankategorien, Lesen).
/// </summary>
public sealed class FmSavingsPlanAnalysisDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_savings_plan_analysis";

    /// <summary>Creates the descriptor.</summary>
    public FmSavingsPlanAnalysisDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"savingsPlanId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single plan; its analysis is always included.\"},\"onlyActive\":{\"type\":\"boolean\",\"description\":\"True = only active plans, false = only archived; omitted = both list calls merged.\"},\"includeAnalysis\":{\"type\":\"boolean\",\"description\":\"Include AnalyzeAsync result per listed plan (default: only single lookups).\"},\"includeCategories\":{\"type\":\"boolean\",\"description\":\"Include the savings plan category list.\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Savings plan stock and progress analysis (target amount/date, interval, active/archived) incl. categories. " +
           "Savings plans serve distinct purposes: 'Recurring' plans smooth regular expenses (insurances, vehicle costs, memberships) into monthly contributions; " +
           "'OneTime' plans are reserves for expected one-time purchases (e.g. replacing an appliance when it breaks) — a past targetDate does NOT mark them dead: " +
           "while accumulatedAmount >= targetAmount the funded reserve stands ready until the purchase actually happens; 'Open' plans are flexible buffers without a target. " +
           "Interpretation of analysis: accumulatedAmount may be NEGATIVE — that means Vorleistung (the purchase happened before the reserve was fully saved, e.g. a building-society loan disbursement, and the plan is being refilled) — a legitimate state, not a data error. " +
           "accumulatedAmount of 0 on a monthly-revolving plan (e.g. a credit-card settlement reserve deposited at month-end and spent mid-month) is normal too, not underfunding. " +
           "The list-level currentAmount field is not populated — the real balance lives in analysis.accumulatedAmount (pass includeAnalysis: true). " +
           "Scopes: (Sparpläne, Lesen), (Sparplankategorien, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Read),
        new McpToolScope(McpDataType.SavingsPlanCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_savings_plan_analysis</c>: adapts <see cref="ISavingsPlanService"/> and
/// <see cref="ISavingsPlanCategoryService"/>. Names, contract numbers and category names are
/// untrusted content (<c>untrusted_data</c>). The Sparplankategorien filter dimension
/// applies per plan via its category id.
/// </summary>
public sealed class FmSavingsPlanAnalysisHandler : IMcpToolHandler
{
    private readonly ISavingsPlanService _plans;
    private readonly ISavingsPlanCategoryService _categories;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmSavingsPlanAnalysisHandler(ISavingsPlanService plans, ISavingsPlanCategoryService categories, McpRecordScopeFilter recordFilter)
    {
        _plans = plans;
        _categories = categories;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmSavingsPlanAnalysisDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var savingsPlanId = McpToolArguments.OptionalGuid(arguments, "savingsPlanId");
        var onlyActive = McpToolArguments.OptionalBool(arguments, "onlyActive");
        var includeAnalysis = McpToolArguments.OptionalBool(arguments, "includeAnalysis");
        var includeCategories = McpToolArguments.OptionalBool(arguments, "includeCategories") ?? false;
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);

        var plans = new List<SavingsPlanDto>();
        if (savingsPlanId.HasValue)
        {
            var plan = await _plans.GetAsync(savingsPlanId.Value, owner, ct);
            if (plan is not null)
            {
                plans.Add(plan);
            }
            includeAnalysis ??= true;
        }
        else
        {
            // ISavingsPlanService lists per active-flag; merge both partitions when no
            // flag was supplied, then apply the paged window.
            if (onlyActive is null or true)
            {
                plans.AddRange(await _plans.ListAsync(owner, true, ct));
            }
            if (onlyActive is null or false)
            {
                plans.AddRange(await _plans.ListAsync(owner, false, ct));
            }
            plans = plans.Skip(skip).Take(take).ToList();
            includeAnalysis ??= false;
        }

        // Record-level filter (Sparplankategorien dimension via SavingsPlan.CategoryId).
        var visible = await _recordFilter.ApplyAsync(
            context.Access, McpDataType.SavingsPlans, plans,
            p => new McpRecordContext { SavingsPlanCategoryId = p.CategoryId }, ct);

        var result = new List<object>(visible.Count);
        foreach (var p in visible)
        {
            object? analysis = null;
            if (includeAnalysis.Value)
            {
                var a = await _plans.AnalyzeAsync(p.Id, owner, ct);
                analysis = new
                {
                    planId = a.PlanId,
                    targetReachable = a.TargetReachable,
                    targetAmount = a.TargetAmount,
                    targetDate = a.TargetDate,
                    accumulatedAmount = a.AccumulatedAmount,
                    requiredMonthly = a.RequiredMonthly,
                    monthsRemaining = a.MonthsRemaining
                };
            }
            result.Add(new
            {
                id = p.Id,
                type = p.Type.ToString(),
                targetAmount = p.TargetAmount,
                targetDate = p.TargetDate,
                interval = p.Interval?.ToString(),
                isActive = p.IsActive,
                createdUtc = p.CreatedUtc,
                archivedUtc = p.ArchivedUtc,
                categoryId = p.CategoryId,
                symbolAttachmentId = p.SymbolAttachmentId,
                remainingAmount = p.RemainingAmount,
                currentAmount = p.CurrentAmount,
                analysis,
                untrusted_data = new
                {
                    name = p.Name,
                    contractNumber = p.ContractNumber
                }
            });
        }

        object? categories = null;
        if (includeCategories)
        {
            categories = (await _categories.ListAsync(owner, ct))
                .Select(c => new
                {
                    id = c.Id,
                    symbolAttachmentId = c.SymbolAttachmentId,
                    untrusted_data = new { name = c.Name }
                }).ToList();
        }

        return new { savingsPlans = result, categories };
    }
}
