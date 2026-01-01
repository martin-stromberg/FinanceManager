using FinanceManager.Application.Attachments;
using FinanceManager.Domain.Attachments;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Attachments;

/// <summary>
/// Service for managing attachment categories for a user.
/// Provides listing, creation, update and deletion operations and enforces ownership and system-category rules.
/// </summary>
public sealed class AttachmentCategoryService : IAttachmentCategoryService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentCategoryService"/> class.
    /// </summary>
    /// <param name="db">Database context used to query and persist attachment categories and attachments.</param>
    public AttachmentCategoryService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Lists attachment categories owned by the specified user.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier to scope categories.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="AttachmentCategoryDto"/> for the owner.</returns>
    public async Task<IReadOnlyList<AttachmentCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct)
        => await _db.AttachmentCategories.AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId)
            .OrderBy(c => c.Name)
            .Select(c => new AttachmentCategoryDto(c.Id, c.Name, c.IsSystem,
                _db.Attachments.AsNoTracking().Any(a => a.OwnerUserId == ownerUserId && a.CategoryId == c.Id)))
            .ToListAsync(ct);

    /// <summary>
    /// Creates a new non-system attachment category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Name of the new category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentCategoryDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when a category with the same name already exists for the owner.</exception>
    public async Task<AttachmentCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct)
        => await CreateAsync(ownerUserId, name, isSystem: false, ct);

    /// <summary>
    /// Creates a new attachment category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Name of the new category.</param>
    /// <param name="isSystem">Whether the category is a system category (cannot be deleted/renamed).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentCategoryDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when a category with the same name already exists for the owner.</exception>
    public async Task<AttachmentCategoryDto> CreateAsync(Guid ownerUserId, string name, bool isSystem, CancellationToken ct)
    {
        var exists = await _db.AttachmentCategories.AnyAsync(c => c.OwnerUserId == ownerUserId && c.Name == name, ct);
        if (exists) { throw new ArgumentException("Category name already exists"); }
        var cat = new AttachmentCategory(ownerUserId, name, isSystem);
        _db.AttachmentCategories.Add(cat);
        await _db.SaveChangesAsync(ct);
        return new AttachmentCategoryDto(cat.Id, cat.Name, cat.IsSystem, false);
    }

    /// <summary>
    /// Deletes an attachment category if it is not in use and not a system category.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="id">Identifier of the category to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the category existed and was removed; otherwise <c>false</c> when not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the category is in use or when attempting to delete a system category.</exception>
    public async Task<bool> DeleteAsync(Guid ownerUserId, Guid id, CancellationToken ct)
    {
        var anyUse = await _db.Attachments.AsNoTracking().AnyAsync(a => a.OwnerUserId == ownerUserId && a.CategoryId == id, ct);
        if (anyUse) { throw new InvalidOperationException("Category is in use"); }
        var cat = await _db.AttachmentCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (cat == null) { return false; }
        if (cat.IsSystem) { throw new InvalidOperationException("System category cannot be deleted"); }
        _db.AttachmentCategories.Remove(cat);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Updates the name of an existing attachment category.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="name">New name for the category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="AttachmentCategoryDto"/>, or <c>null</c> when the category was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided name is invalid or already in use by another category.</exception>
    /// <exception cref="InvalidOperationException">Thrown when attempting to rename a system category.</exception>
    public async Task<AttachmentCategoryDto?> UpdateAsync(Guid ownerUserId, Guid id, string name, CancellationToken ct)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length < 2) { throw new ArgumentException("Name too short"); }
        var cat = await _db.AttachmentCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (cat == null) { return null; }
        if (cat.IsSystem)
        {
            throw new InvalidOperationException("System category cannot be renamed");
        }
        var exists = await _db.AttachmentCategories.AsNoTracking().AnyAsync(c => c.OwnerUserId == ownerUserId && c.Name == name && c.Id != id, ct);
        if (exists)
        {
            throw new ArgumentException("Category name already exists");
        }
        cat.Rename(name);
        await _db.SaveChangesAsync(ct);
        var inUse = await _db.Attachments.AsNoTracking().AnyAsync(a => a.OwnerUserId == ownerUserId && a.CategoryId == id, ct);
        return new AttachmentCategoryDto(cat.Id, cat.Name, cat.IsSystem, inUse);
    }
}
