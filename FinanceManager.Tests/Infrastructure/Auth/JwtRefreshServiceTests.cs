using System.Security.Claims;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure.Auth;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Auth;

public sealed class JwtRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_ShouldRejectInactiveUser()
    {
        var user = new User("user", "HASH::pw", false) { SecurityStamp = "stamp" };
        user.Deactivate();
        var (sut, _, _) = Create(user);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "stamp"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRejectSecurityStampMismatch()
    {
        var user = new User("user", "HASH::pw", false) { SecurityStamp = "current" };
        var (sut, _, _) = Create(user);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "old"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRejectOldAdminPrincipal_AfterRoleRevocationChangedSecurityStamp()
    {
        var user = new User("admin", "HASH::pw", true) { SecurityStamp = "current" };
        var (sut, userManager, jwt) = Create(user, isAdmin: false);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "old-admin-stamp", includeAdminRole: true));

        Assert.False(result.Succeeded);
        userManager.Verify(um => um.IsInRoleAsync(user, "Admin"), Times.Never);
        jwt.Verify(j => j.CreateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), out It.Ref<DateTime>.IsAny, It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_ShouldCreateTokenWithCurrentAdminRoleAndSecurityStamp()
    {
        var user = new User("admin", "HASH::pw", true) { SecurityStamp = "current" };
        var (sut, userManager, jwt) = Create(user, isAdmin: true);

        var result = await sut.RefreshAsync(CreatePrincipal(user.Id, "current"));

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
