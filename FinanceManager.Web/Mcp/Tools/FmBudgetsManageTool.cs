using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Budget;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_budgets_manage</c> (T26): creates and
/// updates budget purposes (<c>BudgetPurpose</c> incl. <c>SourceType</c>/<c>SourceId</c>),
/// rules (<c>BudgetRule</c> incl. <c>PurposePattern</c>), monthly overrides
/// (<c>BudgetOverride</c>) and budget categories. The delete modes are
/// genehmigungspflichtig and answer <c>Err_Mcp_ApprovalRequired</c> without effect until
/// the approval queue ships (EP-09).
/// Scopes: (Budgets, Erstellung/Änderung/Vervollständigung), (Budgetregeln,
/// Erstellung/Änderung/Vervollständigung), (Budgetkategorien, Erstellung/Änderung);
/// deletes additionally the Löschung right of the entity type — evaluated inside the
/// handler.
/// </summary>
public sealed class FmBudgetsManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_budgets_manage";

    /// <summary>Creates the descriptor.</summary>
    public FmBudgetsManageDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"entity\":{\"type\":\"string\",\"enum\":[\"purpose\",\"rule\",\"override\",\"category\"],\"description\":\"Budget entity family (required).\"},\"operation\":{\"type\":\"string\",\"enum\":[\"create\",\"update\",\"delete\"],\"description\":\"Operation (required); delete requires approval and has no effect until EP-09.\"},\"purposeId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target purpose (update/delete) or parent purpose (rule/override create).\"},\"ruleId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target rule (update/delete).\"},\"overrideId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target override (update/delete).\"},\"categoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target category (category update/delete), rule-create binding or purpose assignment; explicit null clears the purpose assignment on update.\"},\"name\":{\"type\":\"string\",\"description\":\"Name (required for purpose/category create; optional overwrite).\"},\"description\":{\"type\":\"string\",\"description\":\"Purpose description; explicit null clears it on update.\"},\"sourceType\":{\"type\":\"string\",\"enum\":[\"Contact\",\"ContactGroup\",\"SavingsPlan\"],\"description\":\"Purpose source type (required for purpose create).\"},\"sourceId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Purpose source id — contact, contact category or savings plan (required for purpose create).\"},\"valuationType\":{\"type\":\"string\",\"enum\":[\"ExactPostings\",\"TotalBudget\"],\"description\":\"Purpose valuation type (default ExactPostings).\"},\"amount\":{\"type\":\"number\",\"description\":\"Rule/override amount (required for rule/override create).\"},\"interval\":{\"type\":\"string\",\"enum\":[\"Monthly\",\"Quarterly\",\"Yearly\",\"CustomMonths\"],\"description\":\"Rule interval (required for rule create).\"},\"customIntervalMonths\":{\"type\":\"integer\",\"minimum\":1,\"description\":\"Custom interval length in months (required when interval=CustomMonths); explicit null clears it on update.\"},\"startDate\":{\"type\":\"string\",\"description\":\"Rule start date ISO-8601 (required for rule create).\"},\"endDate\":{\"type\":\"string\",\"description\":\"Rule end date ISO-8601; explicit null clears it on update.\"},\"purposePattern\":{\"type\":\"string\",\"description\":\"Rule purpose pattern; explicit null clears it on update.\"},\"useRegex\":{\"type\":\"boolean\",\"description\":\"Whether purposePattern is a regular expression.\"},\"periodYear\":{\"type\":\"integer\",\"description\":\"Override period year (required for override create).\"},\"periodMonth\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":12,\"description\":\"Override period month 1-12 (required for override create).\"}},\"required\":[\"entity\",\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Creates and updates budget entities: purposes (entity=purpose, incl. sourceType/sourceId and valuationType), rules (entity=rule, bound to purposeId or categoryId, incl. purposePattern/useRegex), monthly overrides (entity=override, periodYear/periodMonth) and budget categories (entity=category). Delete operations require approval and answer Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Budgets/Budgetregeln, Erstellung/Änderung/Vervollständigung), (Budgetkategorien, Erstellung/Änderung); deletes additionally the Löschung right of the entity family.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Budgets, McpRightKind.Create),
        new McpToolScope(McpDataType.Budgets, McpRightKind.Modify),
        new McpToolScope(McpDataType.Budgets, McpRightKind.Complete),
        new McpToolScope(McpDataType.BudgetRules, McpRightKind.Create),
        new McpToolScope(McpDataType.BudgetRules, McpRightKind.Modify),
        new McpToolScope(McpDataType.BudgetRules, McpRightKind.Complete),
        new McpToolScope(McpDataType.BudgetCategories, McpRightKind.Create),
        new McpToolScope(McpDataType.BudgetCategories, McpRightKind.Modify)
    };
}

/// <summary>
/// Handler of <c>fm_budgets_manage</c> (F-0803): adapts
/// <see cref="IBudgetPurposeService"/>, <see cref="IBudgetRuleService"/>,
/// <see cref="IBudgetOverrideService"/> and <see cref="IBudgetCategoryService"/>.
/// Purpose <c>sourceId</c> resolves owner-scoped per <c>sourceType</c>
/// (Contact → contact, ContactGroup → contact category, SavingsPlan → savings plan)
/// before any effect. Update operations merge provided fields over the current state;
/// the record-level write filter evaluates against an unclassified record (filtered
/// write rows deny — budget categories are no filter dimension). Each effect commits
/// atomically with its staged <c>DataChange</c> record (F-0301); all deletes answer the
/// structured approval-required result without any service call.
/// </summary>
public sealed class FmBudgetsManageHandler : IMcpToolHandler
{
    private const string EntityPurpose = "BudgetPurpose";
    private const string EntityRule = "BudgetRule";
    private const string EntityOverride = "BudgetOverride";
    private const string EntityCategory = "BudgetCategory";

    private readonly IBudgetPurposeService _purposes;
    private readonly IBudgetRuleService _rules;
    private readonly IBudgetOverrideService _overrides;
    private readonly IBudgetCategoryService _categories;
    private readonly IContactService _contacts;
    private readonly IContactCategoryService _contactCategories;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmBudgetsManageHandler(
        IBudgetPurposeService purposes,
        IBudgetRuleService rules,
        IBudgetOverrideService overrides,
        IBudgetCategoryService categories,
        IContactService contacts,
        IContactCategoryService contactCategories,
        ISavingsPlanService savingsPlans,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _purposes = purposes;
        _rules = rules;
        _overrides = overrides;
        _categories = categories;
        _contacts = contacts;
        _contactCategories = contactCategories;
        _savingsPlans = savingsPlans;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmBudgetsManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var entity = McpToolArguments.OptionalString(arguments, "entity", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'entity' is required.");
        var operation = McpToolArguments.OptionalString(arguments, "operation", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        return (entity, operation) switch
        {
            ("purpose", "create") => await CreatePurposeAsync(arguments, owner, context, ct),
            ("purpose", "update") => await UpdatePurposeAsync(arguments, owner, context, ct),
            ("purpose", "delete") => await DeletePurposeAsync(arguments, owner, context, ct),
            ("rule", "create") => await CreateRuleAsync(arguments, owner, context, ct),
            ("rule", "update") => await UpdateRuleAsync(arguments, owner, context, ct),
            ("rule", "delete") => await DeleteRuleAsync(arguments, owner, context, ct),
            ("override", "create") => await CreateOverrideAsync(arguments, owner, context, ct),
            ("override", "update") => await UpdateOverrideAsync(arguments, owner, context, ct),
            ("override", "delete") => await DeleteOverrideAsync(arguments, owner, context, ct),
            ("category", "create") => await CreateCategoryAsync(arguments, owner, context, ct),
            ("category", "update") => await UpdateCategoryAsync(arguments, owner, context, ct),
            ("category", "delete") => await DeleteCategoryAsync(arguments, owner, context, ct),
            ("purpose" or "rule" or "override" or "category", _) => throw new McpToolException(
                "Err_Mcp_InvalidArguments", "Argument 'operation' must be one of create|update|delete."),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'entity' must be one of purpose|rule|override|category.")
        };
    }

    // ------------------------------------------------------------------
    // purpose
    // ------------------------------------------------------------------

    private async Task<object> CreatePurposeAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        var sourceType = McpToolArguments.OptionalEnum<BudgetSourceType>(arguments, "sourceType")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'sourceType' is required for purpose create.");
        var sourceId = McpToolArguments.RequiredGuid(arguments, "sourceId");
        var description = McpToolArguments.OptionalString(arguments, "description", McpMasterDataGuards.MaxDescriptionLength);
        var categoryId = McpToolArguments.OptionalGuid(arguments, "categoryId");
        var valuation = McpToolArguments.OptionalEnum<BudgetValuationType>(arguments, "valuationType")
            ?? BudgetValuationType.ExactPostings;

        await ResolveSourceAsync(sourceType, sourceId, owner, ct);
        if (categoryId.HasValue)
        {
            await McpMasterDataGuards.RequireBudgetCategoryAsync(_categories, categoryId.Value, owner, ct);
        }
        await RequireWritableAsync(context, McpDataType.Budgets, McpRightKind.Create, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityPurpose, Guid.Empty, "Created", null, $"{sourceType}:{sourceId}|{name}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetPurposeCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "purpose", operation = "create", sourceType = sourceType.ToString(), sourceId, categoryId }), ct);
        BudgetPurposeDto created;
        try
        {
            created = await _purposes.CreateAsync(owner, name, sourceType, sourceId, description, categoryId, ct, valuation);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new { entity = "purpose", operation = "create", applied = true, purpose = PurposeResult(created) };
    }

    private async Task<object> UpdatePurposeAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var purposeId = McpToolArguments.RequiredGuid(arguments, "purposeId");
        var existing = await _purposes.GetAsync(purposeId, owner, ct);
        if (existing is null)
        {
            return new { entity = "purpose", operation = "update", purpose = (object?)null };
        }
        await RequireWritableAsync(context, McpDataType.Budgets, McpRightKind.Modify, ct);

        var effName = McpToolArguments.Has(arguments, "name")
            ? McpMasterDataGuards.RequiredName(arguments)
            : existing.Name;
        var effDescription = McpToolArguments.Has(arguments, "description")
            ? McpToolArguments.OptionalString(arguments, "description", McpMasterDataGuards.MaxDescriptionLength)
            : existing.Description;
        var effSourceType = McpToolArguments.Has(arguments, "sourceType")
            ? McpToolArguments.OptionalEnum<BudgetSourceType>(arguments, "sourceType")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'sourceType' must not be null.")
            : existing.SourceType;
        var effSourceId = McpToolArguments.Has(arguments, "sourceId")
            ? McpToolArguments.RequiredGuid(arguments, "sourceId")
            : existing.SourceId;
        var effCategory = McpToolArguments.Has(arguments, "categoryId")
            ? McpToolArguments.OptionalGuid(arguments, "categoryId")
            : existing.BudgetCategoryId;
        var effValuation = McpToolArguments.Has(arguments, "valuationType")
            ? McpToolArguments.OptionalEnum<BudgetValuationType>(arguments, "valuationType")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'valuationType' must not be null.")
            : existing.ValuationType;

        await ResolveSourceAsync(effSourceType, effSourceId, owner, ct);
        if (effCategory.HasValue)
        {
            await McpMasterDataGuards.RequireBudgetCategoryAsync(_categories, effCategory.Value, owner, ct);
        }

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityPurpose, purposeId, "Name", existing.Name, effName);
        McpMasterDataGuards.AddChange(changes, EntityPurpose, purposeId, "Description", existing.Description, effDescription);
        McpMasterDataGuards.AddChange(changes, EntityPurpose, purposeId, "SourceType", existing.SourceType, effSourceType);
        McpMasterDataGuards.AddChange(changes, EntityPurpose, purposeId, "SourceId", existing.SourceId, effSourceId);
        McpMasterDataGuards.AddChange(changes, EntityPurpose, purposeId, "BudgetCategoryId", existing.BudgetCategoryId, effCategory);
        McpMasterDataGuards.AddChange(changes, EntityPurpose, purposeId, "ValuationType", existing.ValuationType, effValuation);
        if (changes.Count == 0)
        {
            return new { entity = "purpose", operation = "update", applied = false, reason = "unchanged", purpose = PurposeResult(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetPurposeUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "purpose", operation = "update", purposeId }), ct);
        BudgetPurposeDto? updated;
        try
        {
            updated = await _purposes.UpdateAsync(purposeId, owner, effName, effSourceType, effSourceId, effDescription, effCategory, ct, effValuation);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The budget purpose could not be updated.");
        }
        return new
        {
            entity = "purpose",
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            purpose = PurposeResult(updated)
        };
    }

    private async Task<object> DeletePurposeAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var purposeId = McpToolArguments.RequiredGuid(arguments, "purposeId");
        var existing = await _purposes.GetAsync(purposeId, owner, ct);
        if (McpMasterDataGuards.ThrowIfExecutionTargetGone(context, existing, "delete"))
        {
            return new { entity = "purpose", operation = "delete", purpose = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Budgets, "delete",
            "Deletes the budget purpose including its rules and overrides; the deletion is final.",
            new { entity = "purpose", purposeId }, ct,
            executeApproved: async ct2 =>
            {
                var changes = new List<McpAuditFieldChange>
                {
                    new(EntityPurpose, purposeId, "Deleted", existing!.Name, null)
                };
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BudgetPurposeDeleted", ToolName, changes, 1,
                    JsonSerializer.Serialize(new { entity = "purpose", operation = "delete", purposeId }), ct2);
                if (!await _purposes.DeleteAsync(purposeId, owner, ct2))
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_TargetGone", "The budget purpose no longer exists.");
                }
                return (object)new { entity = "purpose", operation = "delete", applied = true, purposeId };
            });
    }

    // ------------------------------------------------------------------
    // rule
    // ------------------------------------------------------------------

    private async Task<object> CreateRuleAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var purposeId = McpToolArguments.OptionalGuid(arguments, "purposeId");
        var categoryId = McpToolArguments.OptionalGuid(arguments, "categoryId");
        if ((purposeId.HasValue ? 1 : 0) + (categoryId.HasValue ? 1 : 0) != 1)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Exactly one of 'purposeId' or 'categoryId' is required for rule create.");
        }
        var amount = McpMasterDataGuards.OptionalDecimal(arguments, "amount")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'amount' is required for rule create.");
        var interval = McpToolArguments.OptionalEnum<BudgetIntervalType>(arguments, "interval")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'interval' is required for rule create.");
        var customMonths = McpToolArguments.OptionalInt(arguments, "customIntervalMonths");
        var startDate = McpMasterDataGuards.OptionalDateOnly(arguments, "startDate")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'startDate' is required for rule create.");
        var endDate = McpMasterDataGuards.OptionalDateOnly(arguments, "endDate");
        var pattern = McpToolArguments.OptionalString(arguments, "purposePattern", McpMasterDataGuards.MaxPurposePatternLength);
        var useRegex = McpToolArguments.OptionalBool(arguments, "useRegex") ?? false;

        ValidateRule(amount, interval, customMonths, startDate, endDate);
        if (purposeId.HasValue)
        {
            await McpMasterDataGuards.RequireBudgetPurposeAsync(_purposes, purposeId.Value, owner, ct);
        }
        else
        {
            await McpMasterDataGuards.RequireBudgetCategoryAsync(_categories, categoryId!.Value, owner, ct);
        }
        await RequireWritableAsync(context, McpDataType.BudgetRules, McpRightKind.Create, ct);

        var target = purposeId ?? categoryId!.Value;
        var changes = new List<McpAuditFieldChange>
        {
            new(EntityRule, Guid.Empty, "Created", null,
                $"{(purposeId.HasValue ? "purpose" : "category")}:{target}|{amount}|{interval}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetRuleCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "rule", operation = "create", purposeId, categoryId, amount, interval = interval.ToString() }), ct);
        BudgetRuleDto created;
        try
        {
            created = purposeId.HasValue
                ? await _rules.CreateAsync(owner, purposeId.Value, amount, interval, customMonths, startDate, endDate, pattern, useRegex, ct)
                : await _rules.CreateForCategoryAsync(owner, categoryId!.Value, amount, interval, customMonths, startDate, endDate, pattern, useRegex, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new { entity = "rule", operation = "create", applied = true, rule = RuleResult(created) };
    }

    private async Task<object> UpdateRuleAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var ruleId = McpToolArguments.RequiredGuid(arguments, "ruleId");
        var existing = await _rules.GetAsync(ruleId, owner, ct);
        if (existing is null)
        {
            return new { entity = "rule", operation = "update", rule = (object?)null };
        }
        await RequireWritableAsync(context, McpDataType.BudgetRules, McpRightKind.Modify, ct);

        var effAmount = McpToolArguments.Has(arguments, "amount")
            ? McpMasterDataGuards.OptionalDecimal(arguments, "amount")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'amount' must not be null.")
            : existing.Amount;
        var effInterval = McpToolArguments.Has(arguments, "interval")
            ? McpToolArguments.OptionalEnum<BudgetIntervalType>(arguments, "interval")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'interval' must not be null.")
            : existing.Interval;
        var effCustom = McpToolArguments.Has(arguments, "customIntervalMonths")
            ? McpToolArguments.OptionalInt(arguments, "customIntervalMonths")
            : existing.CustomIntervalMonths;
        var effStart = McpToolArguments.Has(arguments, "startDate")
            ? McpMasterDataGuards.OptionalDateOnly(arguments, "startDate")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'startDate' must not be null.")
            : existing.StartDate;
        var effEnd = McpToolArguments.Has(arguments, "endDate")
            ? McpMasterDataGuards.OptionalDateOnly(arguments, "endDate")
            : existing.EndDate;
        var effPattern = McpToolArguments.Has(arguments, "purposePattern")
            ? McpToolArguments.OptionalString(arguments, "purposePattern", McpMasterDataGuards.MaxPurposePatternLength)
            : existing.PurposePattern;
        var effUseRegex = McpToolArguments.Has(arguments, "useRegex")
            ? McpToolArguments.OptionalBool(arguments, "useRegex")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'useRegex' must not be null.")
            : existing.UseRegex;

        ValidateRule(effAmount, effInterval, effCustom, effStart, effEnd);

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityRule, ruleId, "Amount", existing.Amount, effAmount);
        McpMasterDataGuards.AddChange(changes, EntityRule, ruleId, "Interval", existing.Interval, effInterval);
        McpMasterDataGuards.AddChange(changes, EntityRule, ruleId, "CustomIntervalMonths", existing.CustomIntervalMonths, effCustom);
        McpMasterDataGuards.AddChange(changes, EntityRule, ruleId, "StartDate", existing.StartDate, effStart);
        McpMasterDataGuards.AddChange(changes, EntityRule, ruleId, "EndDate", existing.EndDate, effEnd);
        McpMasterDataGuards.AddChange(changes, EntityRule, ruleId, "PurposePattern", existing.PurposePattern, effPattern);
        McpMasterDataGuards.AddChange(changes, EntityRule, ruleId, "UseRegex", existing.UseRegex, effUseRegex);
        if (changes.Count == 0)
        {
            return new { entity = "rule", operation = "update", applied = false, reason = "unchanged", rule = RuleResult(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetRuleUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "rule", operation = "update", ruleId }), ct);
        BudgetRuleDto? updated;
        try
        {
            updated = await _rules.UpdateAsync(ruleId, owner, effAmount, effInterval, effCustom, effStart, effEnd, effPattern, effUseRegex, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The budget rule could not be updated.");
        }
        return new
        {
            entity = "rule",
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            rule = RuleResult(updated)
        };
    }

    private async Task<object> DeleteRuleAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var ruleId = McpToolArguments.RequiredGuid(arguments, "ruleId");
        var existing = await _rules.GetAsync(ruleId, owner, ct);
        if (McpMasterDataGuards.ThrowIfExecutionTargetGone(context, existing, "delete"))
        {
            return new { entity = "rule", operation = "delete", rule = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.BudgetRules, "delete",
            "Deletes the budget rule permanently; future budget periods lose this rule.",
            new { entity = "rule", ruleId }, ct,
            executeApproved: async ct2 =>
            {
                var changes = new List<McpAuditFieldChange>
                {
                    new(EntityRule, ruleId, "Deleted", ruleId.ToString(), null)
                };
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BudgetRuleDeleted", ToolName, changes, 1,
                    JsonSerializer.Serialize(new { entity = "rule", operation = "delete", ruleId }), ct2);
                if (!await _rules.DeleteAsync(ruleId, owner, ct2))
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_TargetGone", "The budget rule no longer exists.");
                }
                return (object)new { entity = "rule", operation = "delete", applied = true, ruleId };
            });
    }

    // ------------------------------------------------------------------
    // override (monthly amount of a purpose — vervollständigend)
    // ------------------------------------------------------------------

    private async Task<object> CreateOverrideAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var purposeId = McpToolArguments.RequiredGuid(arguments, "purposeId");
        var amount = McpMasterDataGuards.OptionalDecimal(arguments, "amount")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'amount' is required for override create.");
        var period = RequirePeriod(arguments);

        await McpMasterDataGuards.RequireBudgetPurposeAsync(_purposes, purposeId, owner, ct);
        await RequireWritableAsync(context, McpDataType.Budgets, McpRightKind.Complete, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityOverride, Guid.Empty, "Created", null, $"{period}|{amount}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetOverrideCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "override", operation = "create", purposeId, period = period.ToString(), amount }), ct);
        BudgetOverrideDto created;
        try
        {
            created = await _overrides.CreateAsync(owner, purposeId, period, amount, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new { entity = "override", operation = "create", applied = true, @override = OverrideResult(created) };
    }

    private async Task<object> UpdateOverrideAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var overrideId = McpToolArguments.RequiredGuid(arguments, "overrideId");
        var existing = await _overrides.GetAsync(overrideId, owner, ct);
        if (existing is null)
        {
            return new { entity = "override", operation = "update", @override = (object?)null };
        }
        await RequireWritableAsync(context, McpDataType.Budgets, McpRightKind.Modify, ct);

        var effPeriod = McpToolArguments.Has(arguments, "periodYear") || McpToolArguments.Has(arguments, "periodMonth")
            ? RequirePeriod(arguments)
            : new BudgetPeriodKey(existing.PeriodYear, existing.PeriodMonth);
        var effAmount = McpToolArguments.Has(arguments, "amount")
            ? McpMasterDataGuards.OptionalDecimal(arguments, "amount")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'amount' must not be null.")
            : existing.Amount;

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityOverride, overrideId, "Period",
            $"{existing.PeriodYear:D4}-{existing.PeriodMonth:D2}", effPeriod.ToString());
        McpMasterDataGuards.AddChange(changes, EntityOverride, overrideId, "Amount", existing.Amount, effAmount);
        if (changes.Count == 0)
        {
            return new { entity = "override", operation = "update", applied = false, reason = "unchanged", @override = OverrideResult(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetOverrideUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "override", operation = "update", overrideId }), ct);
        BudgetOverrideDto? updated;
        try
        {
            updated = await _overrides.UpdateAsync(overrideId, owner, effPeriod, effAmount, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The budget override could not be updated.");
        }
        return new
        {
            entity = "override",
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            @override = OverrideResult(updated)
        };
    }

    private async Task<object> DeleteOverrideAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var overrideId = McpToolArguments.RequiredGuid(arguments, "overrideId");
        var existing = await _overrides.GetAsync(overrideId, owner, ct);
        if (McpMasterDataGuards.ThrowIfExecutionTargetGone(context, existing, "delete"))
        {
            return new { entity = "override", operation = "delete", @override = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Budgets, "delete",
            "Deletes the monthly budget override; the period falls back to the rule amount.",
            new { entity = "override", overrideId }, ct,
            executeApproved: async ct2 =>
            {
                var changes = new List<McpAuditFieldChange>
                {
                    new(EntityOverride, overrideId, "Deleted", overrideId.ToString(), null)
                };
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BudgetOverrideDeleted", ToolName, changes, 1,
                    JsonSerializer.Serialize(new { entity = "override", operation = "delete", overrideId }), ct2);
                if (!await _overrides.DeleteAsync(overrideId, owner, ct2))
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_TargetGone", "The budget override no longer exists.");
                }
                return (object)new { entity = "override", operation = "delete", applied = true, overrideId };
            });
    }

    // ------------------------------------------------------------------
    // category
    // ------------------------------------------------------------------

    private async Task<object> CreateCategoryAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        await RequireWritableAsync(context, McpDataType.BudgetCategories, McpRightKind.Create, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityCategory, Guid.Empty, "Created", null, name)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetCategoryCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "category", operation = "create", name }), ct);
        BudgetCategoryDto created;
        try
        {
            created = await _categories.CreateAsync(owner, name, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new
        {
            entity = "category",
            operation = "create",
            applied = true,
            category = CategoryResult(created)
        };
    }

    private async Task<object> UpdateCategoryAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var categoryId = McpToolArguments.RequiredGuid(arguments, "categoryId");
        var name = McpMasterDataGuards.RequiredName(arguments);
        var existing = await _categories.GetAsync(categoryId, owner, ct);
        if (existing is null)
        {
            return new { entity = "category", operation = "update", category = (object?)null };
        }
        if (string.Equals(existing.Name, name, StringComparison.Ordinal))
        {
            return new { entity = "category", operation = "update", applied = false, reason = "unchanged", category = CategoryResult(existing) };
        }
        await RequireWritableAsync(context, McpDataType.BudgetCategories, McpRightKind.Modify, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityCategory, categoryId, "Name", existing.Name, name)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "BudgetCategoryUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { entity = "category", operation = "update", categoryId }), ct);
        BudgetCategoryDto? updated;
        try
        {
            updated = await _categories.UpdateAsync(categoryId, owner, name, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DomainValidationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The budget category could not be updated.");
        }
        return new { entity = "category", operation = "update", applied = true, category = CategoryResult(updated) };
    }

    private async Task<object> DeleteCategoryAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var categoryId = McpToolArguments.RequiredGuid(arguments, "categoryId");
        var existing = await _categories.GetAsync(categoryId, owner, ct);
        if (McpMasterDataGuards.ThrowIfExecutionTargetGone(context, existing, "delete"))
        {
            return new { entity = "category", operation = "delete", category = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.BudgetCategories, "delete",
            "Deletes the budget category permanently; assigned purposes lose their " +
            "category link and category-scoped rules are removed.",
            new { entity = "category", categoryId }, ct,
            executeApproved: async ct2 =>
            {
                var changes = new List<McpAuditFieldChange>
                {
                    new(EntityCategory, categoryId, "Deleted", existing!.Name, null)
                };
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "BudgetCategoryDeleted", ToolName, changes, 1,
                    JsonSerializer.Serialize(new { entity = "category", operation = "delete", categoryId }), ct2);
                if (!await _categories.DeleteAsync(categoryId, owner, ct2))
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_TargetGone", "The budget category no longer exists.");
                }
                return (object)new { entity = "category", operation = "delete", applied = true, categoryId };
            });
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    private async Task RequireWritableAsync(McpToolCallContext context, McpDataType dataType, McpRightKind right, CancellationToken ct)
        => await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, dataType, right, new McpRecordContext(), ct);

    private async Task ResolveSourceAsync(BudgetSourceType sourceType, Guid sourceId, Guid owner, CancellationToken ct)
    {
        switch (sourceType)
        {
            case BudgetSourceType.Contact:
                _ = await _contacts.GetAsync(sourceId, owner, ct)
                    ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                        "The referenced contact does not exist or is not owned by the caller.");
                break;
            case BudgetSourceType.ContactGroup:
                _ = await _contactCategories.GetAsync(sourceId, owner, ct)
                    ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                        "The referenced contact group does not exist or is not owned by the caller.");
                break;
            case BudgetSourceType.SavingsPlan:
                _ = await _savingsPlans.GetAsync(sourceId, owner, ct)
                    ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                        "The referenced savings plan does not exist or is not owned by the caller.");
                break;
        }
    }

    private static void ValidateRule(decimal amount, BudgetIntervalType interval, int? customMonths, DateOnly start, DateOnly? end)
    {
        if (amount <= 0m)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'amount' must be greater than zero.");
        }
        if (interval == BudgetIntervalType.CustomMonths && customMonths is null or <= 0)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'customIntervalMonths' is required and must be positive when interval=CustomMonths.");
        }
        if (end.HasValue && end.Value < start)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'endDate' must not precede 'startDate'.");
        }
    }

    private static BudgetPeriodKey RequirePeriod(JsonElement arguments)
    {
        var year = McpToolArguments.OptionalInt(arguments, "periodYear");
        var month = McpToolArguments.OptionalInt(arguments, "periodMonth");
        if (year is null || month is null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Arguments 'periodYear' and 'periodMonth' are required for the override period.");
        }
        if (year is < 2000 or > 2200)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'periodYear' is out of range (2000-2200).");
        }
        var period = new BudgetPeriodKey(year.Value, month.Value);
        try
        {
            period.Validate();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'periodMonth' must be between 1 and 12.");
        }
        return period;
    }

    private static object PurposeResult(BudgetPurposeDto p)
        => new
        {
            id = p.Id,
            sourceType = p.SourceType.ToString(),
            sourceId = p.SourceId,
            budgetCategoryId = p.BudgetCategoryId,
            valuationType = p.ValuationType.ToString(),
            untrusted_data = new { name = p.Name, description = p.Description }
        };

    private static object RuleResult(BudgetRuleDto r)
        => new
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
        };

    private static object OverrideResult(BudgetOverrideDto o)
        => new
        {
            id = o.Id,
            budgetPurposeId = o.BudgetPurposeId,
            periodYear = o.PeriodYear,
            periodMonth = o.PeriodMonth,
            amount = o.Amount
        };

    private static object CategoryResult(BudgetCategoryDto c)
        => new
        {
            id = c.Id,
            untrusted_data = new { name = c.Name }
        };
}
