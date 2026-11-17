using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Mcp;

/// <summary>
/// Resolves the administrative role membership of a user — the same Identity role the
/// fm_admin_* tool gate reads via <c>IUserAdminService.GetAsync → UserAdminDto.IsAdmin</c>
/// (<c>UserManager.IsInRoleAsync(user, "Admin")</c>). Role membership, not the domain
/// <c>User.IsAdmin</c> flag, is authoritative here: a demotion via user administration
/// removes the role immediately, while the domain flag is a startup-synced projection.
/// The plain EF query keeps the check usable inside owner-scoped service paths where
/// <c>IUserAdminService</c> cannot run — its administrative caller gate
/// (<c>EnsureAdmin</c>) evaluates the calling user, not the examined owner.
/// </summary>
internal static class McpAdminRole
{
    /// <summary>
    /// Determines whether the user is a member of the <c>Admin</c> identity role.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="userId">User to examine.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when a role membership exists; <see langword="false"/> for
    /// non-admin and unknown users alike (restrictive direction).
    /// </returns>
    public static Task<bool> IsAdminAsync(AppDbContext db, Guid userId, CancellationToken ct)
        => db.UserRoles.AsNoTracking().AnyAsync(
            ur => ur.UserId == userId
                && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"), ct);
}
