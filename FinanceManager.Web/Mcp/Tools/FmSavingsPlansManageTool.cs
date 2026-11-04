using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.SavingsPlans;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_savings_plans_manage</c> (T24): creates
/// and updates savings plans (<c>Type</c>, <c>TargetAmount</c>/<c>TargetDate</c>,
/// <c>Interval</c>, <c>ContractNumber</c>, category). The <c>archive</c> operation —
/// the only delete-equivalent for savings plans (fachkonzept §5.5: archiving instead
/// of deletion; <c>DeleteAsync</c> is not exposed) — is genehmigungspflichtig and
/// answers <c>Err_Mcp_ApprovalRequired</c> without effect until the approval queue
/// ships (EP-09).
/// Scopes: (Sparpläne, Erstellung/Änderung), (Sparplankategorien, Lesen); archive
/// additionally (Sparpläne, Löschung) — evaluated inside the handler.
/// </summary>
public sealed class FmSavingsPlansManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_savings_plans_manage";

    /// <summary>Creates the descriptor.</summary>
    public FmSavingsPlansManageDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"create\",\"update\",\"archive\"],\"description\":\"Master-data operation (required); archive requires approval and has no effect until EP-09.\"},\"savingsPlanId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target savings plan (required for update/archive).\"},\"name\":{\"type\":\"string\",\"description\":\"Plan name (required for create; optional overwrite for update).\"},\"savingsPlanType\":{\"type\":\"string\",\"enum\":[\"OneTime\",\"Recurring\",\"Open\"],\"description\":\"Plan type (required for create; optional overwrite for update).\"},\"targetAmount\":{\"type\":\"number\",\"description\":\"Target amount; explicit null clears it on update.\"},\"targetDate\":{\"type\":\"string\",\"format\":\"date-time\",\"description\":\"Target date; explicit null clears it on update.\"},\"interval\":{\"type\":\"string\",\"enum\":[\"Monthly\",\"BiMonthly\",\"Quarterly\",\"SemiAnnually\",\"Annually\"],\"description\":\"Savings interval; explicit null clears it on update.\"},\"categoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Category assignment; explicit null clears it on update.\"},\"contractNumber\":{\"type\":\"string\",\"description\":\"Contract number; explicit null clears it on update.\"}},\"required\":[\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Creates and updates savings plans (name, savingsPlanType, targetAmount, targetDate, interval, categoryId, contractNumber). Archiving — the only delete-equivalent — requires approval and answers Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Sparpläne, Erstellung/Änderung), (Sparplankategorien, Lesen); archive additionally (Sparpläne, Löschung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Create),
        new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Modify),
        new McpToolScope(McpDataType.SavingsPlanCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_savings_plans_manage</c> (F-0802): adapts
/// <see cref="ISavingsPlanService"/>. Update merges the provided fields over the current
/// state; explicit JSON null clears <c>targetAmount</c>/<c>targetDate</c>/<c>interval</c>/
/// <c>categoryId</c>/<c>contractNumber</c>. Category references resolve owner-scoped via
/// <see cref="ISavingsPlanCategoryService"/>; the record-level scope filter binds the
/// plan's category. Each effect commits atomically with its staged <c>DataChange</c>
/// record (F-0301); archive answers approval-required without any service call.
/// </summary>
public sealed class FmSavingsPlansManageHandler : IMcpToolHandler
{
    private const string EntityType = "SavingsPlan";

    private readonly ISavingsPlanService _plans;
    private readonly ISavingsPlanCategoryService _categories;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmSavingsPlansManageHandler(
        ISavingsPlanService plans,
        ISavingsPlanCategoryService categories,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _plans = plans;
        _categories = categories;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmSavingsPlansManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var operation = McpToolArguments.OptionalString(arguments, "operation", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        return operation switch
        {
            "create" => await CreateAsync(arguments, owner, context, ct),
            "update" => await UpdateAsync(arguments, owner, context, ct),
            "archive" => await ArchiveAsync(arguments, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of create|update|archive.")
        };
    }

    private async Task<object> CreateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        var type = McpToolArguments.OptionalEnum<SavingsPlanType>(arguments, "savingsPlanType")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'savingsPlanType' is required for create.");
        var targetAmount = McpMasterDataGuards.OptionalDecimal(arguments, "targetAmount");
        var targetDate = McpToolArguments.OptionalDateTime(arguments, "targetDate");
        var interval = McpToolArguments.OptionalEnum<SavingsPlanInterval>(arguments, "interval");
        var categoryId = McpToolArguments.OptionalGuid(arguments, "categoryId");
        var contractNumber = McpToolArguments.OptionalString(arguments, "contractNumber", McpMasterDataGuards.MaxContractNumberLength);

        if (targetAmount is < 0m)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'targetAmount' must not be negative.");
        }
        if (categoryId.HasValue)
        {
            await McpMasterDataGuards.RequireSavingsPlanCategoryAsync(_categories, categoryId.Value, owner, ct);
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.SavingsPlans, McpRightKind.Create,
            new McpRecordContext { SavingsPlanCategoryId = categoryId }, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, Guid.Empty, "Created", null, $"{type}|{name}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "SavingsPlanCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "create", name, savingsPlanType = type.ToString(), categoryId }), ct);
        SavingsPlanDto created;
        try
        {
            created = await _plans.CreateAsync(owner, name, type, targetAmount, targetDate, interval, categoryId, contractNumber, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new { operation = "create", applied = true, savingsPlan = Result(created) };
    }

    private async Task<object> UpdateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var planId = McpToolArguments.RequiredGuid(arguments, "savingsPlanId");
        var existing = await _plans.GetAsync(planId, owner, ct);
        if (existing is null)
        {
            return new { operation = "update", savingsPlan = (object?)null };
        }
        if (existing.ArchivedUtc is not null)
        {
            throw new McpToolException("Err_Mcp_InvalidState",
                "The savings plan is archived and cannot be updated.");
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.SavingsPlans, McpRightKind.Modify,
            new McpRecordContext { SavingsPlanCategoryId = existing.CategoryId }, ct);

        var effName = McpToolArguments.Has(arguments, "name")
            ? McpMasterDataGuards.RequiredName(arguments)
            : existing.Name;
        var effType = McpToolArguments.Has(arguments, "savingsPlanType")
            ? McpToolArguments.OptionalEnum<SavingsPlanType>(arguments, "savingsPlanType")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'savingsPlanType' must not be null.")
            : existing.Type;
        var effAmount = McpToolArguments.Has(arguments, "targetAmount")
            ? McpMasterDataGuards.OptionalDecimal(arguments, "targetAmount")
            : existing.TargetAmount;
        var effDate = McpToolArguments.Has(arguments, "targetDate")
            ? McpToolArguments.OptionalDateTime(arguments, "targetDate")
            : existing.TargetDate;
        var effInterval = McpToolArguments.Has(arguments, "interval")
            ? McpToolArguments.OptionalEnum<SavingsPlanInterval>(arguments, "interval")
            : existing.Interval;
        var effCategory = McpToolArguments.Has(arguments, "categoryId")
            ? McpToolArguments.OptionalGuid(arguments, "categoryId")
            : existing.CategoryId;
        var effContract = McpToolArguments.Has(arguments, "contractNumber")
            ? McpToolArguments.OptionalString(arguments, "contractNumber", McpMasterDataGuards.MaxContractNumberLength)
            : existing.ContractNumber;

        if (effAmount is < 0m)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'targetAmount' must not be negative.");
        }
        if (effCategory.HasValue)
        {
            await McpMasterDataGuards.RequireSavingsPlanCategoryAsync(_categories, effCategory.Value, owner, ct);
            await McpMasterDataGuards.RequireRecordWritableAsync(
                _recordFilter, context.Access, McpDataType.SavingsPlans, McpRightKind.Modify,
                new McpRecordContext { SavingsPlanCategoryId = effCategory }, ct);
        }

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityType, planId, "Name", existing.Name, effName);
        McpMasterDataGuards.AddChange(changes, EntityType, planId, "Type", existing.Type, effType);
        McpMasterDataGuards.AddChange(changes, EntityType, planId, "TargetAmount", existing.TargetAmount, effAmount);
        McpMasterDataGuards.AddChange(changes, EntityType, planId, "TargetDate", existing.TargetDate, effDate);
        McpMasterDataGuards.AddChange(changes, EntityType, planId, "Interval", existing.Interval, effInterval);
        McpMasterDataGuards.AddChange(changes, EntityType, planId, "CategoryId", existing.CategoryId, effCategory);
        McpMasterDataGuards.AddChange(changes, EntityType, planId, "ContractNumber", existing.ContractNumber, effContract);
        if (changes.Count == 0)
        {
            return new { operation = "update", applied = false, reason = "unchanged", savingsPlan = Result(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "SavingsPlanUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "update", savingsPlanId = planId }), ct);
        SavingsPlanDto? updated;
        try
        {
            updated = await _plans.UpdateAsync(planId, owner, effName, effType, effAmount, effDate, effInterval, effCategory, effContract, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
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
            throw new McpToolException("Err_Mcp_InvalidState", "The savings plan could not be updated.");
        }
        return new
        {
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            savingsPlan = Result(updated)
        };
    }

    private async Task<object> ArchiveAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var planId = McpToolArguments.RequiredGuid(arguments, "savingsPlanId");
        var existing = await _plans.GetAsync(planId, owner, ct);
        if (existing is null)
        {
            return new { operation = "archive", savingsPlan = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.SavingsPlans, "archive",
            "Archives the savings plan (sets ArchivedUtc / IsActive=false) — the only " +
            "delete-equivalent for savings plans. The plan stops matching new draft " +
            "entries; existing postings keep their reference.",
            new { savingsPlanId = planId }, ct);
    }

    private static object Result(SavingsPlanDto p)
        => new
        {
            id = p.Id,
            savingsPlanType = p.Type.ToString(),
            targetAmount = p.TargetAmount,
            targetDate = p.TargetDate,
            interval = p.Interval?.ToString(),
            categoryId = p.CategoryId,
            isActive = p.IsActive,
            archivedUtc = p.ArchivedUtc,
            untrusted_data = new { name = p.Name, contractNumber = p.ContractNumber }
        };
}
