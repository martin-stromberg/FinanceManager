using FinanceManager.Application.Attachments;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Mcp;
using System.Security.Cryptography;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_attachments_read</c> (T09): lists attachments of a
/// polymorphic target (entityKind + entityId) or reads one attachment's content. Content is
/// emitted exclusively as document data with declared <c>contentType</c> and <c>sha256</c>
/// inside the <c>untrusted_data</c> wrapper — never mixed with prose (sicherheitskonzept.md
/// §5.2). Scope: (Anhänge, Lesen).
/// </summary>
public sealed class FmAttachmentsReadDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_attachments_read";

    /// <summary>Creates the descriptor.</summary>
    public FmAttachmentsReadDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"attachmentId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Read one attachment incl. content; omitted = metadata listing (then entityKind + entityId are required).\"},\"entityKind\":{\"type\":\"string\",\"description\":\"AttachmentEntityKind name (e.g. 'Contact', 'Account', 'StatementEntry', 'Posting').\"},\"entityId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Id of the target entity.\"},\"categoryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional attachment category filter.\"},\"isUrl\":{\"type\":\"boolean\",\"description\":\"Optional URL-attachment filter.\"},\"q\":{\"type\":\"string\",\"description\":\"Optional file-name search text.\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Lists attachments of a target entity (metadata) or downloads one attachment with declared contentType, sha256 and untrusted_data marking. Scope: (Anhänge, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Attachments, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_attachments_read</c>: adapts <see cref="IAttachmentService"/>.
/// Attachment content is binary document data — the most injection-critical surface — and
/// is emitted base64-encoded with declared <c>contentType</c> and a freshly computed
/// <c>sha256</c> inside <c>untrusted_data</c>; oversized payloads are refused with a flag
/// instead of streamed. File names are untrusted as well. The Anhangkategorien filter
/// dimension applies per attachment via its category id.
/// </summary>
public sealed class FmAttachmentsReadHandler : IMcpToolHandler
{
    /// <summary>Maximum attachment payload emitted inline (10 MiB, matching the upload-size convention).</summary>
    internal const long MaxInlineContentBytes = 10L * 1024 * 1024;

    private readonly IAttachmentService _attachments;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmAttachmentsReadHandler(IAttachmentService attachments, McpRecordScopeFilter recordFilter)
    {
        _attachments = attachments;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmAttachmentsReadDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var attachmentId = McpToolArguments.OptionalGuid(arguments, "attachmentId");

        if (attachmentId.HasValue)
        {
            return await ReadSingleAsync(attachmentId.Value, context, ct);
        }

        var entityKind = McpToolArguments.OptionalEnum<AttachmentEntityKind>(arguments, "entityKind")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                "Either 'attachmentId' or 'entityKind' + 'entityId' is required.");
        var entityId = McpToolArguments.RequiredGuid(arguments, "entityId");
        var categoryId = McpToolArguments.OptionalGuid(arguments, "categoryId");
        var isUrl = McpToolArguments.OptionalBool(arguments, "isUrl");
        var q = McpToolArguments.OptionalString(arguments, "q");
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);

        var list = await _attachments.ListAsync(owner, entityKind, entityId, skip, take, categoryId, isUrl, q, ct);
        var count = await _attachments.CountAsync(owner, entityKind, entityId, categoryId, isUrl, q, ct);

        // Record-level filter (Anhangkategorien dimension via Attachment.CategoryId).
        var visible = await _recordFilter.ApplyAsync(
            context.Access, McpDataType.Attachments, list,
            a => new McpRecordContext { AttachmentCategoryId = a.CategoryId }, ct);

        return new
        {
            total = count,
            attachments = visible.Select(a => (object)new
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
            }).ToList()
        };
    }

    private async Task<object> ReadSingleAsync(Guid attachmentId, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var meta = await _attachments.GetAsync(owner, attachmentId, ct);
        if (meta is null)
        {
            // Foreign/non-existing attachments behave like non-existing ones (no
            // forbidden/not-found distinction).
            return new { attachment = (object?)null };
        }

        var visible = await _recordFilter.IsVisibleAsync(
            context.Access, McpDataType.Attachments,
            new McpRecordContext { AttachmentCategoryId = meta.CategoryId }, ct);
        if (!visible)
        {
            return new { attachment = (object?)null };
        }

        var download = await _attachments.DownloadAsync(owner, attachmentId, ct);
        if (download is null)
        {
            // Metadata exists but no binary content (URL attachment or unresolved reference).
            return new
            {
                attachment = (object)new
                {
                    id = meta.Id,
                    entityKind = ((AttachmentEntityKind)meta.EntityKind).ToString(),
                    entityId = meta.EntityId,
                    contentType = meta.ContentType,
                    sizeBytes = meta.SizeBytes,
                    categoryId = meta.CategoryId,
                    uploadedUtc = meta.UploadedUtc,
                    isUrl = meta.IsUrl,
                    contentOmitted = true,
                    reason = "no-binary-content",
                    untrusted_data = new { fileName = meta.FileName }
                }
            };
        }

        var (stream, fileName, contentType) = download.Value;
        byte[] bytes;
        await using (stream)
        {
            if (stream.Length > MaxInlineContentBytes)
            {
                return new
                {
                    attachment = (object)new
                    {
                        id = meta.Id,
                        entityKind = ((AttachmentEntityKind)meta.EntityKind).ToString(),
                        entityId = meta.EntityId,
                        contentType,
                        sizeBytes = stream.Length,
                        categoryId = meta.CategoryId,
                        contentOmitted = true,
                        reason = $"size-limit-exceeded (>{MaxInlineContentBytes} bytes)",
                        untrusted_data = new { fileName }
                    }
                };
            }
            bytes = new byte[stream.Length];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(offset), ct);
                if (read == 0)
                {
                    break;
                }
                offset += read;
            }
            if (offset < bytes.Length)
            {
                Array.Resize(ref bytes, offset);
            }
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new
        {
            attachment = (object)new
            {
                id = meta.Id,
                entityKind = ((AttachmentEntityKind)meta.EntityKind).ToString(),
                entityId = meta.EntityId,
                contentType,
                sizeBytes = (long)bytes.Length,
                sha256,
                categoryId = meta.CategoryId,
                uploadedUtc = meta.UploadedUtc,
                // The payload is document data, never an instruction: emitted base64-encoded
                // with declared content type inside the marked untrusted_data wrapper.
                untrusted_data = new
                {
                    fileName,
                    contentBase64 = Convert.ToBase64String(bytes)
                }
            }
        };
    }
}
