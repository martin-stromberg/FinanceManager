using FinanceManager.Domain.Attachments;

namespace FinanceManager.Application.Attachments;

/// <summary>
/// Service for managing attachments (upload, list, download, update, reassign and delete) for a user.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Uploads a file attachment for the specified entity.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="kind">Kind of entity the attachment belongs to.</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="content">Stream with file content.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created <see cref="AttachmentDto"/>.</returns>
    Task<AttachmentDto> UploadAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Stream content, string fileName, string contentType, Guid? categoryId, CancellationToken ct);

    /// <summary>
    /// Uploads a file attachment with an explicit role.
    /// </summary>
    /// <remarks>Same as <see cref="UploadAsync(Guid,AttachmentEntityKind,Guid,Stream,string,string,Guid?,CancellationToken)"/> but with a role parameter.</remarks>
    Task<AttachmentDto> UploadAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Stream content, string fileName, string contentType, Guid? categoryId, AttachmentRole role, CancellationToken ct);

    /// <summary>
    /// Creates a URL attachment for an entity.
    /// </summary>
    Task<AttachmentDto> CreateUrlAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, string url, string? fileName, Guid? categoryId, CancellationToken ct);

    /// <summary>
    /// Lists attachments for an entity with paging and optional filters.
    /// </summary>
    Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, int skip, int take, Guid? categoryId, bool? isUrl, string? q, CancellationToken ct);

    /// <summary>
    /// Counts attachments for an entity matching optional filters.
    /// </summary>
    Task<int> CountAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Guid? categoryId, bool? isUrl, string? q, CancellationToken ct);

    /// <summary>
    /// Downloads an attachment if available to the owner.
    /// </summary>
    /// <returns>Tuple (Content stream, FileName, ContentType) or null when not found.</returns>
    Task<(Stream Content, string FileName, string ContentType)?> DownloadAsync(Guid ownerUserId, Guid attachmentId, CancellationToken ct);

    /// <summary>
    /// Deletes an attachment owned by the user.
    /// </summary>
    Task<bool> DeleteAsync(Guid ownerUserId, Guid attachmentId, CancellationToken ct);

    /// <summary>
    /// Updates the category of an attachment.
    /// </summary>
    Task<bool> UpdateCategoryAsync(Guid ownerUserId, Guid attachmentId, Guid? categoryId, CancellationToken ct);

    /// <summary>
    /// Updates core properties of an attachment such as filename and category.
    /// </summary>
    Task<bool> UpdateCoreAsync(Guid ownerUserId, Guid attachmentId, string? fileName, Guid? categoryId, CancellationToken ct);

    /// <summary>
    /// Reassigns all attachments from one entity to another.
    /// </summary>
    Task ReassignAsync(AttachmentEntityKind fromKind, Guid fromId, AttachmentEntityKind toKind, Guid toId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new referencing attachment entry pointing to an existing master attachment.
    /// </summary>
    Task<AttachmentDto> CreateReferenceAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Guid masterAttachmentId, CancellationToken ct);
}
