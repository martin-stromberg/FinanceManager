using FinanceManager.Application.Attachments;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Attachments;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_categories_manage</c> (T27): creates and
/// renames the four category types — <c>ContactCategory</c> (Kontaktgruppen),
/// <c>SavingsPlanCategory</c>, <c>SecurityCategory</c>, <c>AttachmentCategory</c>. The
/// delete modes are genehmigungspflichtig and answer <c>Err_Mcp_ApprovalRequired</c>
/// without effect until the approval queue ships (EP-09).
/// Scopes: (Kontaktgruppen/Sparplankategorien/Wertpapierkategorien/Anhangkategorien,
/// Erstellung/Änderung); delete additionally the Löschung right of the category type —
/// evaluated inside the handler.
/// </summary>
public sealed class FmCategoriesManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_categories_manage";

    /// <summary>Creates the descriptor.</summary>
    public FmCategoriesManageDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"categoryType\":{\"type\":\"string\",\"enum\":[\"contact\",\"savingsPlan\",\"security\",\"attachment\"],\"description\":\"Category family (required).\"},\"operation\":{\"type\":\"string\",\"enum\":[\"create\",\"update\",\"delete\"],\"description\":\"Operation (required); delete requires approval and has no effect until EP-09.\"},\"categoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target category (required for update/delete).\"},\"name\":{\"type\":\"string\",\"description\":\"Category name (required for create/update).\"}},\"required\":[\"categoryType\",\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Creates and renames categories of the four families (contact/contact-group, savingsPlan, security, attachment). The delete operations require approval and answer Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Kontaktgruppen/Sparplankategorien/Wertpapierkategorien/Anhangkategorien, Erstellung/Änderung); delete additionally the Löschung right of the category family.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.ContactCategories, McpRightKind.Create),
        new McpToolScope(McpDataType.ContactCategories, McpRightKind.Modify),
        new McpToolScope(McpDataType.SavingsPlanCategories, McpRightKind.Create),
        new McpToolScope(McpDataType.SavingsPlanCategories, McpRightKind.Modify),
        new McpToolScope(McpDataType.SecurityCategories, McpRightKind.Create),
        new McpToolScope(McpDataType.SecurityCategories, McpRightKind.Modify),
        new McpToolScope(McpDataType.AttachmentCategories, McpRightKind.Create),
        new McpToolScope(McpDataType.AttachmentCategories, McpRightKind.Modify)
    };
}

/// <summary>
/// Handler of <c>fm_categories_manage</c> (F-0801): adapts the four
/// <c>I*CategoryService</c> contracts. The <c>categoryType</c> selector chooses the data
/// type whose Create/Modify right the record-level filter evaluates; the category's own
/// id acts as its classification value (a filtered profile can only rename whitelisted
/// categories; creating a new category is denied by filtered rows because the new record
/// is unclassifiable). (Owner,Name) uniqueness violations surface as
/// <c>Err_Mcp_InvalidArguments</c>. Each effect commits atomically with its staged
/// <c>DataChange</c> record (F-0301); delete answers approval-required without effect.
/// </summary>
public sealed class FmCategoriesManageHandler : IMcpToolHandler
{
    private readonly IContactCategoryService _contactCategories;
    private readonly ISavingsPlanCategoryService _savingsPlanCategories;
    private readonly ISecurityCategoryService _securityCategories;
    private readonly IAttachmentCategoryService _attachmentCategories;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmCategoriesManageHandler(
        IContactCategoryService contactCategories,
        ISavingsPlanCategoryService savingsPlanCategories,
        ISecurityCategoryService securityCategories,
        IAttachmentCategoryService attachmentCategories,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _contactCategories = contactCategories;
        _savingsPlanCategories = savingsPlanCategories;
        _securityCategories = securityCategories;
        _attachmentCategories = attachmentCategories;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmCategoriesManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var categoryType = McpToolArguments.OptionalString(arguments, "categoryType", 30)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'categoryType' is required.");
        var operation = McpToolArguments.OptionalString(arguments, "operation", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        var (dataType, entityLabel) = MapType(categoryType);
        return operation switch
        {
            "create" => await CreateAsync(arguments, dataType, entityLabel, owner, context, ct),
            "update" => await UpdateAsync(arguments, dataType, entityLabel, owner, context, ct),
            "delete" => await DeleteAsync(arguments, dataType, entityLabel, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of create|update|delete.")
        };
    }

    private static (McpDataType DataType, string EntityLabel) MapType(string categoryType)
        => categoryType switch
        {
            "contact" => (McpDataType.ContactCategories, "ContactCategory"),
            "savingsPlan" => (McpDataType.SavingsPlanCategories, "SavingsPlanCategory"),
            "security" => (McpDataType.SecurityCategories, "SecurityCategory"),
            "attachment" => (McpDataType.AttachmentCategories, "AttachmentCategory"),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'categoryType' must be one of contact|savingsPlan|security|attachment.")
        };

    private static McpRecordContext RecordFor(McpDataType dataType, Guid? categoryId)
        => dataType switch
        {
            McpDataType.ContactCategories => new McpRecordContext { ContactCategoryId = categoryId },
            McpDataType.SavingsPlanCategories => new McpRecordContext { SavingsPlanCategoryId = categoryId },
            McpDataType.SecurityCategories => new McpRecordContext { SecurityCategoryId = categoryId },
            _ => new McpRecordContext { AttachmentCategoryId = categoryId }
        };

    private async Task<object?> GetExistingAsync(McpDataType dataType, Guid id, Guid owner, CancellationToken ct)
        => dataType switch
        {
            McpDataType.ContactCategories => await _contactCategories.GetAsync(id, owner, ct),
            McpDataType.SavingsPlanCategories => await _savingsPlanCategories.GetAsync(id, owner, ct),
            McpDataType.SecurityCategories => await _securityCategories.GetAsync(id, owner, ct),
            _ => (await _attachmentCategories.ListAsync(owner, ct)).FirstOrDefault(c => c.Id == id)
        };

    private static string? NameOf(object? dto)
        => dto switch
        {
            ContactCategoryDto c => c.Name,
            SavingsPlanCategoryDto c => c.Name,
            SecurityCategoryDto c => c.Name,
            AttachmentCategoryDto c => c.Name,
            _ => null
        };

    private async Task<object> CreateAsync(
        JsonElement arguments, McpDataType dataType, string entityLabel,
        Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        // A new category is unclassifiable — filtered create rows deny it.
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, dataType, McpRightKind.Create,
            RecordFor(dataType, null), ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(entityLabel, Guid.Empty, "Created", null, name)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "CategoryCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { categoryType = entityLabel, operation = "create", name }), ct);
        object dto;
        try
        {
            dto = dataType switch
            {
                McpDataType.ContactCategories => (object)await _contactCategories.CreateAsync(owner, name, ct),
                McpDataType.SavingsPlanCategories => await _savingsPlanCategories.CreateAsync(owner, name, ct),
                McpDataType.SecurityCategories => await _securityCategories.CreateAsync(owner, name, ct),
                _ => await _attachmentCategories.CreateAsync(owner, name, ct)
            };
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
        return new { operation = "create", applied = true, category = CategoryResult(dto, entityLabel) };
    }

    private async Task<object> UpdateAsync(
        JsonElement arguments, McpDataType dataType, string entityLabel,
        Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var categoryId = McpToolArguments.RequiredGuid(arguments, "categoryId");
        var name = McpMasterDataGuards.RequiredName(arguments);

        var existing = await GetExistingAsync(dataType, categoryId, owner, ct);
        if (existing is null)
        {
            return new { operation = "update", category = (object?)null };
        }
        var oldName = NameOf(existing);
        if (string.Equals(oldName, name, StringComparison.Ordinal))
        {
            return new { operation = "update", applied = false, reason = "unchanged", category = CategoryResult(existing, entityLabel) };
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, dataType, McpRightKind.Modify,
            RecordFor(dataType, categoryId), ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(entityLabel, categoryId, "Name", oldName, name)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "CategoryUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { categoryType = entityLabel, operation = "update", categoryId }), ct);
        try
        {
            switch (dataType)
            {
                case McpDataType.ContactCategories:
                    await _contactCategories.UpdateAsync(categoryId, owner, name, ct);
                    break;
                case McpDataType.SavingsPlanCategories:
                    if (await _savingsPlanCategories.UpdateAsync(categoryId, owner, name, ct) is null)
                    {
                        throw new McpToolException("Err_Mcp_InvalidState", "The category could not be updated.");
                    }
                    break;
                case McpDataType.SecurityCategories:
                    if (await _securityCategories.UpdateAsync(categoryId, owner, name, ct) is null)
                    {
                        throw new McpToolException("Err_Mcp_InvalidState", "The category could not be updated.");
                    }
                    break;
                default:
                    if (await _attachmentCategories.UpdateAsync(owner, categoryId, name, ct) is null)
                    {
                        throw new McpToolException("Err_Mcp_InvalidState", "The category could not be updated.");
                    }
                    break;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch (McpToolException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new
        {
            operation = "update",
            applied = true,
            category = new
            {
                id = categoryId,
                categoryType = entityLabel,
                untrusted_data = new { name }
            }
        };
    }

    private async Task<object> DeleteAsync(
        JsonElement arguments, McpDataType dataType, string entityLabel,
        Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var categoryId = McpToolArguments.RequiredGuid(arguments, "categoryId");
        var existing = await GetExistingAsync(dataType, categoryId, owner, ct);
        if (existing is null)
        {
            return new { operation = "delete", category = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context.Access, dataType, "delete",
            $"Deletes the {entityLabel} permanently. References of assigned records lose " +
            "their category link; the deletion is final.",
            new { categoryType = entityLabel, categoryId }, ct);
    }

    private static object CategoryResult(object dto, string entityLabel)
        => dto switch
        {
            ContactCategoryDto c => new
            {
                id = c.Id,
                categoryType = entityLabel,
                untrusted_data = new { name = c.Name }
            },
            SavingsPlanCategoryDto c => new
            {
                id = c.Id,
                categoryType = entityLabel,
                untrusted_data = new { name = c.Name }
            },
            SecurityCategoryDto c => new
            {
                id = c.Id,
                categoryType = entityLabel,
                untrusted_data = new { name = c.Name }
            },
            AttachmentCategoryDto c => new
            {
                id = c.Id,
                categoryType = entityLabel,
                isSystem = c.IsSystem,
                isDefault = c.IsDefault,
                untrusted_data = new { name = c.Name }
            },
            _ => new { categoryType = entityLabel }
        };
}
