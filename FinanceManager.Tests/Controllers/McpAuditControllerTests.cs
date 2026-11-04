using FinanceManager.Application;
using FinanceManager.Application.Mcp;
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
/// Tests of <see cref="McpAuditController"/> (F-0601): the owner id is always taken from
/// <see cref="ICurrentUserService.UserId"/> (isolation at controller level), query filters are
/// forwarded, detail/usage return 404 for missing or foreign ids, export validates the format
/// and produces csv/json file results.
/// </summary>
public sealed class McpAuditControllerTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
    }

    private static (McpAuditController controller, Mock<IMcpAuditQueryService> svc, TestCurrentUserService user) Create()
    {
        var svc = new Mock<IMcpAuditQueryService>(MockBehavior.Strict);
        var user = new TestCurrentUserService();
        var localizer = new Mock<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: false));

        var controller = new McpAuditController(
            svc.Object, Mock.Of<FinanceManager.Application.Mcp.IMcpSecurityOverviewService>(),
            user, NullLogger<McpAuditController>.Instance, localizer.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return (controller, svc, user);
    }

    private static McpAuditEntryListItemDto Item(Guid ownerAccess)
        => new(Guid.NewGuid(), DateTime.UtcNow, 1, 0, "ToolCall", "Success",
            ownerAccess, "acc", "fm_postings_query", null, "McpEndpoint", 10, 3, 0);

    private static McpAuditEntryDetailDto Detail()
        => new(Guid.NewGuid(), DateTime.UtcNow, 1, 0, "ToolCall", "Success",
            Guid.NewGuid(), "acc", "fm_postings_query", "katalog-v1", "McpEndpoint",
            null, null, 10, 3, null, null, null, "hash",
            Array.Empty<McpAuditChangeDto>(), DateTime.UtcNow);

    /// <summary>The list endpoint scopes to the current user and forwards all filters.</summary>
    [Fact]
    public async Task List_PassesOwnerAndFilters()
    {
        var (c, svc, user) = Create();
        var access = Guid.NewGuid();
        var corr = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        svc.Setup(s => s.ListAsync(user.UserId,
                It.Is<McpAuditQueryFilter>(f =>
                    f.FromUtc == from && f.ToUtc == to && f.Category == 0
                    && f.McpAccessId == access && f.CorrelationId == corr
                    && f.ToolName == "fm_x" && f.ResultStatus == "Denied"),
                5, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Item(access) });

        var result = await c.ListAsync(from, to, 0, access, corr, "fm_x", "Denied", 5, 50, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<McpAuditEntryListItemDto>>(ok.Value);
        Assert.Single(items);
        svc.VerifyAll();
    }

    /// <summary>Detail returns 404 for a missing/foreign id and 200 with the detail otherwise.</summary>
    [Fact]
    public async Task Get_NotFound_ForForeignId_And_Ok_ForOwn()
    {
        var (c, svc, user) = Create();
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetAsync(user.UserId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAuditEntryDetailDto?)null);
        Assert.IsType<NotFoundResult>(await c.GetAsync(id, CancellationToken.None));

        var dto = Detail();
        svc.Setup(s => s.GetAsync(user.UserId, dto.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var ok = Assert.IsType<OkObjectResult>(await c.GetAsync(dto.Id, CancellationToken.None));
        Assert.Same(dto, ok.Value);
    }

    /// <summary>Verify returns the chain verification result scoped to the current user.</summary>
    [Fact]
    public async Task Verify_ReturnsResult_ForCurrentUser()
    {
        var (c, svc, user) = Create();
        var expected = new McpAuditVerifyResultDto(3, true, null, null, Array.Empty<McpAuditChainGapDto>());
        svc.Setup(s => s.VerifyAsync(user.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var ok = Assert.IsType<OkObjectResult>(await c.VerifyAsync(CancellationToken.None));
        Assert.Same(expected, ok.Value);
    }

    /// <summary>Export rejects unsupported formats with a 400 ApiErrorDto before touching the service.</summary>
    [Fact]
    public async Task Export_InvalidFormat_Returns400()
    {
        var (c, svc, _) = Create();
        var bad = Assert.IsType<BadRequestObjectResult>(
            await c.ExportAsync("xlsx", null, null, null, null, null, null, null, CancellationToken.None));
        var err = Assert.IsType<FinanceManager.Shared.Dtos.Common.ApiErrorDto>(bad.Value);
        Assert.Equal("Err_Invalid_format", err.code);
        svc.VerifyNoOtherCalls();
    }

    /// <summary>CSV export returns a text/csv file result with the exported rows.</summary>
    [Fact]
    public async Task Export_Csv_ReturnsFileContent()
    {
        var (c, svc, user) = Create();
        svc.Setup(s => s.ExportAsync(user.UserId, It.IsAny<McpAuditQueryFilter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Detail() });

        var file = Assert.IsType<FileContentResult>(
            await c.ExportAsync("csv", null, null, null, null, null, null, null, CancellationToken.None));

        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.EndsWith(".csv", file.FileDownloadName);
        var text = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Utc;SequenceNo;", text);
        Assert.Contains("fm_postings_query", text);
    }

    /// <summary>JSON export returns an application/json file result.</summary>
    [Fact]
    public async Task Export_Json_ReturnsFileContent()
    {
        var (c, svc, user) = Create();
        svc.Setup(s => s.ExportAsync(user.UserId, It.IsAny<McpAuditQueryFilter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Detail() });

        var file = Assert.IsType<FileContentResult>(
            await c.ExportAsync("json", null, null, null, null, null, null, null, CancellationToken.None));

        Assert.Equal("application/json; charset=utf-8", file.ContentType);
        Assert.EndsWith(".json", file.FileDownloadName);
    }

    /// <summary>Usage returns 404 for a foreign/missing access and 200 with the aggregate otherwise.</summary>
    [Fact]
    public async Task Usage_NotFound_ForForeign_And_Ok_ForOwn()
    {
        var (c, svc, user) = Create();
        var access = Guid.NewGuid();
        svc.Setup(s => s.GetAccessUsageAsync(user.UserId, access, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAccessUsageDto?)null);
        Assert.IsType<NotFoundResult>(await c.GetUsageAsync(access, 10, CancellationToken.None));

        var usage = new McpAccessUsageDto(access, 4, 3, 1, 0.25, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow,
            new[] { new McpAuditToolUsageDto("fm_postings_query", 4) },
            new[] { Item(access) });
        svc.Setup(s => s.GetAccessUsageAsync(user.UserId, access, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);
        var ok = Assert.IsType<OkObjectResult>(await c.GetUsageAsync(access, 10, CancellationToken.None));
        Assert.Same(usage, ok.Value);
    }
}
