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
            "{\"postingIds\":[\"x\"]}", null, Guid.NewGuid(), 0, null);

    /// <summary>The list path forwards the access id and status filter and reports the rows.</summary>
    [Fact]
    public async Task TaskStatus_ListsRequests_OfCallingAccess_WithStatusFilter()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.ListAsync(owner, AiActionRequestStatus.Pending, accessId, null, 0, 100,
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
            owner, "ok", null, null, null, Guid.NewGuid(), "k1", 0, null);
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

    /// <summary>A failed request surfaces the engine's classified error envelope as
    /// <c>untrusted_data.error</c> — the defined code/message pair of a defined failure —
    /// instead of a result reference.</summary>
    [Fact]
    public async Task TaskStatus_RequestDetail_FailedDefinedError_ExposesErrorObject()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var detail = new AiActionRequestDetailDto(requestId, accessId, "acc", "fm_admin_backups",
            "create", "katalog-v1", "{}", null, (short)AiActionRequestStatus.Failed,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(70), DateTime.UtcNow.AddHours(-1),
            owner, null, DateTime.UtcNow.AddHours(-1),
            "{\"error\":{\"code\":\"Err_Mcp_InvalidState\",\"message\":\"Invalid 'backupId'.\"}}",
            "Err_Mcp_InvalidState", Guid.NewGuid(), "k1", 1, null);
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.GetAsync(owner, requestId, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

        var handler = NewHandler(approvals.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse($"{{\"requestId\":\"{requestId}\"}}").RootElement,
            new McpToolCallContext(NewContext(owner, accessId).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var item = doc.RootElement.GetProperty("requests")[0];
        Assert.Equal("Failed", item.GetProperty("status").GetString());
        Assert.Equal("Err_Mcp_InvalidState", item.GetProperty("failureReason").GetString());
        var untrusted = item.GetProperty("untrusted_data");
        var error = untrusted.GetProperty("error");
        Assert.Equal("Err_Mcp_InvalidState", error.GetProperty("code").GetString());
        Assert.Equal("Invalid 'backupId'.", error.GetProperty("message").GetString());
        // The error envelope replaces the result reference — it is not a result.
        Assert.Equal(JsonValueKind.Null, untrusted.GetProperty("resultRef").ValueKind);
        Assert.False(item.TryGetProperty("error", out _));
    }

    /// <summary>A foreign exception failure surfaces only the bare exception type name —
    /// no message and no stack — under <c>untrusted_data.error.type</c>.</summary>
    [Fact]
    public async Task TaskStatus_RequestDetail_FailedForeignError_ExposesTypeNameOnly()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var detail = new AiActionRequestDetailDto(requestId, accessId, "acc", "fm_admin_backups",
            "create", "katalog-v1", "{}", null, (short)AiActionRequestStatus.Failed,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(70), DateTime.UtcNow.AddHours(-1),
            owner, null, DateTime.UtcNow.AddHours(-1),
            "{\"error\":{\"type\":\"ArgumentException\"}}",
            "Err_Internal", Guid.NewGuid(), "k1", 1, null);
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.GetAsync(owner, requestId, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

        var handler = NewHandler(approvals.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse($"{{\"requestId\":\"{requestId}\"}}").RootElement,
            new McpToolCallContext(NewContext(owner, accessId).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var untrusted = doc.RootElement.GetProperty("requests")[0].GetProperty("untrusted_data");
        var error = untrusted.GetProperty("error");
        Assert.Equal("ArgumentException", error.GetProperty("type").GetString());
        Assert.False(error.TryGetProperty("message", out _));
        Assert.False(error.TryGetProperty("code", out _));
        Assert.Equal(JsonValueKind.Null, untrusted.GetProperty("resultRef").ValueKind);
    }

    /// <summary>A successful execution whose payload happens to contain an <c>error</c>
    /// property keeps it as the plain result reference — the envelope is only recognized
    /// on failed requests.</summary>
    [Fact]
    public async Task TaskStatus_RequestDetail_ExecutedWithErrorProperty_KeepsResultRef()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var detail = new AiActionRequestDetailDto(requestId, accessId, "acc", "fm_x",
            "run", "katalog-v1", "{}", null, (short)AiActionRequestStatus.Executed,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(70), DateTime.UtcNow.AddHours(-1),
            owner, null, DateTime.UtcNow.AddHours(-1),
            "{\"error\":\"soft warning\",\"done\":true}",
            null, Guid.NewGuid(), "k1", 1, null);
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.GetAsync(owner, requestId, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

        var handler = NewHandler(approvals.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse($"{{\"requestId\":\"{requestId}\"}}").RootElement,
            new McpToolCallContext(NewContext(owner, accessId).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var untrusted = doc.RootElement.GetProperty("requests")[0].GetProperty("untrusted_data");
        Assert.Equal("soft warning", untrusted.GetProperty("resultRef").GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, untrusted.GetProperty("error").ValueKind);
    }

    /// <summary>The list projection carries the same error object for failed requests.</summary>
    [Fact]
    public async Task TaskStatus_List_FailedRequest_ExposesErrorObject()
    {
        var owner = Guid.NewGuid();
        var accessId = Guid.NewGuid();
        var item = new AiActionRequestListItemDto(Guid.NewGuid(), accessId, "acc", "fm_admin_backups",
            "create", (short)AiActionRequestStatus.Failed, DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(71), DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-20),
            "{\"error\":{\"type\":\"SqliteException\"}}", "Err_Internal", Guid.NewGuid(), 1, null);
        var approvals = new Mock<IMcpApprovalService>();
        approvals.Setup(a => a.ListAsync(owner, null, accessId, null, 0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var handler = NewHandler(approvals.Object);
        var result = await handler.InvokeAsync(
            JsonDocument.Parse("{}").RootElement,
            new McpToolCallContext(NewContext(owner, accessId).Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null),
            CancellationToken.None);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var untrusted = doc.RootElement.GetProperty("requests")[0].GetProperty("untrusted_data");
        Assert.Equal("SqliteException", untrusted.GetProperty("error").GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, untrusted.GetProperty("resultRef").ValueKind);
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
            null, null, null, null, null, null, Guid.NewGuid(), "k1", 0, null);
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
