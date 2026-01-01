using FinanceManager.Application.Securities;
using FinanceManager.Domain.Securities;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Service for managing security categories (CRUD and attachment handling).
/// </summary>
public sealed class SecurityCategoryService : ISecurityCategoryService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityCategoryService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to persist and query security categories and attachments.</param>
    public SecurityCategoryService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Lists all security categories for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier used to scope categories.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="SecurityCategoryDto"/> for the owner (may be empty).</returns>
    public async Task<IReadOnlyList<SecurityCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct)
    {
        return await _db.SecurityCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId)
            .OrderBy(c => c.Name)
            .Select(c => new SecurityCategoryDto { Id = c.Id, Name = c.Name, SymbolAttachmentId = c.SymbolAttachmentId })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Gets a security category by id for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the category.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The category DTO when found; otherwise <c>null</c>.</returns>
    public async Task<SecurityCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        return await _db.SecurityCategories.AsNoTracking()
            .Where(c => c.Id == id && c.OwnerUserId == ownerUserId)
            .Select(c => new SecurityCategoryDto { Id = c.Id, Name = c.Name, SymbolAttachmentId = c.SymbolAttachmentId })
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Creates a new security category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Name of the category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="SecurityCategoryDto"/>.</returns>
    /// <exception cref="DbUpdateException">May be thrown when persisting to the database fails (e.g. constraint violation).</exception>
    public async Task<SecurityCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct)
    {
        var category = new SecurityCategory(ownerUserId, name);
        _db.SecurityCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return new SecurityCategoryDto { Id = category.Id, Name = category.Name, SymbolAttachmentId = category.SymbolAttachmentId };
    }

    /// <summary>
    /// Updates the name of an existing security category.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="name">New name for the category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="SecurityCategoryDto"/>, or <c>null</c> when the category was not found.</returns>
    /// <exception cref="DbUpdateException">May be thrown when persisting changes to the database fails.</exception>
    public async Task<SecurityCategoryDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct)
    {
        var category = await _db.SecurityCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (category == null) return null;
        category.Rename(name);
        await _db.SaveChangesAsync(ct);
        return new SecurityCategoryDto { Id = category.Id, Name = category.Name, SymbolAttachmentId = category.SymbolAttachmentId };
    }

    /// <summary>
    /// Deletes a security category if it exists and belongs to the owner.
    /// </summary>
    /// <param name="id">Identifier of the category to delete.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the category existed and was removed; otherwise <c>false</c>.</returns>
    /// <exception cref="DbUpdateException">May be thrown when persisting changes to the database fails.</exception>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var category = await _db.SecurityCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (category == null) return false;
        _db.SecurityCategories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Sets or clears a symbol attachment reference for the security category.
    /// </summary>
    /// <param name="id">Identifier of the category.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="attachmentId">Attachment identifier to set, or <c>null</c> to clear the reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the change has been persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when the category or the referenced attachment is not found or not owned by the user.</exception>
    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var cat = await _db.SecurityCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (cat == null) throw new ArgumentException("Category not found", nameof(id));

        if (attachmentId.HasValue)
        {
            var exists = await _db.Attachments.AsNoTracking()
                .AnyAsync(a => a.Id == attachmentId.Value && a.OwnerUserId == ownerUserId, ct);
            if (!exists)
            {
                throw new ArgumentException("Attachment not found", nameof(attachmentId));
            }
        }

        cat.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }
}