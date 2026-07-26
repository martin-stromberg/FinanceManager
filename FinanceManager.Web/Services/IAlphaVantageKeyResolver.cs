using FinanceManager.Application;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly IAlphaVantageSecretProtector _secretProtector;
    private readonly ILogger<AlphaVantageKeyResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaVantageKeyResolver"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used to query stored API keys.</param>
    /// <param name="secretProtector">Protector used to restore and lazily protect stored API keys.</param>
    /// <param name="logger">Logger used for secret-free audit events.</param>
    public AlphaVantageKeyResolver(AppDbContext db, IAlphaVantageSecretProtector secretProtector, ILogger<AlphaVantageKeyResolver> logger)
    {
        _db = db;
        _secretProtector = secretProtector;
        _logger = logger;
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
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (!string.IsNullOrWhiteSpace(user?.AlphaVantageApiKey))
        {
            var key = await RestoreAndReprotectIfNeededAsync(user, "personal", requestedUserId: userId, ct);
            _logger.LogInformation("AlphaVantage API key resolved from {Source} key for user {RequestedUserId}", "personal", userId);
            return key;
        }

        return await GetSharedAsync(requestedUserId: userId, ct);
    }

    /// <summary>
    /// Returns a shared AlphaVantage key provided by an administrator who opted to share their key.
    /// When multiple admins are sharing the method deterministically chooses one by ordering on username.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The shared API key when present; otherwise <c>null</c>.</returns>
    /// <exception cref="System.Exception">Propagates exceptions thrown by the underlying data provider.</exception>
    public async Task<string?> GetSharedAsync(CancellationToken ct)
        => await GetSharedAsync(requestedUserId: null, ct);

    private async Task<string?> GetSharedAsync(Guid? requestedUserId, CancellationToken ct)
    {
        var admin = await _db.Users
            .Where(u => u.IsAdmin && u.ShareAlphaVantageApiKey && u.AlphaVantageApiKey != null)
            .OrderBy(u => u.UserName) // deterministic choice using mapped Identity property
            .FirstOrDefaultAsync(ct);
        if (admin == null)
        {
            return null;
        }

        var key = await RestoreAndReprotectIfNeededAsync(admin, "shared", requestedUserId, ct);
        _logger.LogInformation(
            "AlphaVantage API key resolved from {Source} admin key {AdminUserId} requested by {RequestedUserId}",
            "shared",
            admin.Id,
            requestedUserId);
        return key;
    }

    private async Task<string?> RestoreAndReprotectIfNeededAsync(User user, string source, Guid? requestedUserId, CancellationToken ct)
    {
        var storedValue = user.AlphaVantageApiKey;
        var key = _secretProtector.Unprotect(storedValue);
        if (string.IsNullOrWhiteSpace(key) || _secretProtector.IsProtected(storedValue))
        {
            return key;
        }

        user.SetAlphaVantageKey(_secretProtector.Protect(key));
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "AlphaVantage API key lazily protected for user {StoredUserId} from {Source} key requested by {RequestedUserId}",
            user.Id,
            source,
            requestedUserId);
        return key;
    }
}
