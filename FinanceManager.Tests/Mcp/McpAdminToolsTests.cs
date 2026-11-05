using FinanceManager.Application.Backups;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Application.WellKnown;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Users;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Backups;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using FinanceManager.Web.Services.Updates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the administrative tool set T34–T38 (<c>fm_admin_*</c>): the admin gate
/// (<c>Err_Mcp_AdminRequired</c> for non-admin owners), the (Administration, …) scope
/// declarations, the approval gating of the final/effective sub-operations
/// (install/schedule, create, set) incl. their approved-execution path, the direct
/// notification dismissal and the <c>untrusted_data</c> marking of all free texts.
/// </summary>
public sealed class McpAdminToolsTests
{
    private static readonly Guid AdminOwner = Guid.NewGuid();

    private static McpAccess NewAccess(Guid owner, McpAccessLevel level = McpAccessLevel.Buchhalter)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", level, McpApprovalMode.PerAction);

    private static Mock<IMcpAccessContext> NewAccessContext(Guid owner, McpAccess? access = null)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);
        ctx.SetupGet(c => c.Access).Returns(access ?? NewAccess(owner));
        return ctx;
    }

    private static McpToolCallContext NewCallContext(
        IMcpAccessContext access, McpToolExecutionMode mode = McpToolExecutionMode.Interactive)
        => new(access, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null, mode);

    private static Mock<UserManager<User>> NewUsers(bool isAdmin = true, Guid? owner = null)
    {
        var store = new Mock<IUserStore<User>>();
        var users = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var adminOwner = owner ?? AdminOwner;
        users.Setup(u => u.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) =>
                Guid.TryParse(id, out var g) ? new User("owner") { Id = g } : null);
        users.Setup(u => u.IsInRoleAsync(It.IsAny<User>(), "Admin"))
            .ReturnsAsync((User u, string _) => isAdmin && u.Id == adminOwner);
        return users;
    }

    private static Mock<IMcpScopeEvaluator> NewScopeEvaluator(bool allowed = true)
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(),
                It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowed);
        return evaluator;
    }

    private static Mock<IMcpAuditWriter> NewAuditWriter()
    {
        var writer = new Mock<IMcpAuditWriter>();
        writer.Setup(w => w.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAuditEntry?)null);
        return writer;
    }

    private static Mock<IMcpDownloadTicketService> NewTickets(string token = "testtoken")
    {
        var tickets = new Mock<IMcpDownloadTicketService>();
        tickets.Setup(t => t.Issue(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<TimeSpan?>()))
            .Returns((Guid o, Guid b, Guid? a, TimeSpan? l) =>
                new McpDownloadTicket(token, DateTime.UtcNow.Add(l ?? TimeSpan.FromMinutes(10))));
        return tickets;
    }

    private static IHttpContextAccessor NewHttpContextAccessor()
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("fm.example.test");
        return new HttpContextAccessor { HttpContext = http };
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonDocument Serialize(object result) => JsonDocument.Parse(JsonSerializer.Serialize(result));

    private static UpdateStatusDto NewStatus(UpdateStatusKind kind = UpdateStatusKind.Ready)
        => new(kind, "1.2.3", null, "1.3.0", "win-x64", DateTime.UtcNow.AddHours(-1), null,
            "fm-win-x64.zip", false, null, new TimeOnly(3, 0),
            new UpdateMetadataDto("1.3.0", "release notes <b>", DateTime.UtcNow.AddDays(-2),
                "owner", "repo", new List<UpdateAssetDto>()));

    private static UpdateSettingsDto NewUpdateSettings()
        => new(true, "owner", "repo", "manifest.json", new TimeOnly(1, 0), new TimeOnly(5, 0),
            new TimeOnly(3, 0), null, null, "work", 60, false);

    // ------------------------------------------------------------------
    // Catalog registration
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public void Registry_AcceptsTheAdminCatalog()
    {
        var registry = new McpToolRegistry(new IMcpToolDescriptor[]
        {
            new FmAdminStatusDescriptor(),
            new FmAdminUpdatesDescriptor(),
            new FmAdminBackupsDescriptor(),
            new FmAdminNotificationsDescriptor(),
            new FmAdminSettingsDescriptor()
        });

        Assert.Equal(5, registry.Tools.Count);
        Assert.All(registry.Tools, t =>
        {
            Assert.StartsWith("fm_admin_", t.Name);
            var scope = Assert.Single(t.RequiredScopes);
            Assert.Equal(McpDataType.Administration, scope.DataType);
            Assert.Equal(McpRightKind.Read, scope.Right);
            Assert.Equal(JsonValueKind.Object, t.InputSchema.RootElement.ValueKind);
        });
        Assert.Equal(McpToolSecurityClass.Lesend,
            registry.Find(FmAdminStatusDescriptor.ToolName)?.SecurityClass);
        Assert.All(
            new[]
            {
                FmAdminUpdatesDescriptor.ToolName, FmAdminBackupsDescriptor.ToolName,
                FmAdminNotificationsDescriptor.ToolName, FmAdminSettingsDescriptor.ToolName
            },
            name => Assert.Equal(McpToolSecurityClass.Schreibvorbereitend,
                registry.Find(name)?.SecurityClass));
    }

    // ------------------------------------------------------------------
    // Admin gate
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminGate_DeniesNonAdminOwner_ForEveryAdminTool()
    {
        var owner = Guid.NewGuid();
        var users = NewUsers(isAdmin: false);
        var updates = new Mock<IUpdateOrchestrator>();
        var backups = new Mock<IBackupService>();
        var notifications = new Mock<INotificationService>();
        var evaluator = NewScopeEvaluator();
        var writer = NewAuditWriter();
        var ctx = NewCallContext(NewAccessContext(owner).Object);
        var ct = TestContext.Current.CancellationToken;

        var handlers = new IMcpToolHandler[]
        {
            new FmAdminStatusHandler(users.Object, new Mock<IInstalledReleaseMetadataProvider>().Object,
                updates.Object, backups.Object, notifications.Object, new TestMcpSettingsService()),
            new FmAdminUpdatesHandler(users.Object, updates.Object, evaluator.Object, writer.Object),
            new FmAdminBackupsHandler(users.Object, backups.Object, NewTickets().Object,
                NewHttpContextAccessor(), evaluator.Object, writer.Object),
            new FmAdminNotificationsHandler(users.Object, notifications.Object, evaluator.Object, writer.Object),
            new FmAdminSettingsHandler(users.Object, new TestMcpSettingsService(), updates.Object,
                new Mock<IWellKnownSettingsService>().Object, evaluator.Object, writer.Object)
        };

        foreach (var handler in handlers)
        {
            var ex = await Assert.ThrowsAsync<McpToolException>(
                () => handler.InvokeAsync(Args("{}"), ctx, ct));
            Assert.Equal("Err_Mcp_AdminRequired", ex.Code);
        }
        // Nothing was touched on the domain services.
        updates.Verify(u => u.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
        backups.Verify(b => b.ListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // fm_admin_status
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminStatus_ReturnsAggregatedInstallationStatus()
    {
        var users = NewUsers();
        var release = new Mock<IInstalledReleaseMetadataProvider>();
        release.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledReleaseMetadataDto("1.2.3", null, "abc", "owner/repo", "win-x64"));
        var updates = new Mock<IUpdateOrchestrator>();
        updates.Setup(u => u.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(NewStatus());
        var backups = new Mock<IBackupService>();
        backups.Setup(b => b.ListAsync(AdminOwner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BackupDto>
            {
                new() { Id = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow.AddDays(-1), FileName = "b1.zip", SizeBytes = 10, Source = "System" },
                new() { Id = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow, FileName = "b2.zip", SizeBytes = 20, Source = "System" }
            });
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.ListActiveAsync(AdminOwner, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationDto>
            {
                new(Guid.NewGuid(), "t", "m", 1, 0, DateTime.UtcNow, false, DateTime.UtcNow, null)
            });

        var handler = new FmAdminStatusHandler(users.Object, release.Object, updates.Object,
            backups.Object, notifications.Object, new TestMcpSettingsService(enabled: true, writeAccessEnabled: false));
        var result = await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.Equal("1.2.3", doc.RootElement.GetProperty("installation").GetProperty("version").GetString());
        Assert.Equal("Ready", doc.RootElement.GetProperty("update").GetProperty("status").GetString());
        Assert.Equal("1.3.0", doc.RootElement.GetProperty("update").GetProperty("availableVersion").GetString());
        var backupsEl = doc.RootElement.GetProperty("backups");
        Assert.Equal(2, backupsEl.GetProperty("count").GetInt32());
        Assert.Equal("b2.zip", backupsEl.GetProperty("latest").GetProperty("untrusted_data").GetProperty("fileName").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("notifications").GetProperty("activeCount").GetInt32());
        Assert.False(doc.RootElement.GetProperty("mcp").GetProperty("writeAccessEnabled").GetBoolean());
    }

    // ------------------------------------------------------------------
    // fm_admin_updates
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminUpdates_StatusAndCheck_ReadThroughTheOrchestrator()
    {
        var users = NewUsers();
        var updates = new Mock<IUpdateOrchestrator>();
        updates.Setup(u => u.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(NewStatus());
        updates.Setup(u => u.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResultDto(true, NewStatus(), "check message"));
        var handler = new FmAdminUpdatesHandler(users.Object, updates.Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var ctx = NewCallContext(NewAccessContext(AdminOwner).Object);
        var ct = TestContext.Current.CancellationToken;

        using var statusDoc = Serialize(await handler.InvokeAsync(Args("{}"), ctx, ct));
        Assert.Equal("Ready", statusDoc.RootElement.GetProperty("update").GetProperty("status").GetString());

        using var checkDoc = Serialize(await handler.InvokeAsync(Args("{\"operation\":\"check\"}"), ctx, ct));
        Assert.True(checkDoc.RootElement.GetProperty("updateAvailable").GetBoolean());
        Assert.Equal("check message",
            checkDoc.RootElement.GetProperty("untrusted_data").GetProperty("message").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminUpdates_Install_RequiresConfirmDowntime()
    {
        var handler = new FmAdminUpdatesHandler(NewUsers().Object, new Mock<IUpdateOrchestrator>().Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args("{\"operation\":\"install\"}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminUpdates_Install_InteractiveAnswersApprovalRequired()
    {
        var updates = new Mock<IUpdateOrchestrator>();
        var handler = new FmAdminUpdatesHandler(NewUsers().Object, updates.Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await handler.InvokeAsync(
            Args("{\"operation\":\"install\",\"confirmDowntime\":true}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.True(doc.RootElement.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal("Err_Mcp_ApprovalRequired", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("install", doc.RootElement.GetProperty("operation").GetString());
        updates.Verify(u => u.StartInstallAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminUpdates_Install_ExecutesOnApprovedExecution()
    {
        var updates = new Mock<IUpdateOrchestrator>();
        updates.Setup(u => u.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(NewStatus());
        updates.Setup(u => u.StartInstallAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewStatus(UpdateStatusKind.Installing));
        var handler = new FmAdminUpdatesHandler(NewUsers().Object, updates.Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await handler.InvokeAsync(
            Args("{\"operation\":\"install\",\"confirmDowntime\":true}"),
            NewCallContext(NewAccessContext(AdminOwner).Object, McpToolExecutionMode.ApprovedExecution),
            TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal("Installing", doc.RootElement.GetProperty("update").GetProperty("status").GetString());
        updates.Verify(u => u.StartInstallAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminUpdates_Install_DeniedWithoutModifyScope()
    {
        var handler = new FmAdminUpdatesHandler(NewUsers().Object, new Mock<IUpdateOrchestrator>().Object,
            NewScopeEvaluator(allowed: false).Object, NewAuditWriter().Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args("{\"operation\":\"install\",\"confirmDowntime\":true}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken));
        Assert.Equal("Err_Mcp_ScopeDenied", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminUpdates_Schedule_ExecutesOnApprovedExecution()
    {
        var updates = new Mock<IUpdateOrchestrator>();
        updates.Setup(u => u.ScheduleAsync(new TimeOnly(4, 30), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewUpdateSettings());
        var handler = new FmAdminUpdatesHandler(NewUsers().Object, updates.Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await handler.InvokeAsync(
            Args("{\"operation\":\"schedule\",\"scheduledInstallTime\":\"04:30\"}"),
            NewCallContext(NewAccessContext(AdminOwner).Object, McpToolExecutionMode.ApprovedExecution),
            TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        updates.Verify(u => u.ScheduleAsync(new TimeOnly(4, 30), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // fm_admin_backups
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminBackups_List_PagesOwnerBackupsWithUntrustedNames()
    {
        var backups = new Mock<IBackupService>();
        backups.Setup(b => b.ListAsync(AdminOwner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 3).Select(i => new BackupDto
            {
                Id = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow.AddDays(-i),
                FileName = $"b{i}.zip",
                SizeBytes = 100 + i,
                Source = "System"
            }).ToList());
        var handler = new FmAdminBackupsHandler(NewUsers().Object, backups.Object,
            NewTickets().Object, NewHttpContextAccessor(),
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await handler.InvokeAsync(Args("{\"take\":2}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.Equal(3, doc.RootElement.GetProperty("totalCount").GetInt32());
        var list = doc.RootElement.GetProperty("backups");
        Assert.Equal(2, list.GetArrayLength());
        Assert.False(list[0].TryGetProperty("fileName", out _));
        Assert.Equal("b0.zip", list[0].GetProperty("untrusted_data").GetProperty("fileName").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminBackups_Create_RunsThroughApprovalAndExecutes()
    {
        var created = new BackupDto
        {
            Id = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            FileName = "new.zip",
            SizeBytes = 5,
            Source = "System"
        };
        var backups = new Mock<IBackupService>();
        backups.Setup(b => b.CreateAsync(AdminOwner, "KI", It.IsAny<CancellationToken>())).ReturnsAsync(created);
        var handler = new FmAdminBackupsHandler(NewUsers().Object, backups.Object,
            NewTickets().Object, NewHttpContextAccessor(),
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var ct = TestContext.Current.CancellationToken;

        using var queued = Serialize(await handler.InvokeAsync(Args("{\"operation\":\"create\"}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), ct));
        Assert.Equal("Err_Mcp_ApprovalRequired", queued.RootElement.GetProperty("code").GetString());
        backups.Verify(b => b.CreateAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        using var exec = Serialize(await handler.InvokeAsync(Args("{\"operation\":\"create\"}"),
            NewCallContext(NewAccessContext(AdminOwner).Object, McpToolExecutionMode.ApprovedExecution), ct));
        Assert.True(exec.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal(created.Id, exec.RootElement.GetProperty("backup").GetProperty("id").GetGuid());
        // MCP-originated snapshots are tagged source "KI" (REST/Program callers keep "System").
        backups.Verify(b => b.CreateAsync(AdminOwner, "KI", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test for the Guid.Empty defect: the real audit writer routes every field
    /// delta through the <c>McpAuditEntryChange</c> ctor which throws for empty entity ids.
    /// The guarding fake replays that guard — the approved create must stage the real
    /// backup id and still answer <c>applied</c>.
    /// </summary>
    [Fact]
    public async Task AdminBackups_Create_ApprovedExecution_StagesRealRecordId()
    {
        var created = new BackupDto
        {
            Id = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            FileName = "new.zip",
            SizeBytes = 5,
            Source = "System"
        };
        var backups = new Mock<IBackupService>();
        backups.Setup(b => b.CreateAsync(AdminOwner, "KI", It.IsAny<CancellationToken>())).ReturnsAsync(created);
        var audit = new GuardingAuditWriter();
        var handler = new FmAdminBackupsHandler(NewUsers().Object, backups.Object,
            NewTickets().Object, NewHttpContextAccessor(),
            NewScopeEvaluator().Object, audit);
        var result = await handler.InvokeAsync(Args("{\"operation\":\"create\"}"),
            NewCallContext(NewAccessContext(AdminOwner).Object, McpToolExecutionMode.ApprovedExecution),
            TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.NotNull(audit.LastRequest);
        var change = Assert.Single(audit.LastRequest!.Changes!);
        Assert.Equal(created.Id, change.EntityId);
        Assert.Equal("BackupCreated", audit.LastRequest.EventType);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminBackups_Download_ReturnsTicketedUrlAndMetadata()
    {
        var backup = new BackupDto
        {
            Id = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            FileName = "b.zip",
            SizeBytes = 1234,
            Source = "System"
        };
        var backups = new Mock<IBackupService>();
        backups.Setup(b => b.GetAsync(AdminOwner, backup.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backup);
        var tickets = NewTickets("tok-abc");
        var handler = new FmAdminBackupsHandler(NewUsers().Object, backups.Object,
            tickets.Object, NewHttpContextAccessor(),
            NewScopeEvaluator().Object, NewAuditWriter().Object);

        var result = await handler.InvokeAsync(
            Args($"{{\"operation\":\"download\",\"id\":\"{backup.Id}\"}}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.Equal("download", doc.RootElement.GetProperty("operation").GetString());
        Assert.Equal("https://fm.example.test/mcp/download/tok-abc",
            doc.RootElement.GetProperty("downloadUrl").GetString());
        Assert.Equal(1234, doc.RootElement.GetProperty("sizeBytes").GetInt64());
        Assert.True(doc.RootElement.TryGetProperty("expiresUtc", out _));
        Assert.Equal("b.zip",
            doc.RootElement.GetProperty("untrusted_data").GetProperty("fileName").GetString());
        // The ticket binds owner + backup (and the causing access for audit attribution).
        tickets.Verify(t => t.Issue(AdminOwner, backup.Id, It.IsAny<Guid?>(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminBackups_Download_UnknownOrForeignId_Fails()
    {
        var backups = new Mock<IBackupService>();
        backups.Setup(b => b.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackupDto?)null);
        var tickets = NewTickets();
        var handler = new FmAdminBackupsHandler(NewUsers().Object, backups.Object,
            tickets.Object, NewHttpContextAccessor(),
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args($"{{\"operation\":\"download\",\"id\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), ct));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
        tickets.Verify(t => t.Issue(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<TimeSpan?>()), Times.Never);

        var missingId = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args("{\"operation\":\"download\"}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), ct));
        Assert.Equal("Err_Mcp_InvalidArguments", missingId.Code);
    }

    // ------------------------------------------------------------------
    // fm_admin_notifications
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminNotifications_List_ReturnsOwnActiveWithUntrustedTexts()
    {
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.ListActiveAsync(AdminOwner, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationDto>
            {
                new(Guid.NewGuid(), "title <ignore>", "msg", 1, 0, DateTime.UtcNow, false, DateTime.UtcNow, "key")
            });
        var handler = new FmAdminNotificationsHandler(NewUsers().Object, notifications.Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        var item = doc.RootElement.GetProperty("notifications")[0];
        Assert.False(item.TryGetProperty("title", out _));
        Assert.Equal("title <ignore>", item.GetProperty("untrusted_data").GetProperty("title").GetString());
        Assert.Equal("key", item.GetProperty("untrusted_data").GetProperty("triggerEventKey").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminNotifications_Dismiss_ExecutesDirectlyWithModifyScope()
    {
        var id = Guid.NewGuid();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.DismissAsync(id, AdminOwner, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new FmAdminNotificationsHandler(NewUsers().Object, notifications.Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await handler.InvokeAsync(
            Args($"{{\"operation\":\"dismiss\",\"notificationId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        notifications.Verify(n => n.DismissAsync(id, AdminOwner, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminNotifications_Dismiss_WithoutModifyScopeIsDenied_AndUnknownIdIsNotFound()
    {
        var id = Guid.NewGuid();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.DismissAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var ct = TestContext.Current.CancellationToken;

        var denied = new FmAdminNotificationsHandler(NewUsers().Object, notifications.Object,
            NewScopeEvaluator(allowed: false).Object, NewAuditWriter().Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => denied.InvokeAsync(
            Args($"{{\"operation\":\"dismiss\",\"notificationId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), ct));
        Assert.Equal("Err_Mcp_ScopeDenied", ex.Code);

        var missing = new FmAdminNotificationsHandler(NewUsers().Object, notifications.Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await missing.InvokeAsync(
            Args($"{{\"operation\":\"dismiss\",\"notificationId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), ct);
        using var doc = Serialize(result);
        Assert.False(doc.RootElement.GetProperty("applied").GetBoolean());
    }

    // ------------------------------------------------------------------
    // fm_admin_settings
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminSettings_Get_ReturnsAllSections()
    {
        var updates = new Mock<IUpdateOrchestrator>();
        updates.Setup(u => u.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(NewUpdateSettings());
        var wellKnown = new Mock<IWellKnownSettingsService>();
        wellKnown.Setup(w => w.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WellKnownSettingsDto { ChangePasswordUrl = "/change-password" });
        var handler = new FmAdminSettingsHandler(NewUsers().Object,
            new TestMcpSettingsService(enabled: true, writeAccessEnabled: true),
            updates.Object, wellKnown.Object, NewScopeEvaluator().Object, NewAuditWriter().Object);
        var result = await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), TestContext.Current.CancellationToken);

        using var doc = Serialize(result);
        Assert.True(doc.RootElement.GetProperty("mcp").GetProperty("enabled").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("updates").GetProperty("enabled").GetBoolean());
        Assert.Equal("/change-password",
            doc.RootElement.GetProperty("wellKnown").GetProperty("untrusted_data").GetProperty("changePasswordUrl").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminSettings_Set_ValidatesTheBoundedFieldSubset()
    {
        var handler = new FmAdminSettingsHandler(NewUsers().Object, new TestMcpSettingsService(),
            new Mock<IUpdateOrchestrator>().Object, new Mock<IWellKnownSettingsService>().Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var ctx = NewCallContext(NewAccessContext(AdminOwner).Object);
        var ct = TestContext.Current.CancellationToken;

        var noField = await Assert.ThrowsAsync<McpToolException>(
            () => handler.InvokeAsync(Args("{\"operation\":\"set\",\"section\":\"mcp\"}"), ctx, ct));
        Assert.Equal("Err_Mcp_InvalidArguments", noField.Code);

        var wrongField = await Assert.ThrowsAsync<McpToolException>(
            () => handler.InvokeAsync(
                Args("{\"operation\":\"set\",\"section\":\"mcp\",\"includePrereleases\":true}"), ctx, ct));
        Assert.Equal("Err_Mcp_InvalidArguments", wrongField.Code);

        var badUrl = await Assert.ThrowsAsync<McpToolException>(
            () => handler.InvokeAsync(
                Args("{\"operation\":\"set\",\"section\":\"wellKnown\",\"changePasswordUrl\":\"javascript:alert(1)\"}"), ctx, ct));
        Assert.Equal("Err_Mcp_InvalidArguments", badUrl.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AdminSettings_Set_McpSection_RunsThroughApprovalAndApplies()
    {
        var settings = new TestMcpSettingsService(enabled: true, writeAccessEnabled: true);
        var handler = new FmAdminSettingsHandler(NewUsers().Object, settings,
            new Mock<IUpdateOrchestrator>().Object, new Mock<IWellKnownSettingsService>().Object,
            NewScopeEvaluator().Object, NewAuditWriter().Object);
        var ct = TestContext.Current.CancellationToken;

        using var queued = Serialize(await handler.InvokeAsync(
            Args("{\"operation\":\"set\",\"section\":\"mcp\",\"writeAccessEnabled\":false}"),
            NewCallContext(NewAccessContext(AdminOwner).Object), ct));
        Assert.Equal("Err_Mcp_ApprovalRequired", queued.RootElement.GetProperty("code").GetString());
        Assert.True(settings.WriteAccessEnabled); // no effect before approval

        using var exec = Serialize(await handler.InvokeAsync(
            Args("{\"operation\":\"set\",\"section\":\"mcp\",\"writeAccessEnabled\":false}"),
            NewCallContext(NewAccessContext(AdminOwner).Object, McpToolExecutionMode.ApprovedExecution), ct));
        Assert.True(exec.RootElement.GetProperty("applied").GetBoolean());
        Assert.False(settings.WriteAccessEnabled);
        Assert.True(settings.Enabled); // unspecified field preserved
    }
}
