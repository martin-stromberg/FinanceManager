using System.Security.Claims;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure.Auth;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Auth;

/// <summary>
/// Verifies <see cref="JwtRefreshService.RefreshAsync"/> re-validates a refresh request against the user's
/// <em>current</em> database state (active flag, security stamp, admin role) instead of trusting the claims baked
/// into the presented principal - the mechanism that makes a revoked account or a revoked admin role take effect
/// immediately on refresh instead of only after the original token expires.
/// </summary>
public sealed class JwtRefreshServiceTests
{
    /// <summary>
    /// Verifies that a refresh request for a deactivated user is rejected even though the presented principal still
    /// carries a matching security stamp - deactivation must block further token issuance, not just new logins.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ShouldRejectInactiveUser()
    {
        var user = new User("user", "HASH::pw", false) { SecurityStamp = "stamp" };
        user.Deactivate();
        var (sut, _, _) = Create(user);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "stamp"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Verifies that a refresh request is rejected when the security stamp on the principal no longer matches the
    /// user's current stamp - the stamp changes whenever credentials or security-relevant properties change, so a
    /// mismatch means the token was issued before that change and must not be silently renewed.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ShouldRejectSecurityStampMismatch()
    {
        var user = new User("user", "HASH::pw", false) { SecurityStamp = "current" };
        var (sut, _, _) = Create(user);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "old"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Verifies that a principal minted while the user still held the Admin role is rejected once that role
    /// revocation has changed the user's security stamp - and that the role check and token creation are never even
    /// attempted, so a de-admin'd user cannot use a stale refresh token to keep renewing elevated access.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ShouldRejectOldAdminPrincipal_AfterRoleRevocationChangedSecurityStamp()
    {
        var user = new User("admin", "HASH::pw", true) { SecurityStamp = "current" };
        var (sut, userManager, jwt) = Create(user, isAdmin: false);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "old-admin-stamp", includeAdminRole: true), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        userManager.Verify(um => um.IsInRoleAsync(user, "Admin"), Times.Never);
        jwt.Verify(j => j.CreateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), out It.Ref<DateTime>.IsAny, It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// Verifies the happy path: a valid, current principal yields a new token created with the user's live admin
    /// status and security stamp (not values copied from the old principal) - confirming the role/stamp are looked
    /// up fresh rather than propagated from the presented claims.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ShouldCreateTokenWithCurrentAdminRoleAndSecurityStamp()
    {
        var user = new User("admin", "HASH::pw", true) { SecurityStamp = "current" };
        var (sut, userManager, jwt) = Create(user, isAdmin: true);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "current"), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("token", result.Token);
        userManager.Verify(um => um.IsInRoleAsync(user, "Admin"), Times.Once);
        jwt.Verify(j => j.CreateToken(user.Id, user.UserName, true, "current", out It.Ref<DateTime>.IsAny, user.PreferredLanguage, user.TimeZoneId), Times.Once);
    }

    private static (JwtRefreshService sut, Mock<UserManager<User>> userManager, Mock<IJwtTokenService> jwt) Create(User user, bool isAdmin = false)
    {
        var store = new Mock<IUserStore<User>>();
        var userManager = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        userManager.Setup(um => um.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        userManager.Setup(um => um.GetSecurityStampAsync(user)).ReturnsAsync(user.SecurityStamp!);
        userManager.Setup(um => um.IsInRoleAsync(user, "Admin")).ReturnsAsync(isAdmin);

        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), out It.Ref<DateTime>.IsAny, It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("token");

        var sut = new JwtRefreshService(userManager.Object, jwt.Object, Mock.Of<ILogger<JwtRefreshService>>());
        return (sut, userManager, jwt);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string securityStamp, bool includeAdminRole = false)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRefreshService.SecurityStampClaimType, securityStamp)
        };
        if (includeAdminRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, "Bearer");
        return new ClaimsPrincipal(identity);
    }
}
