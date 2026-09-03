using FinanceManager.Application;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Auth
{
    /// <summary>
    /// Tests for <see cref="UserAdminService"/> covering admin-driven user management: creation,
    /// renaming, password resets, unlocking, deletion, and - most importantly - the rule that any
    /// change affecting a user's active state or admin role must invalidate their ASP.NET Identity
    /// security stamp so already-issued sessions/tokens stop being trusted immediately.
    /// </summary>
    public sealed class UserAdminServiceTests
    {
        private static (UserAdminService sut, AppDbContext db, Mock<IPasswordHashingService> hasher, Mock<UserManager<User>> userManagerMock) Create()
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddLogging(); // Logger registrieren
            var sp = services.BuildServiceProvider();
            var db = sp.GetRequiredService<AppDbContext>();
            var hasher = new Mock<IPasswordHashingService>();
            hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(p => $"HASH::{p}");
            var logger = sp.GetRequiredService<ILogger<UserAdminService>>(); // Logger abrufen

            // create a minimal UserManager mock to satisfy constructor
            var store = new Mock<IUserStore<User>>();
            var userManagerMock = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManagerMock.Setup(u => u.SetLockoutEndDateAsync(It.IsAny<User>(), It.IsAny<DateTimeOffset?>())).ReturnsAsync(IdentityResult.Success);
            userManagerMock.Setup(u => u.ResetAccessFailedCountAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
            userManagerMock.Setup(u => u.UpdateSecurityStampAsync(It.IsAny<User>()))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<User>(u => u.SecurityStamp = Guid.NewGuid().ToString("N"));

            // --- NEW: maintain role membership state inside the mock ---
            var adminUsers = new HashSet<Guid>();
            userManagerMock
                .Setup(um => um.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<User, string>((user, role) =>
                {
                    if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                        adminUsers.Add(user.Id);
                });

            userManagerMock
                .Setup(um => um.RemoveFromRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success)
                .Callback<User, string>((user, role) =>
                {
                    if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                        adminUsers.Remove(user.Id);
                });

            userManagerMock
                .Setup(um => um.IsInRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync((User user, string role) =>
                {
                    if (user == null || string.IsNullOrEmpty(role)) return false;
                    return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && adminUsers.Contains(user.Id);
                });
            // --- END NEW ---

            var sut = new UserAdminService(db, userManagerMock.Object, hasher.Object, new TestCurrentUserService(true), logger); // pass userManager
            return (sut, db, hasher, userManagerMock);
        }

        /// <summary>
        /// Verifies that creating a user persists it with the given username and admin flag,
        /// and that exactly one row exists afterwards - the baseline happy path for admin-driven
        /// user creation.
        /// </summary>
        [Fact]
        public async Task CreateAsync_ShouldPersistUser()
        {
            var (sut, db, _, _) = Create();
            var dto = await sut.CreateAsync("alice", "Password1", true, CancellationToken.None);
            Assert.Equal("alice", dto.Username);
            Assert.True(dto.IsAdmin);
            Assert.Equal(1, db.Users.Count());
        }

        /// <summary>
        /// Verifies that creating a user with a username that already exists is rejected with
        /// <see cref="InvalidOperationException"/> instead of silently creating a second account,
        /// enforcing username uniqueness at the service layer.
        /// </summary>
        [Fact]
        public async Task CreateAsync_DuplicateUsername_Throws()
        {
            var (sut, db, _, _) = Create();
            db.Users.Add(new User("alice", "HASH::x", false));
            db.SaveChanges();
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateAsync("alice", "pw", false, CancellationToken.None));
        }

        /// <summary>
        /// Verifies that renaming a user to a username that is not yet taken succeeds and the new
        /// name is reflected in the returned DTO.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_Rename_WhenUnique_Works()
        {
            var (sut, db, _, _) = Create();
            var u = new User("old", "HASH::x", false);
            db.Users.Add(u); db.SaveChanges();
            var updated = await sut.UpdateAsync(u.Id, "new", null, null, null, CancellationToken.None);
            Assert.Equal("new", updated!.Username);
        }

        /// <summary>
        /// Verifies that renaming a user to a name already used by a different user is rejected,
        /// preventing username collisions from being introduced through an update rather than
        /// only being checked at creation time.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_Rename_ToExisting_Throws()
        {
            var (sut, db, _, _) = Create();
            var a = new User("a", "HASH::x", false);
            var b = new User("b", "HASH::x", false);
            db.Users.AddRange(a, b); db.SaveChanges();
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(a.Id, "b", null, null, null, CancellationToken.None));
        }

        /// <summary>
        /// Verifies that resetting a user's password replaces the stored password hash with the
        /// newly hashed value, confirming the plain-text password is never persisted as-is.
        /// </summary>
        [Fact]
        public async Task ResetPasswordAsync_UpdatesHash()
        {
            var (sut, db, hasher, _) = Create();
            var u = new User("user", "HASH::old", false);
            db.Users.Add(u); db.SaveChanges();
            hasher.Setup(h => h.Hash("newpw")).Returns("HASH::newpw");
            var ok = await sut.ResetPasswordAsync(u.Id, "newpw", CancellationToken.None);
            Assert.True(ok);
            // Verify hash changed in tracked entity (reflection set) by reloading
            var re = db.Users.Single();
            Assert.Equal("HASH::newpw", re.PasswordHash);
        }

        /// <summary>
        /// Verifies that deactivating a user's account triggers a security stamp update, which
        /// invalidates any bearer tokens or cookies already issued to that account - deactivation
        /// would otherwise not take effect until an existing session naturally expired.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_Deactivate_ShouldUpdateSecurityStamp()
        {
            var (sut, db, _, userManagerMock) = Create();
            var u = new User("user", "HASH::x", false);
            db.Users.Add(u);
            db.SaveChanges();

            await sut.UpdateAsync(u.Id, null, null, false, null, CancellationToken.None);

            userManagerMock.Verify(um => um.UpdateSecurityStampAsync(It.Is<User>(x => x.Id == u.Id)), Times.Once);
        }

        /// <summary>
        /// Verifies that removing the Admin role from a user also triggers a security stamp
        /// update, so a token issued while the user was still an admin cannot keep carrying
        /// elevated privileges after the role was revoked.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_RemoveAdminRole_ShouldUpdateSecurityStamp()
        {
            var (sut, db, _, userManagerMock) = Create();
            var u = new User("admin", "HASH::x", true);
            db.Users.Add(u);
            db.SaveChanges();
            await userManagerMock.Object.AddToRoleAsync(u, "Admin");

            await sut.UpdateAsync(u.Id, null, false, null, null, CancellationToken.None);

            userManagerMock.Verify(um => um.UpdateSecurityStampAsync(It.Is<User>(x => x.Id == u.Id)), Times.Once);
        }

        /// <summary>
        /// Verifies that <c>UpdateAsync</c> throws <see cref="InvalidOperationException"/> when the
        /// security stamp update fails during deactivation, rather than committing the deactivation
        /// while leaving the stale security stamp in place - a half-applied change would let the
        /// deactivated user's existing session keep working.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_Deactivate_ShouldThrow_WhenSecurityStampUpdateFails()
        {
            var (sut, db, _, userManagerMock) = Create();
            var u = new User("user", "HASH::x", false);
            db.Users.Add(u);
            db.SaveChanges();
            userManagerMock.Setup(um => um.UpdateSecurityStampAsync(It.Is<User>(x => x.Id == u.Id)))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "stamp failed" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(u.Id, null, null, false, null, CancellationToken.None));
        }

        /// <summary>
        /// Verifies that the same failure propagation applies when removing the Admin role and the
        /// security stamp update fails: the whole update is rejected rather than leaving the role
        /// change committed without a corresponding stamp refresh.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_RemoveAdminRole_ShouldThrow_WhenSecurityStampUpdateFails()
        {
            var (sut, db, _, userManagerMock) = Create();
            var u = new User("admin", "HASH::x", true);
            db.Users.Add(u);
            db.SaveChanges();
            await userManagerMock.Object.AddToRoleAsync(u, "Admin");
            userManagerMock.Setup(um => um.UpdateSecurityStampAsync(It.Is<User>(x => x.Id == u.Id)))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "stamp failed" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(u.Id, null, false, null, null, CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <c>UpdateAsync</c> skips the security stamp refresh when neither the
        /// active flag nor the admin role actually changed, so a no-op update does not needlessly
        /// invalidate the user's existing sessions.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_UnchangedActiveAndRole_ShouldNotUpdateSecurityStamp()
        {
            var (sut, db, _, userManagerMock) = Create();
            var u = new User("user", "HASH::x", false);
            db.Users.Add(u);
            db.SaveChanges();

            await sut.UpdateAsync(u.Id, null, false, true, null, CancellationToken.None);

            userManagerMock.Verify(um => um.UpdateSecurityStampAsync(It.IsAny<User>()), Times.Never);
        }

        /// <summary>
        /// Verifies that resetting a password to an empty string is rejected with
        /// <see cref="ArgumentException"/> instead of hashing and persisting an unusable password.
        /// </summary>
        [Fact]
        public async Task ResetPasswordAsync_Empty_Throws()
        {
            var (sut, db, _, _) = Create();
            var u = new User("user", "HASH::old", false);
            db.Users.Add(u); db.SaveChanges();
            await Assert.ThrowsAsync<ArgumentException>(() => sut.ResetPasswordAsync(u.Id, "", CancellationToken.None));
        }

        /// <summary>
        /// Verifies that a failed security stamp update during a password reset causes
        /// <c>ResetPasswordAsync</c> to throw, so a new password hash is never committed without
        /// also invalidating sessions that were authenticated with the old password.
        /// </summary>
        [Fact]
        public async Task ResetPasswordAsync_ShouldThrow_WhenSecurityStampUpdateFails()
        {
            var (sut, db, hasher, userManagerMock) = Create();
            var u = new User("user", "HASH::old", false);
            db.Users.Add(u);
            db.SaveChanges();
            hasher.Setup(h => h.Hash("newpw")).Returns("HASH::newpw");
            userManagerMock.Setup(um => um.UpdateSecurityStampAsync(It.Is<User>(x => x.Id == u.Id)))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "stamp failed" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResetPasswordAsync(u.Id, "newpw", CancellationToken.None));
        }

        /// <summary>
        /// Verifies that unlocking a user clears both the Identity lockout end date and the
        /// access-failed counter, so the account is fully usable again rather than still being one
        /// failed attempt away from re-triggering the lockout threshold.
        /// </summary>
        [Fact]
        public async Task UnlockAsync_ClearsIdentityLockout()
        {
            var (sut, db, _, userManagerMock) = Create();
            var u = new User("user", "HASH::x", false);
            // set Identity lockout end
            u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
            db.Users.Add(u); db.SaveChanges();

            var ok = await sut.UnlockAsync(u.Id, CancellationToken.None);
            Assert.True(ok);

            userManagerMock.Verify(um => um.SetLockoutEndDateAsync(It.Is<User>(x => x.Id == u.Id), null), Times.Once);
            userManagerMock.Verify(um => um.ResetAccessFailedCountAsync(It.Is<User>(x => x.Id == u.Id)), Times.Once);
        }

        /// <summary>
        /// Verifies that deleting an existing user removes their record from the database.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_RemovesUser()
        {
            var (sut, db, _, _) = Create();
            var u = new User("user", "HASH::x", false);
            db.Users.Add(u); db.SaveChanges();
            var ok = await sut.DeleteAsync(u.Id, CancellationToken.None);
            Assert.True(ok);
            Assert.Equal(0, db.Users.Count());
        }

        /// <summary>
        /// Verifies that deleting a user id that does not exist returns <see langword="false"/>
        /// instead of throwing, so callers can treat a missing target as a harmless no-op.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_NonExisting_ReturnsFalse()
        {
            var (sut, db, _, _) = Create();
            var ok = await sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.False(ok);
        }

        private sealed class TestCurrentUserService : ICurrentUserService
        {
            public TestCurrentUserService(bool isAdmin)
            {
                IsAdmin = isAdmin;
            }

            public Guid UserId { get; } = Guid.NewGuid();

            public string? PreferredLanguage => null;

            public bool IsAuthenticated => true;

            public bool IsAdmin { get; }
        }
    }
}
