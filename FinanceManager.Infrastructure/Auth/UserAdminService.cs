using FinanceManager.Application.Users;
using FinanceManager.Domain.Contacts; // added
using FinanceManager.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Administrative service for managing application users (list, create, update, password reset, unlock, delete, attachments).
/// This service is intended for administrative tooling and bypasses some user-facing restrictions (for example creating a Self contact).
/// </summary>
public sealed class UserAdminService : IUserAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly IPasswordHashingService _passwordHasher;
    private readonly ILogger<UserAdminService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAdminService"/> class.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="userManager">Identity <see cref="UserManager{TUser}"/> used for role operations and lockout handling.</param>
    /// <param name="passwordHasher">Service used to hash passwords for manual resets/creates.</param>
    /// <param name="logger">Logger instance for the service.</param>
    public UserAdminService(AppDbContext db, UserManager<User> userManager, IPasswordHashingService passwordHasher, ILogger<UserAdminService> logger)
    {
        _db = db;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Lists all users in the system with administrative metadata.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="UserAdminDto"/> describing users.</returns>
    /// <exception cref="System.InvalidOperationException">Rethrows database related exceptions when the underlying query fails.</exception>
    public async Task<IReadOnlyList<UserAdminDto>> ListAsync(CancellationToken ct)
    {
        _logger.LogInformation("Listing users");
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync(ct);

        var list = new List<UserAdminDto>(users.Count);
        foreach (var u in users)
        {
            var isAdmin = await _userManager.IsInRoleAsync(u, "Admin");
            list.Add(new UserAdminDto(
                u.Id,
                u.UserName,
                isAdmin,
                u.Active,
                u.LockoutEnd.HasValue ? u.LockoutEnd.Value.UtcDateTime : (DateTime?)null,
                u.LastLoginUtc,
                u.PreferredLanguage));
        }

        _logger.LogInformation("Listed {Count} users", list.Count);
        return list;
    }

    /// <summary>
    /// Gets detailed administrative information for a single user.
    /// </summary>
    /// <param name="id">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="UserAdminDto"/> when the user exists; otherwise <c>null</c>.</returns>
    public async Task<UserAdminDto?> GetAsync(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting user {UserId}", id);
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .FirstOrDefaultAsync(ct);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", id);
            return null;
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        return new UserAdminDto(
            user.Id,
            user.UserName,
            isAdmin,
            user.Active,
            user.LockoutEnd.HasValue ? user.LockoutEnd.Value.UtcDateTime : (DateTime?)null,
            user.LastLoginUtc,
            user.PreferredLanguage);
    }

    /// <summary>
    /// Creates a new application user and optionally assigns the Admin role.
    /// The method also creates a Self contact for the user if none exists.
    /// </summary>
    /// <param name="username">New user's username. Required.</param>
    /// <param name="password">Initial password. Required.</param>
    /// <param name="isAdmin">When <c>true</c> the user will be assigned the Admin role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="UserAdminDto"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a user with the same username already exists.</exception>
    /// <exception cref="DbUpdateException">Thrown when persisting user or contact to the database fails.</exception>
    public async Task<UserAdminDto> CreateAsync(string username, string password, bool isAdmin, CancellationToken ct)
    {
        _logger.LogInformation("Creating user {Username} (IsAdmin={IsAdmin})", username, isAdmin);
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username required", nameof(username));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password required", nameof(password));

        bool exists = await _db.Users.AnyAsync(u => u.UserName == username, ct);
        if (exists)
        {
            _logger.LogWarning("Cannot create user {Username}: already exists", username);
            throw new InvalidOperationException("Username already exists");
        }

        var user = new User(username, _passwordHasher.Hash(password), false);
        user.LockoutEnabled = !isAdmin;
        if (string.IsNullOrEmpty(user.SecurityStamp)) user.SecurityStamp = Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(user.ConcurrencyStamp)) user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Create self contact (admin path) — bypass public service restriction on creating Self contacts.
        bool hasSelf = await _db.Contacts.AsNoTracking().AnyAsync(c => c.OwnerUserId == user.Id && c.Type == ContactType.Self, ct);
        if (!hasSelf)
        {
            _db.Contacts.Add(new Contact(user.Id, user.UserName, ContactType.Self, null));
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created self contact for user {UserId}", user.Id);
        }

        // Assign role if requested
        if (isAdmin)
        {
            try
            {
                var roleName = "Admin";
                // ensure role exists
                var roleExists = await _userManager.IsInRoleAsync(user, roleName);
                if (!roleExists)
                {
                    // Creating role requires RoleManager; try via UserManager.AddToRoleAsync which will fail if role missing.
                    var addRes = await _userManager.AddToRoleAsync(user, roleName);
                    if (!addRes.Succeeded)
                    {
                        _logger.LogWarning("Failed to add user {UserId} to role {Role}: {Errors}", user.Id, roleName, string.Join(';', addRes.Errors.Select(e => e.Description)));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Assigning Admin role to created user failed (non-fatal)");
            }
        }

        _logger.LogInformation("Created user {UserId} ({Username})", user.Id, user.UserName);
        var finalIsAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        return new UserAdminDto(
            user.Id,
            user.UserName,
            finalIsAdmin,
            user.Active,
            user.LockoutEnd.HasValue ? user.LockoutEnd.Value.UtcDateTime : (DateTime?)null,
            user.LastLoginUtc,
            user.PreferredLanguage);
    }

    /// <summary>
    /// Updates an existing user's properties and role membership.
    /// </summary>
    /// <param name="id">User identifier to update.</param>
    /// <param name="username">New username (optional).</param>
    /// <param name="isAdmin">Request to add/remove the Admin role (optional).</param>
    /// <param name="active">Active flag to activate/deactivate the user (optional).</param>
    /// <param name="preferredLanguage">Preferred language code (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="UserAdminDto"/>, or <c>null</c> when the user was not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when username rename conflicts with an existing user.</exception>
    public async Task<UserAdminDto?> UpdateAsync(Guid id, string? username, bool? isAdmin, bool? active, string? preferredLanguage, CancellationToken ct)
    {
        _logger.LogInformation("Updating user {UserId}", id);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for update", id);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(username) && !string.Equals(user.UserName, username, StringComparison.OrdinalIgnoreCase))
        {
            bool exists = await _db.Users.AnyAsync(u => u.UserName == username && u.Id != id, ct);
            if (exists)
            {
                _logger.LogWarning("Cannot rename user {UserId} to {NewUsername}: target already exists", id, username);
                throw new InvalidOperationException("Username already exists");
            }
            _logger.LogInformation("Renaming user {UserId} from {OldUsername} to {NewUsername}", id, user.UserName, username.Trim());
            user.Rename(username.Trim());
        }

        if (isAdmin.HasValue)
        {
            try
            {
                var roleName = "Admin";
                var currentlyInRole = await _userManager.IsInRoleAsync(user, roleName);
                if (isAdmin.Value && !currentlyInRole)
                {
                    var addRes = await _userManager.AddToRoleAsync(user, roleName);
                    if (!addRes.Succeeded)
                    {
                        _logger.LogWarning("Failed to add user {UserId} to role {Role}: {Errors}", id, roleName, string.Join(';', addRes.Errors.Select(e => e.Description)));
                    }
                }
                else if (!isAdmin.Value && currentlyInRole)
                {
                    var removeRes = await _userManager.RemoveFromRoleAsync(user, roleName);
                    if (!removeRes.Succeeded)
                    {
                        _logger.LogWarning("Failed to remove user {UserId} from role {Role}: {Errors}", id, roleName, string.Join(';', removeRes.Errors.Select(e => e.Description)));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Updating roles for user {UserId} failed (non-fatal)", id);
            }
        }

        if (active.HasValue && user.Active != active.Value)
        {
            if (!active.Value)
            {
                _logger.LogInformation("Deactivating user {UserId}", id);
                user.Deactivate();
            }
            else
            {
                _logger.LogInformation("Activating user {UserId}", id);
                user.Activate();
            }
        }

        if (preferredLanguage != null && preferredLanguage != user.PreferredLanguage)
        {
            _logger.LogInformation("Setting preferred language for user {UserId} to {Lang}", id, preferredLanguage);
            user.SetPreferredLanguage(preferredLanguage);
        }

        await _db.SaveChangesAsync(ct);
        var finalIsAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        return new UserAdminDto(
            user.Id,
            user.UserName,
            finalIsAdmin,
            user.Active,
            user.LockoutEnd.HasValue ? user.LockoutEnd.Value.UtcDateTime : (DateTime?)null,
            user.LastLoginUtc,
            user.PreferredLanguage);
    }

    /// <summary>
    /// Resets the password for the specified user by setting a new hashed password value.
    /// </summary>
    /// <param name="id">User identifier to reset the password for.</param>
    /// <param name="newPassword">New plaintext password to set (will be hashed).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the user existed and the password was updated; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="newPassword"/> is empty.</exception>
    public async Task<bool> ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct)
    {
        _logger.LogInformation("Resetting password for user {UserId}", id);
        if (string.IsNullOrWhiteSpace(newPassword)) throw new ArgumentException("Password required", nameof(newPassword));
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for password reset", id);
            return false;
        }
        user.SetPasswordHash(_passwordHasher.Hash(newPassword));
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Password reset for user {UserId} completed", id);
        return true;
    }

    /// <summary>
    /// Clears lockout state for the specified user and resets the failed access count.
    /// </summary>
    /// <param name="id">User identifier to unlock.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the unlock operation succeeded; otherwise <c>false</c>.</returns>
    public async Task<bool> UnlockAsync(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Unlocking user {UserId}", id);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for unlock", id);
            return false;
        }

        // Use Identity's UserManager to clear lockout and reset access-failed count.
        var setResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!setResult.Succeeded)
        {
            _logger.LogWarning("Failed to clear lockout for user {UserId}: {Errors}", id, string.Join(';', setResult.Errors.Select(e => e.Description)));
            return false;
        }

        var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded)
        {
            _logger.LogWarning("Failed to reset access failed count for user {UserId}: {Errors}", id, string.Join(';', resetResult.Errors.Select(e => e.Description)));
            // lockout cleared but failed count not reset — still consider operation successful,
            // caller can retry/reset manually if needed.
        }

        _logger.LogInformation("Cleared lockout for user {UserId}", id);
        return true;
    }

    /// <summary>
    /// Deletes the specified user from the database.
    /// </summary>
    /// <param name="id">User identifier to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the user existed and was removed; otherwise <c>false</c>.</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting user {UserId}", id);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for delete", id);
            return false;
        }
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted user {UserId}", id);
        return true;
    }

    /// <summary>
    /// Sets or clears a symbol attachment reference for the target user. Intended for admin use.
    /// </summary>
    /// <param name="userId">Administrator user id performing the action (for logging / auditing).</param>
    /// <param name="targetUserId">Target user identifier whose symbol is modified.</param>
    /// <param name="attachmentId">Attachment id to set or <c>null</c> to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the change has been persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when the target user is not found.</exception>
    public async Task SetSymbolAttachmentAsync(Guid userId, Guid targetUserId, Guid? attachmentId, CancellationToken ct)
    {
        _logger.LogInformation("Setting symbol for user {TargetUserId} by admin {AdminId}", targetUserId, userId);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct);
        if (user == null) throw new ArgumentException("User not found", nameof(targetUserId));
        user.SetSymbolAttachment(attachmentId);
        await _db.SaveChangesAsync(ct);
    }
}
