using FinanceManager.Application;
using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests of <see cref="AiActionRequestsController"/> (F-0905): the owner id always comes
/// from <see cref="ICurrentUserService.UserId"/> (isolation at controller level), filters are
/// forwarded, decisions carry the optional comment, foreign/missing ids yield 404 and
/// invalid state transitions surface as <c>Err_Mcp_InvalidState</c> API errors.
/// </summary>
public sealed class AiActionRequestsControllerTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
    }

    private static (AiActionRequestsController controller, Mock<IMcpApprovalService> svc, TestCurrentUserService user) Create()
    {
        var svc = new Mock<IMcpApprovalService>(MockBehavior.Strict);
        var user = new TestCurrentUserService();
        var localizer = new Mock<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: false));

        var controller = new AiActionRequestsController(
            svc.Object, user, NullLogger<AiActionRequestsController>.Instance, localizer.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return (controller, svc, user);
    }

    private static AiActionRequestListItemDto ListItem(Guid accessId)
        => new(Guid.NewGuid(), accessId, "acc", "fm_contacts_manage", "delete", 0,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(72), null, null, null, null,
            Guid.NewGuid(), 0);

    /// <summary>The list endpoint scopes to the current user and forwards status/access filters.</summary>
    [Fact]
    public async Task List_PassesOwnerAndFilters()
    {
        var (c, svc, user) = Create();
        var access = Guid.NewGuid();
        svc.Setup(s => s.ListAsync(user.UserId, AiActionRequestStatus.Pending, access, 5, 50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListItem(access) });

        var result = await c.ListAsync((short)AiActionRequestStatus.Pending, access, 5, 50, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<AiActionRequestListItemDto>>(ok.Value);
        Assert.Single(items);
        svc.VerifyAll();
    }

    /// <summary>An out-of-range status filter is rejected with an ApiErrorDto before the service runs.</summary>
    [Fact]
    public async Task List_InvalidStatus_Returns400()
    {
        var (c, svc, _) = Create();
        var bad = Assert.IsType<BadRequestObjectResult>(
            await c.ListAsync(99, null, 0, 50, CancellationToken.None));
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Equal("Err_Mcp_InvalidArguments", err.code);
        svc.VerifyNoOtherCalls();
    }

    /// <summary>Detail returns 404 for a missing/foreign id and 200 with the detail otherwise.</summary>
    [Fact]
    public async Task Get_NotFound_ForForeignId_And_Ok_ForOwn()
    {
        var (c, svc, user) = Create();
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetAsync(user.UserId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiActionRequestDetailDto?)null);
        Assert.IsType<NotFoundResult>(await c.GetAsync(id, CancellationToken.None));

        var dto = new AiActionRequestDetailDto(id, Guid.NewGuid(), "acc", "fm_x", "delete",
            "katalog-v1", "{}", null, 0, DateTime.UtcNow, DateTime.UtcNow.AddHours(72),
            null, null, null, null, null, null, Guid.NewGuid(), "key", 0);
        svc.Setup(s => s.GetAsync(user.UserId, id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        var ok = Assert.IsType<OkObjectResult>(await c.GetAsync(id, CancellationToken.None));
        Assert.Same(dto, ok.Value);
    }

    /// <summary>Approve forwards the owner id and decision comment; 204 on success.</summary>
    [Fact]
    public async Task Approve_PassesOwnerAndComment_Returns204()
    {
        var (c, svc, user) = Create();
        var id = Guid.NewGuid();
        svc.Setup(s => s.ApproveAsync(user.UserId, id, "go ahead", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await c.ApproveAsync(id, new AiActionRequestDecisionRequest("go ahead"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        svc.VerifyAll();
    }

    /// <summary>Approve on a missing/foreign id returns 404.</summary>
    [Fact]
    public async Task Approve_NotFound_Returns404()
    {
        var (c, svc, user) = Create();
        var id = Guid.NewGuid();
        svc.Setup(s => s.ApproveAsync(user.UserId, id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Assert.IsType<NotFoundResult>(await c.ApproveAsync(id, null, CancellationToken.None));
    }

    /// <summary>A non-pending request surfaces as Err_Mcp_InvalidState API error.</summary>
    [Fact]
    public async Task Approve_InvalidState_Returns400_WithCode()
    {
        var (c, svc, user) = Create();
        var id = Guid.NewGuid();
        svc.Setup(s => s.ApproveAsync(user.UserId, id, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Err_Mcp_InvalidState", "not pending"));

        var bad = Assert.IsType<BadRequestObjectResult>(await c.ApproveAsync(id, null, CancellationToken.None));
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Equal("Err_Mcp_InvalidState", err.code);
    }

    /// <summary>Reject forwards the rejection reason; 204 on success.</summary>
    [Fact]
    public async Task Reject_PassesOwnerAndComment_Returns204()
    {
        var (c, svc, user) = Create();
        var id = Guid.NewGuid();
        svc.Setup(s => s.RejectAsync(user.UserId, id, "too risky", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await c.RejectAsync(id, new AiActionRequestDecisionRequest("too risky"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        svc.VerifyAll();
    }

    /// <summary>Cancel withdraws the pending request; 404 on missing/foreign id.</summary>
    [Fact]
    public async Task Cancel_Ok_And_NotFound()
    {
        var (c, svc, user) = Create();
        var id = Guid.NewGuid();
        svc.Setup(s => s.CancelAsync(user.UserId, id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Assert.IsType<NotFoundResult>(await c.CancelAsync(id, CancellationToken.None));

        svc.Setup(s => s.CancelAsync(user.UserId, id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Assert.IsType<NoContentResult>(await c.CancelAsync(id, CancellationToken.None));
    }
}
