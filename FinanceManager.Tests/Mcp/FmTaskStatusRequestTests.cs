using FinanceManager.Application;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Tests of the <c>fm_task_status</c> AiActionRequest view (T33 full form, F-0905): the
/// request list is restricted to requests of the calling access, the detail path returns
/// the full projection for an own id and nothing for a foreign one, and parameter snapshot,
/// access name and result reference live in the marked <c>untrusted_data</c> wrapper.
/// </summary>
public sealed class FmTaskStatusRequestTests
{
    private static Mock<IMcpAccessContext> NewContext(Guid owner, Guid accessId)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);
        ctx.SetupGet(c => c.McpAccessId).Returns(accessId);
        return ctx;
    }

    private static FmTaskStatusHandler NewHandler(IMcpApprovalService approvals)
        => new(Mock.Of<IBackgroundTaskManager>(m => m.GetAll() == new List<BackgroundTaskInfo>()), approvals);

    private static AiActionRequestListItemDto ListItem(Guid accessId, short status = 0)
        => new(Guid.NewGuid(), accessId, "acc", "fm_contacts_manage", "delete", status,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(71), null, null,
            "{\"postingIds\":[\"x\"]}", null, Guid.NewGuid(), 0);

    /// <summary>The list path forwards the access id and status filter and reports the rows.</summary>
    [Fact]
    public async Task TaskStatus_ListsRequests_OfCallingAccess_WithStatusFilter()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.ListAsync(owner, AiActionRequestStatus.Pending, accessId, 0, 100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListItem(accessId) });

        var handler = NewHandler(approvals.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse("{\"status\":\"Pending\"}").RootElement,
            new McpToolCallContext(NewContext(owner, accessId).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var requests = doc.RootElement.GetProperty("requests");
        Assert.Equal(1, requests.GetArrayLength());
        var item = requests[0];
        Assert.Equal("fm_contacts_manage", item.GetProperty("toolName").GetString());
        Assert.Equal("delete", item.GetProperty("actionKey").GetString());
        Assert.Equal("Pending", item.GetProperty("status").GetString());
        Assert.True(item.GetProperty("expiresUtc").GetDateTime() > DateTime.UtcNow);
        // Result reference and access name are untrusted content, not top-level fields.
        Assert.False(item.TryGetProperty("mcpAccessName", out _));
        Assert.False(item.TryGetProperty("resultRef", out _));
        Assert.Equal("acc", item.GetProperty("untrusted_data").GetProperty("mcpAccessName").GetString());
        Assert.Equal("x", item.GetProperty("untrusted_data").GetProperty("resultRef").GetProperty("postingIds")[0].GetString());
        approvals.VerifyAll();
    }

    /// <summary>The requestId path returns the detail projection incl. parameter snapshot.</summary>
    [Fact]
    public async Task TaskStatus_RequestDetail_ReturnsFullProjection()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var detail = new AiActionRequestDetailDto(requestId, accessId, "acc", "fm_contacts_manage",
            "delete", "katalog-v1", "{\"contactId\":\"c1\"}", "please clean", 1,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(70), DateTime.UtcNow.AddHours(-1),
            owner, "ok", null, null, null, Guid.NewGuid(), "k1", 0);
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.GetAsync(owner, requestId, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

        var handler = NewHandler(approvals.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse($"{{\"requestId\":\"{requestId}\"}}").RootElement,
            new McpToolCallContext(NewContext(owner, accessId).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var requests = doc.RootElement.GetProperty("requests");
        Assert.Equal(1, requests.GetArrayLength());
        var item = requests[0];
        Assert.Equal(requestId, item.GetProperty("requestId").GetGuid());
        Assert.Equal("Approved", item.GetProperty("status").GetString());
        var untrusted = item.GetProperty("untrusted_data");
        Assert.Equal("c1", untrusted.GetProperty("parameterSnapshot").GetProperty("contactId").GetString());
        Assert.Equal("please clean", untrusted.GetProperty("reasonText").GetString());
        Assert.Equal("ok", untrusted.GetProperty("decisionNote").GetString());
        Assert.False(item.TryGetProperty("parameterSnapshot", out _));
    }

    /// <summary>A request id of a different access behaves like a missing one — empty list,
    /// no forbidden/not-found distinction.</summary>
    [Fact]
    public async Task TaskStatus_RequestDetail_ForeignAccess_ReturnsEmpty()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var detail = new AiActionRequestDetailDto(requestId, Guid.NewGuid(), "other", "fm_x",
            "delete", "katalog-v1", "{}", null, 0, DateTime.UtcNow, DateTime.UtcNow.AddHours(72),
            null, null, null, null, null, null, Guid.NewGuid(), "k1", 0);
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.GetAsync(owner, requestId, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

        var handler = NewHandler(approvals.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse($"{{\"requestId\":\"{requestId}\"}}").RootElement,
            new McpToolCallContext(NewContext(owner, accessId).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(0, doc.RootElement.GetProperty("requests").GetArrayLength());
    }

    /// <summary>An invalid status filter is rejected with the stable argument error code.</summary>
    [Fact]
    public async Task TaskStatus_InvalidStatusFilter_ThrowsInvalidArguments()
    {
        var owner = Guid.NewGuid();
        var handler = NewHandler(Mock.Of<IMcpApprovalService>());
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            JsonDocument.Parse("{\"status\":\"Whatever\"}").RootElement,
            new McpToolCallContext(NewContext(owner, Guid.NewGuid()).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
    }
}
