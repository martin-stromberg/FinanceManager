using FinanceManager.Application.Securities;
using FinanceManager.Domain.Attachments;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Service for managing securities (CRUD, archiving and attachment handling).
/// </summary>
public sealed class SecurityService : ISecurityService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query and persist securities and related entities.</param>
    public SecurityService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Lists securities for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier used to scope the query.</param>
    /// <param name="onlyActive">When <c>true</c> only active securities are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="SecurityDto"/> for the owner (may be empty).</returns>
    public async Task<IReadOnlyList<SecurityDto>> ListAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct)
    {
        var q = _db.Securities.AsNoTracking().Where(s => s.OwnerUserId == ownerUserId);
        if (onlyActive) { q = q.Where(s => s.IsActive); }

        var entities = await q.OrderBy(s => s.Name).ToListAsync(ct);
        var tasks = entities.Select(e => MapToDtoAsync(e, ct));
        var list = await Task.WhenAll(tasks);
        return list;
    }

    /// <summary>
    /// Gets a single security by id for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the security.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="SecurityDto"/> when found; otherwise <c>null</c>.</returns>
    public async Task<SecurityDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.Securities.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == ownerUserId, ct);
        if (entity == null) return null;
        return await MapToDtoAsync(entity, ct);
    }

    /// <summary>
    /// Creates a new security for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="name">Name of the security. Must be unique per owner.</param>
    /// <param name="identifier">External identifier for the security (e.g. ISIN).</param>
    /// <param name="description">Optional description.</param>
    /// <param name="alphaVantageCode">Optional AlphaVantage code for price lookup.</param>
    /// <param name="currencyCode">Currency code of the security.</param>
    /// <param name="categoryId">Optional category id; when supplied must belong to the owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="SecurityDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the category is invalid or the security name is not unique for the owner.</exception>
    public async Task<SecurityDto> CreateAsync(Guid ownerUserId, string name, string identifier, string? description, string? alphaVantageCode, string currencyCode, Guid? categoryId, CancellationToken ct)
    {
        if (categoryId != null)
        {
            bool catExists = await _db.SecurityCategories.AnyAsync(c => c.Id == categoryId && c.OwnerUserId == ownerUserId, ct);
            if (!catExists) { throw new ArgumentException("Invalid category", nameof(categoryId)); }
        }
        bool exists = await _db.Securities.AnyAsync(s => s.OwnerUserId == ownerUserId && s.Name == name, ct);
        if (exists) { throw new ArgumentException("Security name must be unique per user", nameof(name)); }

        var entity = new FinanceManager.Domain.Securities.Security(ownerUserId, name, identifier, description, alphaVantageCode, currencyCode, categoryId);
        _db.Securities.Add(entity);
        await _db.SaveChangesAsync(ct);
        return await MapToDtoAsync(entity, ct);
    }

    /// <summary>
    /// Updates an existing security.
    /// </summary>
    /// <param name="id">Identifier of the security to update.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="name">New name of the security.</param>
    /// <param name="identifier">New identifier.</param>
    /// <param name="description">New description.</param>
    /// <param name="alphaVantageCode">New AlphaVantage code.</param>
    /// <param name="currencyCode">New currency code.</param>
    /// <param name="categoryId">New category id (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="SecurityDto"/>, or <c>null</c> when the security was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the new name conflicts with another security of the same owner or when the category is invalid.</exception>
    public async Task<SecurityDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, string identifier, string? description, string? alphaVantageCode, string currencyCode, Guid? categoryId, CancellationToken ct)
    {
        var entity = await _db.Securities.FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == ownerUserId, ct);
        if (entity == null) { return null; }

        if (!string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _db.Securities.AnyAsync(s => s.OwnerUserId == ownerUserId && s.Name == name && s.Id != id, ct);
            if (exists) { throw new ArgumentException("Security name must be unique per user", nameof(name)); }
        }
        if (categoryId != null)
        {
            bool catExists = await _db.SecurityCategories.AnyAsync(c => c.Id == categoryId && c.OwnerUserId == ownerUserId, ct);
            if (!catExists) { throw new ArgumentException("Invalid category", nameof(categoryId)); }
        }

        entity.Update(name, identifier, description, alphaVantageCode, currencyCode, categoryId);
        await _db.SaveChangesAsync(ct);

        return await MapToDtoAsync(entity, ct);
    }

    /// <summary>
    /// Archives (marks as inactive) the specified security.
    /// </summary>
    /// <param name="id">Identifier of the security to archive.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the security was found and archived; otherwise <c>false</c> when not found or already inactive.</returns>
    public async Task<bool> ArchiveAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.Securities.FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == ownerUserId, ct);
        if (entity == null || !entity.IsActive) { return false; }
        entity.Archive();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Deletes a security if it exists and is archived.
    /// </summary>
    /// <param name="id">Identifier of the security to delete.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the security existed and was removed; otherwise <c>false</c>.</returns>
    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.Securities.FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == ownerUserId, ct);
        if (entity == null) { return false; }
        if (entity.IsActive) { return false; }

        // Delete attachments for this security
        await _db.Attachments
            .Where(a => a.OwnerUserId == ownerUserId && a.EntityKind == AttachmentEntityKind.Security && a.EntityId == entity.Id)
            .ExecuteDeleteAsync(ct);

        _db.Securities.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Counts securities for the owner, optionally filtering to active securities.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="onlyActive">When <c>true</c> only active securities are counted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of matching securities.</returns>
    public Task<int> CountAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct)
    {
        var q = _db.Securities.AsNoTracking().Where(s => s.OwnerUserId == ownerUserId);
        if (onlyActive) { q = q.Where(s => s.IsActive); }
        return q.CountAsync(ct);
    }

    /// <summary>
    /// Sets or clears a symbol attachment reference for the security.
    /// </summary>
    /// <param name="id">Identifier of the security.</param>
    /// <param name="ownerUserId">Owner user identifier used to validate ownership.</param>
    /// <param name="attachmentId">Attachment identifier to set, or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the change has been persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when the security is not found or not owned by the user.</exception>
    public async Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct)
    {
        var sec = await _db.Securities.FirstOrDefaultAsync(s => s.Id == id && s.OwnerUserId == ownerUserId, ct);
        if (sec == null) throw new ArgumentException("Security not found", nameof(id));
        sec.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }

    // Map a domain Security entity to its DTO representation.
    private async Task<SecurityDto> MapToDtoAsync(FinanceManager.Domain.Securities.Security s, CancellationToken ct)
    {
        string? categoryName = null;
        if (s.CategoryId != null)
        {
            categoryName = await _db.SecurityCategories.Where(c => c.Id == s.CategoryId).Select(c => c.Name).FirstOrDefaultAsync(ct);
        }

        return new SecurityDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Identifier = s.Identifier,
            AlphaVantageCode = s.AlphaVantageCode,
            CurrencyCode = s.CurrencyCode,
            CategoryId = s.CategoryId,
            CategoryName = categoryName,
            IsActive = s.IsActive,
            CreatedUtc = s.CreatedUtc,
            ArchivedUtc = s.ArchivedUtc,
            SymbolAttachmentId = s.SymbolAttachmentId,
            HasPriceError = s.HasPriceError
        };
    }
}
