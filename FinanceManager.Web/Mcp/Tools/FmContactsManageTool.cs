using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Contacts;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_contacts_manage</c> (T22): creates and
/// updates contacts (name, <c>ContactType</c>, category, description,
/// payment-intermediary flag) and maintains alias patterns. The destructive
/// sub-operations <c>merge</c> (irreversible — alias transfer, reference re-pointing,
/// source deletion) and <c>delete</c> are genehmigungspflichtig and answer
/// <c>Err_Mcp_ApprovalRequired</c> without effect until the approval queue ships (EP-09).
/// Scopes: (Kontakte, Erstellung/Änderung/Vervollständigung), (Kontaktgruppen, Lesen);
/// merge+delete additionally (Kontakte, Löschung) — evaluated inside the handler.
/// </summary>
public sealed class FmContactsManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_contacts_manage";

    /// <summary>Creates the descriptor.</summary>
    public FmContactsManageDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"create\",\"update\",\"addAlias\",\"removeAlias\",\"merge\",\"delete\"],\"description\":\"Master-data operation (required); merge and delete require approval and have no effect until EP-09.\"},\"contactId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target contact (required for update/addAlias/removeAlias/merge=source/delete).\"},\"name\":{\"type\":\"string\",\"description\":\"Contact name (required for create; optional overwrite for update).\"},\"contactType\":{\"type\":\"string\",\"enum\":[\"Self\",\"Bank\",\"Person\",\"Organization\",\"Other\"],\"description\":\"Contact type (required for create; optional overwrite for update). Self cannot be set.\"},\"categoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Contact-category assignment; explicit null clears it on update.\"},\"description\":{\"type\":\"string\",\"description\":\"Free-text description; explicit null clears it on update.\"},\"isPaymentIntermediary\":{\"type\":\"boolean\",\"description\":\"Payment-intermediary flag; explicit null clears it on update.\"},\"pattern\":{\"type\":\"string\",\"description\":\"Alias pattern (required for addAlias).\"},\"aliasId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Alias id (required for removeAlias).\"},\"targetContactId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Merge target contact (required for merge).\"},\"mergePreference\":{\"type\":\"string\",\"enum\":[\"DestinationFirst\",\"SourceFirst\"],\"description\":\"Merge field preference (default DestinationFirst).\"}},\"required\":[\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Creates and updates contacts (name, contactType, categoryId, description, isPaymentIntermediary) and maintains alias patterns (addAlias/removeAlias). The merge (irreversible: alias transfer, reference re-pointing, source deletion) and delete operations require approval and answer Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Kontakte, Erstellung/Änderung/Vervollständigung), (Kontaktgruppen, Lesen); merge/delete additionally (Kontakte, Löschung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Contacts, McpRightKind.Create),
        new McpToolScope(McpDataType.Contacts, McpRightKind.Modify),
        new McpToolScope(McpDataType.Contacts, McpRightKind.Complete),
        new McpToolScope(McpDataType.ContactCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_contacts_manage</c> (F-0801): adapts <see cref="IContactService"/>.
/// Update merges the provided fields over the current state so unspecified fields are
/// preserved; an explicit JSON null clears <c>categoryId</c>/<c>description</c>/
/// <c>isPaymentIntermediary</c>. Category references resolve owner-scoped via
/// <see cref="IContactCategoryService"/>; the record-level scope filter (filtered
/// individual profiles) binds the contact's category. Each effect commits atomically
/// with its staged <c>DataChange</c> record incl. field deltas (F-0301). Merge/delete
/// answer the structured approval-required result without any service call.
/// </summary>
public sealed class FmContactsManageHandler : IMcpToolHandler
{
    private const string EntityType = "Contact";

    private readonly IContactService _contacts;
    private readonly IContactCategoryService _categories;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmContactsManageHandler(
        IContactService contacts,
        IContactCategoryService categories,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _contacts = contacts;
        _categories = categories;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmContactsManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var operation = McpToolArguments.OptionalString(arguments, "operation", 30)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        return operation switch
        {
            "create" => await CreateAsync(arguments, owner, context, ct),
            "update" => await UpdateAsync(arguments, owner, context, ct),
            "addAlias" => await AddAliasAsync(arguments, owner, context, ct),
            "removeAlias" => await RemoveAliasAsync(arguments, owner, context, ct),
            "merge" => await MergeAsync(arguments, owner, context, ct),
            "delete" => await DeleteAsync(arguments, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of create|update|addAlias|removeAlias|merge|delete.")
        };
    }

    // ------------------------------------------------------------------
    // create
    // ------------------------------------------------------------------

    private async Task<object> CreateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        var type = McpToolArguments.OptionalEnum<ContactType>(arguments, "contactType")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contactType' is required for create.");
        var categoryId = McpToolArguments.OptionalGuid(arguments, "categoryId");
        var description = McpToolArguments.OptionalString(arguments, "description", McpMasterDataGuards.MaxDescriptionLength);
        var intermediary = McpToolArguments.OptionalBool(arguments, "isPaymentIntermediary");

        if (type == ContactType.Self)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Creating a contact of type 'Self' is not allowed.");
        }
        if (categoryId.HasValue)
        {
            await McpMasterDataGuards.RequireContactCategoryAsync(_categories, categoryId.Value, owner, ct);
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Contacts, McpRightKind.Create,
            new McpRecordContext { ContactCategoryId = categoryId }, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, Guid.Empty, "Created", null, $"{type}|{name}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "ContactCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "create", name, type = type.ToString(), categoryId }), ct);
        ContactDto created;
        try
        {
            created = await _contacts.CreateAsync(owner, name, type, categoryId, description, intermediary, ct);
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
        return new { operation = "create", applied = true, contact = Result(created) };
    }

    // ------------------------------------------------------------------
    // update — merge provided fields over the current values
    // ------------------------------------------------------------------

    private async Task<object> UpdateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var contactId = McpToolArguments.RequiredGuid(arguments, "contactId");
        var existing = await _contacts.GetAsync(contactId, owner, ct);
        if (existing is null)
        {
            // 404 semantics — a foreign contact behaves like a missing one.
            return new { operation = "update", contact = (object?)null };
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Contacts, McpRightKind.Modify,
            new McpRecordContext { ContactCategoryId = existing.CategoryId }, ct);

        var effName = McpToolArguments.Has(arguments, "name")
            ? McpMasterDataGuards.RequiredName(arguments)
            : existing.Name;
        var effType = McpToolArguments.Has(arguments, "contactType")
            ? McpToolArguments.OptionalEnum<ContactType>(arguments, "contactType")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contactType' must not be null.")
            : existing.Type;
        var effCategory = McpToolArguments.Has(arguments, "categoryId")
            ? McpToolArguments.OptionalGuid(arguments, "categoryId")
            : existing.CategoryId;
        var effDescription = McpToolArguments.Has(arguments, "description")
            ? McpToolArguments.OptionalString(arguments, "description", McpMasterDataGuards.MaxDescriptionLength)
            : existing.Description;
        var effIntermediary = McpToolArguments.Has(arguments, "isPaymentIntermediary")
            ? McpToolArguments.OptionalBool(arguments, "isPaymentIntermediary")
            : existing.IsPaymentIntermediary;

        if (existing.Type == ContactType.Self && effType != ContactType.Self)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Changing the type of a 'Self' contact is not allowed.");
        }
        if (effType == ContactType.Self && existing.Type != ContactType.Self)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Changing a contact's type to 'Self' is not allowed.");
        }
        if (effCategory.HasValue)
        {
            await McpMasterDataGuards.RequireContactCategoryAsync(_categories, effCategory.Value, owner, ct);
            // A filtered write profile must also cover the target category.
            await McpMasterDataGuards.RequireRecordWritableAsync(
                _recordFilter, context.Access, McpDataType.Contacts, McpRightKind.Modify,
                new McpRecordContext { ContactCategoryId = effCategory }, ct);
        }

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityType, contactId, "Name", existing.Name, effName);
        McpMasterDataGuards.AddChange(changes, EntityType, contactId, "ContactType", existing.Type, effType);
        McpMasterDataGuards.AddChange(changes, EntityType, contactId, "CategoryId", existing.CategoryId, effCategory);
        McpMasterDataGuards.AddChange(changes, EntityType, contactId, "Description", existing.Description, effDescription);
        McpMasterDataGuards.AddChange(changes, EntityType, contactId, "IsPaymentIntermediary", existing.IsPaymentIntermediary, effIntermediary);
        if (changes.Count == 0)
        {
            return new { operation = "update", applied = false, reason = "unchanged", contact = Result(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "ContactUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "update", contactId }), ct);
        ContactDto? updated;
        try
        {
            updated = await _contacts.UpdateAsync(contactId, owner, effName, effType, effCategory, effDescription, effIntermediary, ct);
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
            throw new McpToolException("Err_Mcp_InvalidState", "The contact could not be updated.");
        }
        return new
        {
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            contact = Result(updated)
        };
    }

    // ------------------------------------------------------------------
    // addAlias / removeAlias
    // ------------------------------------------------------------------

    private async Task<object> AddAliasAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var contactId = McpToolArguments.RequiredGuid(arguments, "contactId");
        var pattern = McpToolArguments.OptionalString(arguments, "pattern", McpMasterDataGuards.MaxAliasPatternLength);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'pattern' is required for addAlias.");
        }

        var existing = await _contacts.GetAsync(contactId, owner, ct);
        if (existing is null)
        {
            return new { operation = "addAlias", contact = (object?)null };
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Contacts, McpRightKind.Complete,
            new McpRecordContext { ContactCategoryId = existing.CategoryId }, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, contactId, "AliasAdded", null, pattern)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "ContactAliasAdded", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "addAlias", contactId }), ct);
        try
        {
            await _contacts.AddAliasAsync(contactId, owner, pattern, ct);
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
        return new
        {
            operation = "addAlias",
            applied = true,
            contactId,
            untrusted_data = new { pattern }
        };
    }

    private async Task<object> RemoveAliasAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var contactId = McpToolArguments.RequiredGuid(arguments, "contactId");
        var aliasId = McpToolArguments.RequiredGuid(arguments, "aliasId");

        var existing = await _contacts.GetAsync(contactId, owner, ct);
        if (existing is null)
        {
            return new { operation = "removeAlias", contact = (object?)null };
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Contacts, McpRightKind.Modify,
            new McpRecordContext { ContactCategoryId = existing.CategoryId }, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, contactId, "AliasRemoved", aliasId.ToString(), null)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "ContactAliasRemoved", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "removeAlias", contactId, aliasId }), ct);
        try
        {
            await _contacts.DeleteAliasAsync(contactId, owner, aliasId, ct);
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
        return new { operation = "removeAlias", applied = true, contactId, aliasId };
    }

    // ------------------------------------------------------------------
    // merge / delete — genehmigungspflichtig (approval-konzept §2.2, no effect until EP-09)
    // ------------------------------------------------------------------

    private async Task<object> MergeAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var contactId = McpToolArguments.RequiredGuid(arguments, "contactId");
        var targetContactId = McpToolArguments.RequiredGuid(arguments, "targetContactId");
        var preference = McpToolArguments.OptionalEnum<MergePreference>(arguments, "mergePreference")
            ?? MergePreference.DestinationFirst;
        if (contactId == targetContactId)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Source and target contact must differ.");
        }
        var existing = await _contacts.GetAsync(contactId, owner, ct);
        if (existing is null)
        {
            return new { operation = "merge", contact = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Contacts, "merge",
            "Merges the source contact into the target contact: transfers alias patterns, " +
            "re-points all referencing records (draft entries, postings, budgets) and deletes " +
            "the source contact. The merge is irreversible.",
            new { sourceContactId = contactId, targetContactId, mergePreference = preference.ToString() }, ct);
    }

    private async Task<object> DeleteAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var contactId = McpToolArguments.RequiredGuid(arguments, "contactId");
        var existing = await _contacts.GetAsync(contactId, owner, ct);
        if (existing is null)
        {
            return new { operation = "delete", contact = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Contacts, "delete",
            "Deletes the contact including its alias patterns. Referencing records lose " +
            "their contact link; the deletion is final.",
            new { contactId }, ct);
    }

    private static object Result(ContactDto c)
        => new
        {
            id = c.Id,
            contactType = c.Type.ToString(),
            categoryId = c.CategoryId,
            isPaymentIntermediary = c.IsPaymentIntermediary,
            symbolAttachmentId = c.SymbolAttachmentId,
            untrusted_data = new { name = c.Name, description = c.Description }
        };
}
