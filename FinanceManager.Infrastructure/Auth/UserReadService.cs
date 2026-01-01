using FinanceManager.Application.Users;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Lightweight read-only service for user presence checks used by setup and UI flows.
/// </summary>
public sealed class UserReadService : IUserReadService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserReadService"/> class.
    /// </summary>
    /// <param name="db">The application's <see cref="AppDbContext"/> used for read operations.</param>
    public UserReadService(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns whether any users exist in the system.
    /// </summary>
    /// <param name="ct">Cancellation token for the asynchronous operation.</param>
    /// <returns>A task that resolves to <c>true</c> when at least one user exists; otherwise <c>false</c>.</returns>
    /// <exception cref="System.Exception">Propagates exceptions thrown by the underlying data provider.</exception>
    public Task<bool> HasAnyUsersAsync(CancellationToken ct) => _db.Users.AsNoTracking().AnyAsync(ct);
}
