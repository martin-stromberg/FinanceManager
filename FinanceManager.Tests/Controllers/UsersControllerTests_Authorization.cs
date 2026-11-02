using FinanceManager.Application;
using FinanceManager.Application.Demo;
using FinanceManager.Application.Users;
using FinanceManager.Shared.Dtos.Users;
using FinanceManager.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using SharedResources = FinanceManager.Web.Controllers.Controller;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for the authorization contract of <see cref="UsersController"/>: the controller must
/// require authentication, the demo-data endpoint must be limited to the current user (or an
/// admin), and the anonymous "exists" probe must stay reachable for the login flow.
/// </summary>
public sealed class UsersControllerTests_Authorization
{
    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid UserId { get; set; }
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin { get; set; }
    }

    private static (UsersController controller, TestCurrentUser current, Mock<IDemoDataService> demo) Create()
    {
        var localizer = new Mock<IStringLocalizer<SharedResources>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        var current = new TestCurrentUser { UserId = Guid.NewGuid() };
        var demo = new Mock<IDemoDataService>();
        demo.Setup(d => d.CreateDemoDataAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = new UsersController(
            new Mock<IUserReadService>().Object,
            demo.Object,
            current,
            new Mock<ILogger<UsersController>>().Object,
            localizer.Object);
        return (controller, current, demo);
    }

    /// <summary>
    /// The controller class must carry an authorize attribute so all endpoints require a
    /// signed-in user unless explicitly exempted.
    /// </summary>
    [Fact]
    public void Class_AuthorizeAttribute_RequiresAuthentication()
    {
        Assert.NotNull(typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>());
    }

    /// <summary>
    /// The "exists" probe must stay anonymous because the login page queries it before sign-in.
    /// </summary>
    [Fact]
    public void HasAnyUsersAsync_AllowAnonymous_RemainsPubliclyReachable()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.HasAnyUsersAsync));
        Assert.NotNull(method!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    /// <summary>
    /// Requesting demo data for a different user id must be rejected with Forbid.
    /// </summary>
    [Fact]
    public async Task CreateDemoDataAsync_ForeignUserId_ReturnsForbid()
    {
        var (controller, _, demo) = Create();

        var result = await controller.CreateDemoDataAsync(Guid.NewGuid(), new DemoRequest(false), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        demo.Verify(d => d.CreateDemoDataAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Requesting demo data for the own user id executes the demo-data service.
    /// </summary>
    [Fact]
    public async Task CreateDemoDataAsync_OwnUserId_ReturnsAccepted()
    {
        var (controller, current, demo) = Create();

        var result = await controller.CreateDemoDataAsync(current.UserId, new DemoRequest(false), CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        demo.Verify(d => d.CreateDemoDataAsync(current.UserId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Admins may still trigger demo data for arbitrary users.
    /// </summary>
    [Fact]
    public async Task CreateDemoDataAsync_AdminForForeignUser_ReturnsAccepted()
    {
        var (controller, current, _) = Create();
        current.IsAdmin = true;

        var result = await controller.CreateDemoDataAsync(Guid.NewGuid(), new DemoRequest(false), CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
    }
}
