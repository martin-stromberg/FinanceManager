using FinanceManager.Application.Attachments;
using FinanceManager.Domain.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace FinanceManager.Infrastructure.Attachments;

/// <summary>
/// Service for storing and managing attachments in the application database.
/// Provides upload, listing, download, deletion and metadata operations.
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AttachmentService> _logger;
    private const int MaxTake = 200;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentService"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public AttachmentService(AppDbContext db, ILogger<AttachmentService> logger)
    {
        _db = db; _logger = logger;
    }

    /// <summary>
    /// Uploads binary content as an attachment and stores it in the database.
    /// This overload preserves backwards compatibility and assigns the <see cref="AttachmentRole.Regular"/> role.
    /// </summary>
    /// <param name="ownerUserId">Owner user id for scoping the attachment.</param>
    /// <param name="kind">Entity kind the attachment belongs to.</param>
    /// <param name="entityId">Identifier of the target entity instance.</param>
    /// <param name="content">Stream containing the file content. The stream will be read but not disposed by this method.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">MIME content type of the file.</param>
    /// <param name="categoryId">Optional attachment category id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentDto"/> describing the stored attachment.</returns>
    public Task<AttachmentDto> UploadAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Stream content, string fileName, string contentType, Guid? categoryId, CancellationToken ct)
        => UploadAsync(ownerUserId, kind, entityId, content, fileName, contentType, categoryId, AttachmentRole.Regular, ct);

    /// <summary>
    /// Uploads binary content as an attachment and stores it in the database with the specified role.
    /// </summary>
    /// <param name="ownerUserId">Owner user id for scoping the attachment.</param>
    /// <param name="kind">Entity kind the attachment belongs to.</param>
    /// <param name="entityId">Identifier of the target entity instance.</param>
    /// <param name="content">Stream containing the file content. The stream will be read but not disposed by this method.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">MIME content type of the file.</param>
    /// <param name="categoryId">Optional attachment category id.</param>
    /// <param name="role">Role of the attachment (regular, preview, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentDto"/> describing the stored attachment.</returns>
    public async Task<AttachmentDto> UploadAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Stream content, string fileName, string contentType, Guid? categoryId, AttachmentRole role, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var size = (long)bytes.Length;
        string sha = ComputeSha256(bytes);

        var entity = new Attachment(ownerUserId, kind, entityId, fileName, contentType, size, sha, categoryId, bytes, null, null, role);
        _db.Attachments.Add(entity);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Attachment uploaded {AttachmentId} kind={Kind} entity={EntityId} size={Size} role={Role}", entity.Id, kind, entityId, size, role);
        return Map(entity);
    }

    /// <summary>
    /// Creates a URL attachment that references an external resource instead of storing binary data.
    /// </summary>
    /// <param name="ownerUserId">Owner user id for scoping the attachment.</param>
    /// <param name="kind">Entity kind the attachment belongs to.</param>
    /// <param name="entityId">Identifier of the target entity instance.</param>
    /// <param name="url">URL pointing to the external resource.</param>
    /// <param name="fileName">Optional display file name; when null the URL path segment or the full URL is used.</param>
    /// <param name="categoryId">Optional attachment category id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentDto"/> describing the URL attachment.</returns>
    /// <exception cref="UriFormatException">If the provided url is not a valid absolute or relative URI.</exception>
    public async Task<AttachmentDto> CreateUrlAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, string url, string? fileName, Guid? categoryId, CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? new Uri(url).Segments.LastOrDefault() ?? url : fileName;
        var entity = new Attachment(ownerUserId, kind, entityId, name, "text/uri-list", 0, null, categoryId, null, url);
        _db.Attachments.Add(entity);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Attachment URL created {AttachmentId} kind={Kind} entity={EntityId} url={Url}", entity.Id, kind, entityId, url);
        return Map(entity);
    }

    /// <summary>
    /// Lists attachments for the specified owner/entity using the legacy overload (no filtering by category or url flag).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="kind">Entity kind.</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="skip">Number of items to skip for paging.</param>
    /// <param name="take">Number of items to take for paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="AttachmentDto"/> matching the query.</returns>
    public Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, int skip, int take, CancellationToken ct)
        => ListAsync(ownerUserId, kind, entityId, skip, take, categoryId: null, isUrl: null, q: null, ct);

    private IQueryable<Attachment> BuildQuery(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Guid? categoryId, bool? isUrl, string? q)
    {
        var qry = _db.Attachments.AsNoTracking()
            .Where(a => a.OwnerUserId == ownerUserId && a.EntityKind == kind && a.EntityId == entityId);
        if (categoryId.HasValue)
        {
            qry = qry.Where(a => a.CategoryId == categoryId.Value);
        }
        if (isUrl.HasValue)
        {
            qry = isUrl.Value ? qry.Where(a => a.Url != null) : qry.Where(a => a.Url == null);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            qry = qry.Where(a => a.FileName.Contains(term));
        }
        return qry;
    }

    /// <summary>
    /// Lists attachments for the specified owner/entity and supports filtering by category, url flag and filename query.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="kind">Entity kind.</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="skip">Number of items to skip for paging.</param>
    /// <param name="take">Number of items to take for paging (clamped to a maximum).</param>
    /// <param name="categoryId">Optional category id to filter attachments.</param>
    /// <param name="isUrl">Optional flag to filter only URL attachments (true) or only binary attachments (false); when null no filtering applied.</param>
    /// <param name="q">Optional search query matched against file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="AttachmentDto"/> matching the query.</returns>
    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, int skip, int take, Guid? categoryId, bool? isUrl, string? q, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, MaxTake);
        skip = Math.Max(0, skip);
        var qry = BuildQuery(ownerUserId, kind, entityId, categoryId, isUrl, q);
        return await qry
            // Stable ordering to make paging deterministic when many rows share the same timestamp
            .OrderByDescending(a => a.UploadedUtc)
            .ThenByDescending(a => a.Id)
            .Skip(skip)
            .Take(take)
            .Select(a => new AttachmentDto(a.Id, (short)a.EntityKind, a.EntityId, a.FileName, a.ContentType, a.SizeBytes, a.CategoryId, a.UploadedUtc, a.Url != null, (short)a.Role))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Counts attachments for the specified owner/entity with optional filters.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="kind">Entity kind.</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="categoryId">Optional category id filter.</param>
    /// <param name="isUrl">Optional flag to filter only URL attachments (true) or only binary attachments (false).</param>
    /// <param name="q">Optional filename search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of matching attachments.</returns>
    public async Task<int> CountAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Guid? categoryId, bool? isUrl, string? q, CancellationToken ct)
    {
        var qry = BuildQuery(ownerUserId, kind, entityId, categoryId, isUrl, q);
        return await qry.CountAsync(ct);
    }

    /// <summary>
    /// Downloads the binary content of an attachment. For reference attachments the master attachment is resolved.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="attachmentId">Attachment identifier to download.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple of <c>(Content, FileName, ContentType)</c> when binary content is available; otherwise <c>null</c> (for URL-only attachments or missing content).
    /// </returns>
    public async Task<(Stream Content, string FileName, string ContentType)?> DownloadAsync(Guid ownerUserId, Guid attachmentId, CancellationToken ct)
    {
        var a = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.OwnerUserId == ownerUserId, ct);
        if (a == null) { return null; }
        // If this is a reference-only attachment, resolve master
        if (a.Content == null && string.IsNullOrWhiteSpace(a.Url) && a.ReferenceAttachmentId.HasValue)
        {
            var master = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.ReferenceAttachmentId.Value && x.OwnerUserId == ownerUserId, ct);
            if (master == null || master.Content == null) { return null; }
            return (new MemoryStream(master.Content, writable: false), master.FileName, master.ContentType);
        }
        if (a.Content == null) { return null; }
        return (new MemoryStream(a.Content, writable: false), a.FileName, a.ContentType);
    }

    /// <summary>
    /// Deletes the specified attachment. If the attachment is a reference, the master attachment (and thereby all references) will be deleted.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="attachmentId">Attachment identifier to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if deletion succeeded or the attachment was found and removed; otherwise <c>false</c>.</returns>
    public async Task<bool> DeleteAsync(Guid ownerUserId, Guid attachmentId, CancellationToken ct)
    {
        var a = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.OwnerUserId == ownerUserId, ct);
        if (a == null) { return false; }

        // If the selected attachment is only a reference, delete the master instead (and thereby all references)
        var targetId = a.ReferenceAttachmentId ?? a.Id;
        Attachment? target;
        if (a.ReferenceAttachmentId.HasValue)
        {
            target = await _db.Attachments.FirstOrDefaultAsync(x => x.Id == targetId && x.OwnerUserId == ownerUserId, ct);
            if (target == null)
            {
                // Master is missing; fall back to deleting the reference itself
                _db.Attachments.Remove(a);
                await _db.SaveChangesAsync(ct);
                return true;
            }
        }
        else
        {
            target = a;
        }

        // Delete only the master; DB FK (OnDelete Cascade) removes references automatically
        _db.Attachments.Remove(target);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Updates the category of an attachment.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="categoryId">Category id to set, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the update was applied; otherwise <c>false</c> when the attachment was not found.</returns>
    public async Task<bool> UpdateCategoryAsync(Guid ownerUserId, Guid attachmentId, Guid? categoryId, CancellationToken ct)
    {
        var a = await _db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.OwnerUserId == ownerUserId, ct);
        if (a == null) { return false; }
        a.SetCategory(categoryId);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Updates core metadata of an attachment (file name and category) and propagates name changes to references.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="fileName">Optional new file name; when null the name is not changed.</param>
    /// <param name="categoryId">Optional category id to set, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the update was applied; otherwise <c>false</c> when the attachment was not found.</returns>
    public async Task<bool> UpdateCoreAsync(Guid ownerUserId, Guid attachmentId, string? fileName, Guid? categoryId, CancellationToken ct)
    {
        var a = await _db.Attachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.OwnerUserId == ownerUserId, ct);
        if (a == null) { return false; }
        if (!string.IsNullOrWhiteSpace(fileName) && !string.Equals(fileName, a.FileName, StringComparison.Ordinal))
        {
            a.Rename(fileName);
            // Propagate name change to referencing attachments to keep consistent display names
            var refs = await _db.Attachments.Where(x => x.ReferenceAttachmentId == a.Id && x.OwnerUserId == ownerUserId).ToListAsync(ct);
            foreach (var r in refs)
            {
                r.Rename(a.FileName);
            }
        }
        a.SetCategory(categoryId);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Reassigns attachments from one entity to another (used when committing drafts to accounts etc.).
    /// </summary>
    /// <param name="fromKind">Source entity kind.</param>
    /// <param name="fromId">Source entity id.</param>
    /// <param name="toKind">Destination entity kind.</param>
    /// <param name="toId">Destination entity id.</param>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ReassignAsync(AttachmentEntityKind fromKind, Guid fromId, AttachmentEntityKind toKind, Guid toId, Guid ownerUserId, CancellationToken ct)
    {
        await _db.Attachments
            .Where(a => a.OwnerUserId == ownerUserId && a.EntityKind == fromKind && a.EntityId == fromId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.EntityKind, toKind).SetProperty(a => a.EntityId, toId), ct);
    }

    /// <summary>
    /// Creates a lightweight reference attachment that points to a master attachment.
    /// The created reference does not store content itself but links to the master attachment id.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="kind">Entity kind for which the reference is created (usually Posting).</param>
    /// <param name="entityId">Entity id to associate the reference with.</param>
    /// <param name="masterAttachmentId">Identifier of the existing master attachment to reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentDto"/> for the reference.</returns>
    /// <exception cref="ArgumentException">Thrown when the master attachment cannot be found for the owner.</exception>
    public async Task<AttachmentDto> CreateReferenceAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, Guid masterAttachmentId, CancellationToken ct)
    {
        var master = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == masterAttachmentId && a.OwnerUserId == ownerUserId, ct);
        if (master == null) { throw new ArgumentException("Master attachment not found"); }
        var copy = new Attachment(ownerUserId, kind, entityId, master.FileName, master.ContentType, 0L, master.Sha256, master.CategoryId, null, null, master.Id);
        var entry = _db.Attachments.Add(copy);
        await _db.SaveChangesAsync(ct);
        return Map(copy);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static AttachmentDto Map(Attachment a)
        => new AttachmentDto(a.Id, (short)a.EntityKind, a.EntityId, a.FileName, a.ContentType, a.SizeBytes, a.CategoryId, a.UploadedUtc, a.Url != null, (short)a.Role);
}
