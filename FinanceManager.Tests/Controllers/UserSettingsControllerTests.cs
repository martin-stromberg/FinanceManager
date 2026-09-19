using System.Reflection;
using System.Security.Claims;
using FinanceManager.Application;
using FinanceManager.Application.Users;
using FinanceManager.Domain;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Auth;
using FinanceManager.Web.Controllers;
using FinanceManager.Web.Infrastructure.Auth;
using FinanceManager.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for <see cref="UserSettingsController"/>'s password-change endpoint: model validation,
/// the error-code mapping to <c>ApiErrorDto</c> responses, the auth-cookie reissue after the
/// security-stamp rotation, and the declarative route/authorization contract.
/// </summary>
public sealed class UserSettingsControllerTests
{
    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid UserId { get; set; }
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
    }

    private static (
        UserSettingsController controller,
        AppDbContext db,
        TestCurrentUser currentUser,
        Mock<IUserAuthService> authService,
        Mock<UserManager<User>> userManager,
        Mock<IJwtTokenService> jwt,
        Mock<IAuthTokenProvider> tokenProvider) Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        var user = new User("u", "h", isAdmin: false);
        db.Users.Add(user);
        db.SaveChanges();

        var current = new TestCurrentUser { UserId = user.Id };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var sp = services.BuildServiceProvider();
        var localizer = sp.GetRequiredService<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();

        var logger = LoggerFactory.Create(b => { }).CreateLogger<UserSettingsController>();

        var jwtMock = new Mock<IJwtTokenService>();
        var tokenExpiresUtc = DateTime.UtcNow.AddHours(1);
        jwtMock.Setup(j => j.CreateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), out tokenExpiresUtc, It.IsAny<string?>(), It.IsAny<string?>()))
               .Returns("newtoken");
        var tokenProviderMock = new Mock<IAuthTokenProvider>();

        var store = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManagerMock.Setup(um => um.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        userManagerMock.Setup(um => um.IsInRoleAsync(It.IsAny<User>(), "Admin")).ReturnsAsync(false);
        userManagerMock.Setup(um => um.GetSecurityStampAsync(It.IsAny<User>())).ReturnsAsync((User u) => u.SecurityStamp ?? "stamp");
        var alphaVantageSecretProtectorMock = new Mock<IAlphaVantageSecretProtector>();
        var authServiceMock = new Mock<IUserAuthService>();

        var controller = new UserSettingsController(db, current, logger, localizer, jwtMock.Object, tokenProviderMock.Object, userManagerMock.Object, alphaVantageSecretProtectorMock.Object, authServiceMock.Object);
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, current.UserId.ToString()) }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, db, current, authServiceMock, userManagerMock, jwtMock, tokenProviderMock);
    }

    /// <summary>
    /// Verifies that a request with a pre-populated <c>ModelState</c> error is rejected as a
    /// validation problem without ever calling the auth service.
    /// </summary>
    [Fact]
    public async Task ChangePassword_InvalidModel_Returns400()
    {
        var (controller, _, _, authService, _, _, _) = Create();
        controller.ModelState.AddModelError(nameof(ChangePasswordRequest.NewPassword), "The NewPassword field is required.");

        var result = await controller.ChangePasswordAsync(new ChangePasswordRequest("old", ""), CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>();
        authService.Verify(s => s.ChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a rejected password change (wrong current password) returns 400 with the
    /// stable <c>Err_InvalidCurrentPassword</c> code inside an <c>ApiErrorDto</c>.
    /// </summary>
    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400()
    {
        var (controller, _, current, authService, _, _, _) = Create();
        authService
            .Setup(s => s.ChangePasswordAsync(current.UserId, "wrong", "new-pw-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Err_InvalidCurrentPassword"));

        var result = await controller.ChangePasswordAsync(new ChangePasswordRequest("wrong", "new-pw-123"), CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = bad.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.code.Should().Be("Err_InvalidCurrentPassword");
    }

    /// <summary>
    /// Verifies that a rejected password change caused by a policy violation returns 400 with the
    /// stable <c>Err_PasswordPolicyViolation</c> code inside an <c>ApiErrorDto</c>.
    /// </summary>
    [Fact]
    public async Task ChangePassword_PolicyViolation_Returns400()
    {
        var (controller, _, current, authService, _, _, _) = Create();
        authService
            .Setup(s => s.ChangePasswordAsync(current.UserId, "old", "short", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Err_PasswordPolicyViolation"));

        var result = await controller.ChangePasswordAsync(new ChangePasswordRequest("old", "short"), CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = bad.Value.Should().BeOfType<ApiErrorDto>().Subject;
        error.code.Should().Be("Err_PasswordPolicyViolation");
    }

    /// <summary>
    /// Verifies that a successful password change returns 204 and reissues the
    /// <c>FinanceManager.Auth</c> cookie — required because Identity rotates the security stamp,
    /// which would otherwise invalidate the current session on the next request.
    /// </summary>
    [Fact]
    public async Task ChangePassword_Success_Returns204_AndReissuesAuthCookie()
    {
        var (controller, _, current, authService, _, jwt, tokenProvider) = Create();
        authService
            .Setup(s => s.ChangePasswordAsync(current.UserId, "old-pw", "new-pw-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var result = await controller.ChangePasswordAsync(new ChangePasswordRequest("old-pw", "new-pw-123"), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("FinanceManager.Auth=newtoken");
        tokenProvider.Verify(t => t.InvalidateCache(), Times.Once);
        jwt.Verify(j => j.CreateToken(current.UserId, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), out It.Ref<DateTime>.IsAny, It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the endpoint answers 404 when the current user no longer exists in the
    /// identity store.
    /// </summary>
    [Fact]
    public async Task ChangePassword_UnknownUser_Returns404()
    {
        var (controller, _, _, authService, userManager, _, _) = Create();
        userManager.Setup(um => um.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var result = await controller.ChangePasswordAsync(new ChangePasswordRequest("old-pw", "new-pw-123"), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        authService.Verify(s => s.ChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies the route/authorization contract declaratively: the action must be reachable at
    /// <c>PUT api/user/settings/password</c> under the controller-level JWT authorization.
    /// </summary>
    [Fact]
    public void ChangePassword_RouteAndAuthorizationContract()
    {
        var method = typeof(UserSettingsController)
            .GetMethod(nameof(UserSettingsController.ChangePasswordAsync));

        method.Should().NotBeNull();
        var putAttr = method!.GetCustomAttribute<HttpPutAttribute>();
        putAttr.Should().NotBeNull();
        putAttr!.Template.Should().Be("password");

        typeof(UserSettingsController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Should().NotBeNull("UserSettingsController must require JWT bearer authentication");
    }
}
