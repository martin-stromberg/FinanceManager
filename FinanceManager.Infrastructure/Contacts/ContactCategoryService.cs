using FinanceManager.Application.Contacts;
using FinanceManager.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contacts;

/// <summary>
/// Service for managing contact categories (CRUD operations).
/// Implements <see cref="IContactCategoryService"/> and performs basic validation and persistence logic.
/// </summary>
public sealed class ContactCategoryService : IContactCategoryService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCategoryService"/> class.
    /// </summary>
    /// <param name="db">The application database context used for persistence.</param>
    public ContactCategoryService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Returns all contact categories for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier to scope the categories.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="ContactCategoryDto"/> for the owner.</returns>
    public async Task<IReadOnlyList<ContactCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct)
    {
        return await _db.Set<ContactCategory>().AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId)
            .OrderBy(c => c.Name)
            .Select(c => new ContactCategoryDto(c.Id, c.Name, c.SymbolAttachmentId))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Creates a new contact category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Name of the category. Must be unique for the owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="ContactCategoryDto"/> representing the persisted category.</returns>
    /// <exception cref="ArgumentException">Thrown when a category with the same name already exists for the owner.</exception>
    public async Task<ContactCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct)
    {
        var exists = await _db.Set<ContactCategory>()
            .AnyAsync(c => c.OwnerUserId == ownerUserId && c.Name == name, ct);
        if (exists)
        {
            throw new ArgumentException("Category name already exists.");
        }

        var cat = new ContactCategory(ownerUserId, name);
        _db.Add(cat);
        await _db.SaveChangesAsync(ct);
        return new ContactCategoryDto(cat.Id, cat.Name, cat.SymbolAttachmentId);
    }

    /// <summary>
    /// Sets or clears the symbol attachment for a contact category.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="attachmentId">Attachment identifier to set, or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the operation has finished.</returns>
    /// <exception cref="ArgumentException">Thrown when the category cannot be found for the specified id and owner.</exception>
    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var cat = await _db.Set<ContactCategory>()
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (cat == null) throw new ArgumentException("Category not found", nameof(id));
        cat.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Retrieves a contact category by id for the given owner.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mapped <see cref="ContactCategoryDto"/>, or <c>null</c> when not found.</returns>
    public async Task<ContactCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var c = await _db.Set<ContactCategory>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == ownerUserId, ct);
        if (c == null) return null;
        return new ContactCategoryDto(c.Id, c.Name, c.SymbolAttachmentId);
    }

    /// <summary>
    /// Updates the name of an existing contact category.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">New name for the category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the update has been applied.</returns>
    /// <exception cref="ArgumentException">Thrown when the category cannot be found for the specified id and owner.</exception>
    public async Task UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct)
    {
        var c = await _db.Set<ContactCategory>()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == ownerUserId, ct);
        if (c == null) throw new ArgumentException("Category not found", nameof(id));
        c.Rename(name);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes a contact category.
    /// </summary>
    /// <param name="id">Category identifier.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the category has been removed.</returns>
    /// <exception cref="ArgumentException">Thrown when the category cannot be found for the specified id and owner.</exception>
    public async Task DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var c = await _db.Set<ContactCategory>()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == ownerUserId, ct);
        if (c == null) throw new ArgumentException("Category not found", nameof(id));
        _db.Remove(c);
        await _db.SaveChangesAsync(ct);
    }
}