using FinanceManager.Application.Savings;
using FinanceManager.Domain.Savings;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure;

/// <summary>
/// Service providing CRUD operations for <see cref="SavingsPlanCategory"/> entities.
/// This implementation uses EF Core <see cref="AppDbContext"/> to persist changes.
/// </summary>
public sealed class SavingsPlanCategoryService : ISavingsPlanCategoryService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsPlanCategoryService"/> class.
    /// </summary>
    /// <param name="db">The application's database context.</param>
    public SavingsPlanCategoryService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Lists all savings plan categories for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier whose categories should be returned.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to a read-only list of <see cref="SavingsPlanCategoryDto"/> instances.</returns>
    public async Task<IReadOnlyList<SavingsPlanCategoryDto>> ListAsync(Guid ownerUserId, CancellationToken ct)
        => await _db.SavingsPlanCategories
            .Where(c => c.OwnerUserId == ownerUserId)
            .Select(c => new SavingsPlanCategoryDto { Id = c.Id, Name = c.Name, SymbolAttachmentId = c.SymbolAttachmentId })
            .ToListAsync(ct);

    /// <summary>
    /// Retrieves a single savings plan category by id for the given owner.
    /// </summary>
    /// <param name="id">Category identifier to retrieve.</param>
    /// <param name="ownerUserId">Owner user identifier for scoping the lookup.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that resolves to the matching <see cref="SavingsPlanCategoryDto"/>, or <c>null</c> when not found.
    /// </returns>
    public async Task<SavingsPlanCategoryDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
        => await _db.SavingsPlanCategories
            .Where(c => c.Id == id && c.OwnerUserId == ownerUserId)
            .Select(c => new SavingsPlanCategoryDto { Id = c.Id, Name = c.Name, SymbolAttachmentId = c.SymbolAttachmentId })
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Creates a new savings plan category for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier for the new category.</param>
    /// <param name="name">Display name of the category.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to the created <see cref="SavingsPlanCategoryDto"/>.</returns>
    public async Task<SavingsPlanCategoryDto> CreateAsync(Guid ownerUserId, string name, CancellationToken ct)
    {
        var category = new SavingsPlanCategory(ownerUserId, name);
        _db.SavingsPlanCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return new SavingsPlanCategoryDto { Id = category.Id, Name = category.Name, SymbolAttachmentId = category.SymbolAttachmentId };
    }

    /// <summary>
    /// Updates the name of an existing savings plan category for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="ownerUserId">Owner user identifier for scoping the operation.</param>
    /// <param name="name">New name for the category.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that resolves to the updated <see cref="SavingsPlanCategoryDto"/>, or <c>null</c> when the category was not found.
    /// </returns>
    public async Task<SavingsPlanCategoryDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, CancellationToken ct)
    {
        var category = await _db.SavingsPlanCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (category == null) return null;
        category.Rename(name);
        await _db.SaveChangesAsync(ct);
        return new SavingsPlanCategoryDto { Id = category.Id, Name = category.Name, SymbolAttachmentId = category.SymbolAttachmentId };
    }

    /// <summary>
    /// Deletes a savings plan category for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the category to delete.</param>
    /// <param name="ownerUserId">Owner user identifier for scoping the operation.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that resolves to <c>true</c> when the category was deleted; otherwise <c>false</c> when not found.
    /// </returns>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var category = await _db.SavingsPlanCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (category == null) return false;
        _db.SavingsPlanCategories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Sets or clears the symbol attachment reference for the specified category.
    /// </summary>
    /// <param name="id">Identifier of the category to update.</param>
    /// <param name="ownerUserId">Owner user identifier for scoping the operation.</param>
    /// <param name="attachmentId">Attachment GUID to set, or <c>null</c>/<see cref="Guid.Empty"/> to clear.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <exception cref="ArgumentException">Thrown when the category is not found for the specified id and owner.</exception>
    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var category = await _db.SavingsPlanCategories.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, ct);
        if (category == null) throw new ArgumentException("Category not found", nameof(id));
        category.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }
}