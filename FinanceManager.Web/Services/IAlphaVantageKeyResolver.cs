using FinanceManager.Application;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Web.Services;

/// <summary>
/// Resolves AlphaVantage API keys for users or a shared (admin-provided) key.
/// Implementations may look up per-user keys and fall back to a shared key when available.
/// </summary>
public interface IAlphaVantageKeyResolver
{
    /// <summary>
    /// Gets the AlphaVantage API key configured for the specified user.
    /// </summary>
    /// <param name="userId">The user identifier to resolve the key for.</param>
    /// <param name="ct">Cancellation token for the lookup operation.</param>
    /// <returns>
    /// The configured API key for the user when present; otherwise <c>null</c> when no per-user key exists.
    /// Implementations may still fall back to a shared key via <see cref="GetSharedAsync"/> when appropriate.
    /// </returns>
    /// <exception cref="System.Exception">Propagates exceptions thrown by the underlying data provider.</exception>
    Task<string?> GetForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Gets the shared AlphaVantage API key that administrators agreed to share with other users.
    /// </summary>
    /// <param name="ct">Cancellation token for the lookup operation.</param>
    /// <returns>The shared API key when configured; otherwise <c>null</c>.</returns>
    /// <exception cref="System.Exception">Propagates exceptions thrown by the underlying data provider.</exception>
    Task<string?> GetSharedAsync(CancellationToken ct);
}

/// <summary>
/// Default database-backed implementation of <see cref="IAlphaVantageKeyResolver"/> that reads keys from the application <see cref="AppDbContext"/> Users table.
/// </summary>
public sealed class AlphaVantageKeyResolver : IAlphaVantageKeyResolver
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaVantageKeyResolver"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query stored API keys.</param>
    public AlphaVantageKeyResolver(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves the AlphaVantage key configured for a specific user. If the user has no personal key configured
    /// this method will attempt to return a shared admin key via <see cref="GetSharedAsync"/>.
    /// </summary>
    /// <param name="userId">The identifier of the user to lookup the key for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The per-user API key when configured; otherwise the shared API key when available; or <c>null</c> when none is configured.
    /// </returns>
    /// <exception cref="System.Exception">Propagates exceptions thrown by the underlying data provider.</exception>
    public async Task<string?> GetForUserAsync(Guid userId, CancellationToken ct)
    {
        var key = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.AlphaVantageApiKey)
            .SingleOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(key)) return key;

        return await GetSharedAsync(ct);
    }

    /// <summary>
    /// Returns a shared AlphaVantage key provided by an administrator who opted to share their key.
    /// When multiple admins are sharing the method deterministically chooses one by ordering on username.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The shared API key when present; otherwise <c>null</c>.</returns>
    /// <exception cref="System.Exception">Propagates exceptions thrown by the underlying data provider.</exception>
    public async Task<string?> GetSharedAsync(CancellationToken ct)
    {
        return await _db.Users.AsNoTracking()
            .Where(u => u.IsAdmin && u.ShareAlphaVantageApiKey && u.AlphaVantageApiKey != null)
            .OrderBy(u => u.UserName) // deterministic choice — use mapped Identity property
            .Select(u => u.AlphaVantageApiKey!)
            .FirstOrDefaultAsync(ct);
    }
}