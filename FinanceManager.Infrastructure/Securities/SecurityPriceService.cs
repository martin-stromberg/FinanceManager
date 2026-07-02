using FinanceManager.Application.Securities;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Implementation of <see cref="ISecurityPriceService"/> that persists and queries
/// security price records using the application's <c>AppDbContext</c>.
/// </summary>
/// <remarks>
/// This service validates ownership of a security before creating or listing prices.
/// All methods are asynchronous and honour the provided <see cref="CancellationToken"/>.
/// </remarks>
public class SecurityPriceService : ISecurityPriceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SecurityPriceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPriceService"/> class.
    /// </summary>
    /// <param name="db">The application's database context used to persist and query security prices.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="db"/> is <c>null</c>.</exception>
    public SecurityPriceService(AppDbContext db, ILogger<SecurityPriceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new security price record for the specified security and date.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns the security.</param>
    /// <param name="securityId">The identifier of the security to which the price belongs.</param>
    /// <param name="date">The date for which the closing price applies (date portion used).</param>
    /// <param name="close">The closing price value for the given date.</param>
    /// <param name="ct">CancellationToken to cancel the operation.</param>
    /// <returns>A task that completes once the price record has been persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when the specified security does not exist or is not owned by <paramref name="ownerUserId"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="close"/> is negative or otherwise invalid.</exception>
    public async Task CreateAsync(Guid ownerUserId, Guid securityId, DateTime date, decimal close, CancellationToken ct)
    {
        _logger.LogInformation("Creating security price for {SecurityId} on {Date}", securityId, date.Date);
        // ensure security belongs to user
        var owned = await _db.Securities.AsNoTracking().AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (!owned) throw new ArgumentException("Security not found or not owned by user");

        if (close < 0m) throw new ArgumentOutOfRangeException(nameof(close), "Close price must not be negative.");

        var entity = new FinanceManager.Domain.Securities.SecurityPrice(securityId, date, close);
        _db.SecurityPrices.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Lists historical security prices for the specified security ordered by date descending.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns the security.</param>
    /// <param name="securityId">The identifier of the security whose prices to list.</param>
    /// <param name="skip">Number of records to skip for paging (may be zero).</param>
    /// <param name="take">Maximum number of records to return.</param>
    /// <param name="ct">CancellationToken to cancel the operation.</param>
    /// <returns>
    /// A task that returns a read-only list of <see cref="SecurityPriceDto"/> instances.
    /// The list is ordered by date descending (most recent first). If the security is not owned by the
    /// provided <paramref name="ownerUserId"/>, an empty list is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="skip"/> or <paramref name="take"/> is negative.</exception>
    public async Task<IReadOnlyList<SecurityPriceDto>> ListAsync(Guid ownerUserId, Guid securityId, int skip, int take, CancellationToken ct)
    {
        _logger.LogInformation("Listing security prices for {SecurityId} (skip={Skip}, take={Take})", securityId, skip, take);
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be zero or positive.");
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take), "Take must be greater than zero.");

        var owned = await _db.Securities.AsNoTracking().AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (!owned) return Array.Empty<SecurityPriceDto>();

        var q = _db.SecurityPrices.AsNoTracking().Where(p => p.SecurityId == securityId).OrderByDescending(p => p.Date).Skip(skip).Take(take);
        var list = await q.Select(p => new SecurityPriceDto(p.Date, p.Close)).ToListAsync(ct);
        return list;
    }

    /// <summary>
    /// Gets the latest date for which there is a security price record for the specified security.
    /// </summary>
    /// <param name="ownerUserId">The identifier of the user who owns the security.</param>
    /// <param name="securityId">The identifier of the security whose latest price date to retrieve.</param>
    /// <param name="ct">CancellationToken to cancel the operation.</param>
    /// <returns>
    /// A task that returns the latest date as a nullable <see cref="DateTime"/>.
    /// Returns <c>null</c> if the security is not owned by the user or if there are no price records.
    /// </returns>
    public async Task<DateTime?> GetLatestDateAsync(Guid ownerUserId, Guid securityId, CancellationToken ct)
    {
        _logger.LogInformation("Getting latest security price date for {SecurityId}", securityId);
        var owned = await _db.Securities.AsNoTracking().AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (!owned) return null;
        var last = await _db.SecurityPrices.AsNoTracking().Where(p => p.SecurityId == securityId).OrderByDescending(p => p.Date).Select(p => p.Date).FirstOrDefaultAsync(ct);
        return last == default ? null : last;
    }

    /// <summary>
    /// Upserts a batch of daily prices for one security.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="securityId">Target security identifier.</param>
    /// <param name="items">Daily price items to upsert. Duplicate days are resolved as last item wins.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Import-like counters for inserted, updated and unchanged prices.</returns>
    public async Task<SecurityPriceImportResultDto> UpsertDailyPricesAsync(
        Guid ownerUserId,
        Guid securityId,
        IReadOnlyList<SecurityPriceImportItem> items,
        CancellationToken ct)
    {
        _logger.LogInformation("Upserting {ItemCount} security prices for {SecurityId}", items?.Count ?? 0, securityId);

        ArgumentNullException.ThrowIfNull(items);

        var owned = await _db.Securities.AsNoTracking().AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (!owned)
        {
            throw new ArgumentException("Security not found or not owned by user");
        }

        if (items.Count == 0)
        {
            return new SecurityPriceImportResultDto(0, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>());
        }

        var deduplicated = new Dictionary<DateTime, decimal>();
        foreach (var item in items)
        {
            if (item.Close < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(items), "Close price must not be negative.");
            }

            deduplicated[item.Date.Date] = item.Close;
        }

        var dates = deduplicated.Keys.ToList();
        var existingByDate = await _db.SecurityPrices
            .Where(x => x.SecurityId == securityId && dates.Contains(x.Date))
            .ToDictionaryAsync(x => x.Date, x => x, ct);

        var inserted = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var kvp in deduplicated)
        {
            if (existingByDate.TryGetValue(kvp.Key, out var existing))
            {
                if (existing.Close != kvp.Value)
                {
                    _db.Entry(existing).Property(x => x.Close).CurrentValue = kvp.Value;
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }
            else
            {
                _db.SecurityPrices.Add(new FinanceManager.Domain.Securities.SecurityPrice(securityId, kvp.Key, kvp.Value));
                inserted++;
            }
        }

        if (inserted > 0 || updated > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new SecurityPriceImportResultDto(inserted, updated, unchanged, 0, Array.Empty<SecurityPriceImportErrorDto>());
    }

    /// <summary>
    /// Sets a short price error message on the given security. This marks the security as having
    /// a price lookup error (for example invalid external symbol) so that automated backfills
    /// can skip it until the error is cleared.
    /// </summary>
    /// <param name="ownerUserId">The owner of the security.</param>
    /// <param name="securityId">The security identifier.</param>
    /// <param name="message">A short error message to record. May be <c>null</c> or empty to clear the error.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the operation has been applied.</returns>
    /// <exception cref="ArgumentException">Thrown when the security does not exist or is not owned by <paramref name="ownerUserId"/>.</exception>
    public async Task SetPriceErrorAsync(Guid ownerUserId, Guid securityId, string message, CancellationToken ct)
    {
        _logger.LogInformation("Setting security price error for {SecurityId}", securityId);
        // ensure security belongs to user
        var entity = await _db.Securities.FirstOrDefaultAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (entity == null) throw new ArgumentException("Security not found or not owned by user");

        // Domain method to set price error message on entity
        entity.SetPriceError(message ?? string.Empty);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Clears the stored price error state for the given security.
    /// </summary>
    /// <param name="ownerUserId">The owner of the security.</param>
    /// <param name="securityId">The security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the operation has been applied.</returns>
    /// <exception cref="ArgumentException">Thrown when the security does not exist or is not owned by <paramref name="ownerUserId"/>.</exception>
    public async Task ClearPriceErrorAsync(Guid ownerUserId, Guid securityId, CancellationToken ct)
    {
        _logger.LogInformation("Clearing security price error for {SecurityId}", securityId);
        var entity = await _db.Securities.FirstOrDefaultAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (entity == null) throw new ArgumentException("Security not found or not owned by user");

        if (!entity.HasPriceError)
        {
            return;
        }

        entity.ClearPriceError();
        await _db.SaveChangesAsync(ct);
    }
}
