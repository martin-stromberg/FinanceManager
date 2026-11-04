using FinanceManager.Application.Accounts;
using FinanceManager.Application.Attachments;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Attachments;
using System.Security.Cryptography;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_attachments_manage</c> (T28): uploads
/// attachments (<c>upload</c>, base64 content capped by the JSON-RPC envelope),
/// creates URL attachments (<c>createUrl</c>) and lightweight references
/// (<c>createReference</c>), corrects metadata/category (<c>update</c>,
/// <c>setCategory</c>) and re-links attachments between entities (<c>reassign</c>).
/// The destructive <c>delete</c> operation is genehmigungspflichtig and answers
/// <c>Err_Mcp_ApprovalRequired</c> without effect until the approval queue ships
/// (EP-09).
/// Scopes: (Anhänge, Erstellung/Änderung/Vervollständigung), (Anhangkategorien,
/// Lesen); delete additionally (Anhänge, Löschung) — evaluated inside the handler.
/// </summary>
public sealed class FmAttachmentsManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_attachments_manage";

    /// <summary>Creates the descriptor.</summary>
    public FmAttachmentsManageDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"upload\",\"createUrl\",\"createReference\",\"update\",\"setCategory\",\"reassign\",\"delete\"],\"description\":\"Attachment operation (required); delete requires approval and has no effect until EP-09.\"},\"attachmentId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target attachment (required for update/setCategory/delete).\"},\"entityKind\":{\"type\":\"string\",\"description\":\"Target entity kind (required for upload/createUrl/createReference); master-data kinds resolvable via the services: Contact, SavingsPlan, Security, Account, ContactCategory, SavingsPlanCategory, SecurityCategory, StatementDraft.\"},\"entityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target entity id (required for upload/createUrl/createReference).\"},\"fileName\":{\"type\":\"string\",\"description\":\"File name (required for upload; optional display name for createUrl; optional overwrite for update).\"},\"contentType\":{\"type\":\"string\",\"description\":\"MIME content type (required for upload).\"},\"contentBase64\":{\"type\":\"string\",\"description\":\"Base64 file content (required for upload; max 512 KiB raw — the JSON-RPC envelope caps transport).\"},\"url\":{\"type\":\"string\",\"description\":\"Absolute http(s) URL (required for createUrl).\"},\"categoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Attachment-category assignment; explicit null clears it on update/setCategory.\"},\"role\":{\"type\":\"string\",\"enum\":[\"Regular\",\"Symbol\"],\"description\":\"Attachment role for upload (default Regular).\"},\"masterAttachmentId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Existing attachment to reference (required for createReference).\"},\"fromEntityKind\":{\"type\":\"string\",\"description\":\"Source entity kind (required for reassign).\"},\"fromEntityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Source entity id (required for reassign).\"},\"toEntityKind\":{\"type\":\"string\",\"description\":\"Destination entity kind (required for reassign).\"},\"toEntityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Destination entity id (required for reassign).\"}},\"required\":[\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Manages attachments: upload (base64, ≤512 KiB raw), createUrl (absolute http(s) URL), createReference (link to an existing attachment), update (fileName/categoryId), setCategory, reassign (move all attachments between two owner-verified entities). The delete operation requires approval and answers Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Anhänge, Erstellung/Änderung/Vervollständigung), (Anhangkategorien, Lesen); delete additionally (Anhänge, Löschung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Attachments, McpRightKind.Create),
        new McpToolScope(McpDataType.Attachments, McpRightKind.Modify),
        new McpToolScope(McpDataType.Attachments, McpRightKind.Complete),
        new McpToolScope(McpDataType.AttachmentCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_attachments_manage</c> (F-0803): adapts
/// <see cref="IAttachmentService"/>. Every target entity (upload/url/reference target,
/// both reassign ends — the "Decke der Ziel-Entität") resolves owner-scoped via the
/// regular <c>I*Service</c> contracts of the master-data kinds; ledger-owned kinds
/// (postings, statement entries/imports, draft entries) are not accepted — their
/// attachments flow through the booking pipeline. The upload payload is staged as
/// metadata plus sha256 in the audit record — never binary content. Each effect
/// commits atomically with its staged <c>DataChange</c> record (F-0301); delete
/// answers approval-required without any service call.
/// </summary>
public sealed class FmAttachmentsManageHandler : IMcpToolHandler
{
    private const string EntityType = "Attachment";

    private readonly IAttachmentService _attachments;
    private readonly IAttachmentCategoryService _attachmentCategories;
    private readonly IContactService _contacts;
    private readonly IContactCategoryService _contactCategories;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISavingsPlanCategoryService _savingsPlanCategories;
    private readonly ISecurityService _securities;
    private readonly ISecurityCategoryService _securityCategories;
    private readonly IAccountService _accounts;
    private readonly IStatementDraftService _drafts;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmAttachmentsManageHandler(
        IAttachmentService attachments,
        IAttachmentCategoryService attachmentCategories,
        IContactService contacts,
        IContactCategoryService contactCategories,
        ISavingsPlanService savingsPlans,
        ISavingsPlanCategoryService savingsPlanCategories,
        ISecurityService securities,
        ISecurityCategoryService securityCategories,
        IAccountService accounts,
        IStatementDraftService drafts,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _attachments = attachments;
        _attachmentCategories = attachmentCategories;
        _contacts = contacts;
        _contactCategories = contactCategories;
        _savingsPlans = savingsPlans;
        _savingsPlanCategories = savingsPlanCategories;
        _securities = securities;
        _securityCategories = securityCategories;
        _accounts = accounts;
        _drafts = drafts;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmAttachmentsManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var operation = McpToolArguments.OptionalString(arguments, "operation", 30)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        return operation switch
        {
            "upload" => await UploadAsync(arguments, owner, context, ct),
            "createUrl" => await CreateUrlAsync(arguments, owner, context, ct),
            "createReference" => await CreateReferenceAsync(arguments, owner, context, ct),
            "update" => await UpdateAsync(arguments, owner, context, ct),
            "setCategory" => await SetCategoryAsync(arguments, owner, context, ct),
            "reassign" => await ReassignAsync(arguments, owner, context, ct),
            "delete" => await DeleteAsync(arguments, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of upload|createUrl|createReference|update|setCategory|reassign|delete.")
        };
    }

    // ------------------------------------------------------------------
    // upload / createUrl / createReference (Erstellung)
    // ------------------------------------------------------------------

    private async Task<object> UploadAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var (kind, entityId) = await RequireTargetAsync(arguments, owner, ct);
        var fileName = McpMasterDataGuards.RequiredName(arguments, "fileName", McpMasterDataGuards.MaxFileNameLength);
        var contentType = McpToolArguments.OptionalString(arguments, "contentType", McpMasterDataGuards.MaxContentTypeLength)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contentType' is required for upload.");
        var bytes = McpMasterDataGuards.DecodeUpload(McpToolArguments.OptionalString(arguments, "contentBase64", (int)(McpMasterDataGuards.MaxUploadBytes * 2)));
        var role = McpToolArguments.OptionalEnum<AttachmentRole>(arguments, "role") ?? AttachmentRole.Regular;
        var categoryId = await ResolveCategoryAsync(arguments, owner, ct);
        await RequireWritableAsync(context, McpRightKind.Create, categoryId, ct);

        // The audit payload carries metadata + sha256 — never the binary content.
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, Guid.Empty, "Created", null, $"{kind}:{entityId}|{fileName}|{bytes.Length}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AttachmentUploaded", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "upload", entityKind = kind.ToString(), entityId, fileName, contentType, sizeBytes = bytes.Length, sha256, categoryId }), ct);
        AttachmentDto created;
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            created = await _attachments.UploadAsync(owner, kind, entityId, ms, fileName, contentType, categoryId, role, ct);
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
        return new { operation = "upload", applied = true, sha256, attachment = Result(created) };
    }

    private async Task<object> CreateUrlAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var (kind, entityId) = await RequireTargetAsync(arguments, owner, ct);
        var url = McpMasterDataGuards.RequireHttpUrl(McpToolArguments.OptionalString(arguments, "url", McpMasterDataGuards.MaxUrlLength + 10));
        var fileName = McpToolArguments.OptionalString(arguments, "fileName", McpMasterDataGuards.MaxFileNameLength);
        var categoryId = await ResolveCategoryAsync(arguments, owner, ct);
        await RequireWritableAsync(context, McpRightKind.Create, categoryId, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, Guid.Empty, "Created", null, $"{kind}:{entityId}|url")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AttachmentUrlCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "createUrl", entityKind = kind.ToString(), entityId, url, categoryId }), ct);
        AttachmentDto created;
        try
        {
            created = await _attachments.CreateUrlAsync(owner, kind, entityId, url, fileName, categoryId, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new { operation = "createUrl", applied = true, attachment = Result(created) };
    }

    private async Task<object> CreateReferenceAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var (kind, entityId) = await RequireTargetAsync(arguments, owner, ct);
        var masterId = McpToolArguments.RequiredGuid(arguments, "masterAttachmentId");
        var master = await _attachments.GetAsync(owner, masterId, ct)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced master attachment does not exist or is not owned by the caller.");
        await RequireWritableAsync(context, McpRightKind.Create, master.CategoryId, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, Guid.Empty, "Created", null, $"{kind}:{entityId}|ref:{masterId}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AttachmentReferenceCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "createReference", entityKind = kind.ToString(), entityId, masterAttachmentId = masterId }), ct);
        AttachmentDto created;
        try
        {
            created = await _attachments.CreateReferenceAsync(owner, kind, entityId, masterId, ct);
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
        return new { operation = "createReference", applied = true, attachment = Result(created) };
    }

    // ------------------------------------------------------------------
    // update / setCategory (Änderung)
    // ------------------------------------------------------------------

    private async Task<object> UpdateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var attachmentId = McpToolArguments.RequiredGuid(arguments, "attachmentId");
        var existing = await _attachments.GetAsync(owner, attachmentId, ct);
        if (existing is null)
        {
            return new { operation = "update", attachment = (object?)null };
        }
        var effFileName = McpToolArguments.Has(arguments, "fileName")
            ? McpMasterDataGuards.RequiredName(arguments, "fileName", McpMasterDataGuards.MaxFileNameLength)
            : null;
        var effCategory = McpToolArguments.Has(arguments, "categoryId")
            ? McpToolArguments.OptionalGuid(arguments, "categoryId")
            : existing.CategoryId;
        if (McpToolArguments.Has(arguments, "categoryId") && effCategory.HasValue)
        {
            await McpMasterDataGuards.RequireAttachmentCategoryAsync(_attachmentCategories, effCategory.Value, owner, ct);
        }
        await RequireWritableAsync(context, McpRightKind.Modify, existing.CategoryId, ct);

        var changes = new List<McpAuditFieldChange>();
        if (effFileName is not null)
        {
            McpMasterDataGuards.AddChange(changes, EntityType, attachmentId, "FileName", existing.FileName, effFileName);
        }
        McpMasterDataGuards.AddChange(changes, EntityType, attachmentId, "CategoryId", existing.CategoryId, effCategory);
        if (changes.Count == 0)
        {
            return new { operation = "update", applied = false, reason = "unchanged", attachment = Result(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AttachmentUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "update", attachmentId }), ct);
        bool ok;
        try
        {
            ok = await _attachments.UpdateCoreAsync(owner, attachmentId, effFileName, effCategory, ct);
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
        if (!ok)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The attachment could not be updated.");
        }
        return new
        {
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            attachmentId
        };
    }

    private async Task<object> SetCategoryAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var attachmentId = McpToolArguments.RequiredGuid(arguments, "attachmentId");
        var existing = await _attachments.GetAsync(owner, attachmentId, ct);
        if (existing is null)
        {
            return new { operation = "setCategory", attachment = (object?)null };
        }
        var categoryId = McpToolArguments.Has(arguments, "categoryId")
            ? McpToolArguments.OptionalGuid(arguments, "categoryId")
            : throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'categoryId' is required for setCategory (explicit null clears).");
        if (categoryId.HasValue)
        {
            await McpMasterDataGuards.RequireAttachmentCategoryAsync(_attachmentCategories, categoryId.Value, owner, ct);
        }
        await RequireWritableAsync(context, McpRightKind.Modify, existing.CategoryId, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, attachmentId, "CategoryId",
                existing.CategoryId?.ToString(), categoryId?.ToString())
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AttachmentUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "setCategory", attachmentId, categoryId }), ct);
        bool ok;
        try
        {
            ok = await _attachments.UpdateCategoryAsync(owner, attachmentId, categoryId, ct);
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
        if (!ok)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The attachment category could not be updated.");
        }
        return new { operation = "setCategory", applied = true, attachmentId, categoryId };
    }

    // ------------------------------------------------------------------
    // reassign — move all attachments between two owner-verified entities
    // ------------------------------------------------------------------

    private async Task<object> ReassignAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var fromKind = McpToolArguments.OptionalEnum<AttachmentEntityKind>(arguments, "fromEntityKind")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'fromEntityKind' is required for reassign.");
        var fromId = McpToolArguments.RequiredGuid(arguments, "fromEntityId");
        var toKind = McpToolArguments.OptionalEnum<AttachmentEntityKind>(arguments, "toEntityKind")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'toEntityKind' is required for reassign.");
        var toId = McpToolArguments.RequiredGuid(arguments, "toEntityId");
        if (fromKind == toKind && fromId == toId)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Source and destination must differ.");
        }

        // Decke der Ziel-Entität: both ends must resolve owner-scoped before the bulk
        // move — ReassignAsync itself performs no entity existence check.
        await ResolveTargetAsync(fromKind, fromId, owner, ct);
        await ResolveTargetAsync(toKind, toId, owner, ct);
        await RequireWritableAsync(context, McpRightKind.Modify, null, ct);

        var moved = await _attachments.ListAsync(owner, fromKind, fromId, 0, McpToolArguments.MaxTake, null, null, null, ct);
        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, fromId, "Reassigned", $"{fromKind}:{fromId}", $"{toKind}:{toId}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AttachmentReassigned", ToolName, changes, moved.Count,
            JsonSerializer.Serialize(new { operation = "reassign", fromEntityKind = fromKind.ToString(), fromEntityId = fromId, toEntityKind = toKind.ToString(), toEntityId = toId, affected = moved.Count }), ct);
        try
        {
            await _attachments.ReassignAsync(fromKind, fromId, toKind, toId, owner, ct);
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
        return new { operation = "reassign", applied = true, moved = moved.Count };
    }

    // ------------------------------------------------------------------
    // delete — genehmigungspflichtig (approval-konzept §2.2, no effect until EP-09)
    // ------------------------------------------------------------------

    private async Task<object> DeleteAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var attachmentId = McpToolArguments.RequiredGuid(arguments, "attachmentId");
        var existing = await _attachments.GetAsync(owner, attachmentId, ct);
        if (McpMasterDataGuards.ThrowIfExecutionTargetGone(context, existing, "delete"))
        {
            return new { operation = "delete", attachment = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Attachments, "delete",
            "Deletes the attachment including its stored content and all references to " +
            "it; the deletion is final.",
            new { attachmentId }, ct,
            executeApproved: async ct2 =>
            {
                var changes = new List<McpAuditFieldChange>
                {
                    new(EntityType, attachmentId, "Deleted", existing!.FileName, null)
                };
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "AttachmentDeleted", ToolName, changes, 1,
                    JsonSerializer.Serialize(new { operation = "delete", attachmentId }), ct2);
                if (!await _attachments.DeleteAsync(owner, attachmentId, ct2))
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_TargetGone", "The attachment no longer exists.");
                }
                return (object)new { operation = "delete", applied = true, attachmentId };
            });
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    private async Task RequireWritableAsync(McpToolCallContext context, McpRightKind right, Guid? categoryId, CancellationToken ct)
        => await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Attachments, right,
            new McpRecordContext { AttachmentCategoryId = categoryId }, ct);

    private async Task<Guid?> ResolveCategoryAsync(JsonElement arguments, Guid owner, CancellationToken ct)
    {
        var categoryId = McpToolArguments.OptionalGuid(arguments, "categoryId");
        if (categoryId.HasValue)
        {
            await McpMasterDataGuards.RequireAttachmentCategoryAsync(_attachmentCategories, categoryId.Value, owner, ct);
        }
        return categoryId;
    }

    /// <summary>
    /// Resolves <c>entityKind</c>+<c>entityId</c> arguments and verifies the owner-scoped
    /// target exists — attachment targets are restricted to the master-data kinds
    /// resolvable through the regular service contracts (Decke der Ziel-Entität, F-0803).
    /// </summary>
    private async Task<(AttachmentEntityKind Kind, Guid EntityId)> RequireTargetAsync(
        JsonElement arguments, Guid owner, CancellationToken ct)
    {
        var kind = McpToolArguments.OptionalEnum<AttachmentEntityKind>(arguments, "entityKind")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'entityKind' is required.");
        var entityId = McpToolArguments.RequiredGuid(arguments, "entityId");
        await ResolveTargetAsync(kind, entityId, owner, ct);
        return (kind, entityId);
    }

    private async Task ResolveTargetAsync(AttachmentEntityKind kind, Guid entityId, Guid owner, CancellationToken ct)
    {
        var exists = kind switch
        {
            AttachmentEntityKind.Contact => await _contacts.GetAsync(entityId, owner, ct) is not null,
            AttachmentEntityKind.ContactCategory => await _contactCategories.GetAsync(entityId, owner, ct) is not null,
            AttachmentEntityKind.SavingsPlan => await _savingsPlans.GetAsync(entityId, owner, ct) is not null,
            AttachmentEntityKind.SavingsPlanCategory => await _savingsPlanCategories.GetAsync(entityId, owner, ct) is not null,
            AttachmentEntityKind.Security => await _securities.GetAsync(entityId, owner, ct) is not null,
            AttachmentEntityKind.SecurityCategory => await _securityCategories.GetAsync(entityId, owner, ct) is not null,
            AttachmentEntityKind.Account => await _accounts.GetAsync(entityId, owner, ct) is not null,
            AttachmentEntityKind.StatementDraft => await _drafts.GetDraftHeaderAsync(entityId, owner, ct) is not null,
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Entity kind '{kind}' is not supported by this tool — ledger-owned entities receive attachments through the booking pipeline.")
        };
        if (!exists)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "The referenced attachment target entity does not exist or is not owned by the caller.");
        }
    }

    private static object Result(AttachmentDto a)
        => new
        {
            id = a.Id,
            entityKind = ((AttachmentEntityKind)a.EntityKind).ToString(),
            entityId = a.EntityId,
            contentType = a.ContentType,
            sizeBytes = a.SizeBytes,
            categoryId = a.CategoryId,
            uploadedUtc = a.UploadedUtc,
            isUrl = a.IsUrl,
            role = a.Role,
            untrusted_data = new { fileName = a.FileName }
        };
}
