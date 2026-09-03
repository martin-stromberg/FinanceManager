using FinanceManager.Application;
using FinanceManager.Web.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for <see cref="BackgroundTasksController"/> covering task enqueueing (including duplicate-detection
/// and its opt-out flag), and that all read/cancel operations are scoped to the requesting user's own tasks
/// so one user can never see or cancel another user's background work.
/// </summary>
public sealed class BackgroundTasksControllerTests
{
    private static (BackgroundTasksController controller, Guid userA, Guid userB, BackgroundTaskManager manager) Create()
    {
        var manager = new BackgroundTaskManager();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var sp = services.BuildServiceProvider();
        var localizer = sp.GetRequiredService<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();

        var controller = new BackgroundTasksController(manager, NullLogger<BackgroundTasksController>.Instance, localizer);
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userA.ToString()) }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, userA, userB, manager);
    }

    /// <summary>
    /// Verifies that enqueueing a task returns the created <see cref="BackgroundTaskInfo"/> owned by the
    /// current user and that it is tracked by the manager.
    /// </summary>
    [Fact]
    public void Enqueue_ShouldReturnTask()
    {
        var (controller, userA, _, manager) = Create();
        var result = controller.Enqueue(BackgroundTaskType.BackupRestore, false); // allowDuplicate false
        var ok = result.Result as OkObjectResult;
        Assert.NotNull(ok);
        var info = ok!.Value as BackgroundTaskInfo;
        Assert.NotNull(info);
        Assert.Equal(userA, info!.UserId);
        Assert.Single(manager.GetAll().Where(t => t.Id == info.Id));
    }

    /// <summary>
    /// Verifies that enqueueing the same task type twice without the duplicate flag returns the same task
    /// instance both times, preventing the same expensive job from being scheduled multiple times concurrently.
    /// </summary>
    [Fact]
    public void Enqueue_ShouldReturnExisting_WhenDuplicateNotAllowed()
    {
        var (controller, _, _, _) = Create();
        var first = (controller.Enqueue(BackgroundTaskType.BookAllDrafts, false).Result as OkObjectResult)!.Value as BackgroundTaskInfo;
        var second = (controller.Enqueue(BackgroundTaskType.BookAllDrafts, false).Result as OkObjectResult)!.Value as BackgroundTaskInfo;
        Assert.Equal(first!.Id, second!.Id); // same
    }

    /// <summary>
    /// Verifies that passing <c>allowDuplicate: true</c> lets the same task type be enqueued twice as distinct
    /// task instances, overriding the default duplicate-suppression behavior.
    /// </summary>
    [Fact]
    public void Enqueue_ShouldAllowDuplicate_WhenFlagTrue()
    {
        var (controller, _, _, _) = Create();
        var first = (controller.Enqueue(BackgroundTaskType.ClassifyAllDrafts, true).Result as OkObjectResult)!.Value as BackgroundTaskInfo;
        var second = (controller.Enqueue(BackgroundTaskType.ClassifyAllDrafts, true).Result as OkObjectResult)!.Value as BackgroundTaskInfo;
        Assert.NotEqual(first!.Id, second!.Id); // different
    }

    /// <summary>
    /// Verifies that the active/queued task listing only returns tasks belonging to the current user, even
    /// when another user has tasks enqueued in the same shared <see cref="BackgroundTaskManager"/> — a
    /// cross-user data leakage guard.
    /// </summary>
    [Fact]
    public void GetActiveAndQueued_ShouldFilterByUser()
    {
        var (controller, userA, userB, manager) = Create();
        // Enqueue task for current user (userA)
        controller.Enqueue(BackgroundTaskType.BackupRestore, false);
        // Manually enqueue for other user by bypassing controller
        manager.Enqueue(BackgroundTaskType.BookAllDrafts, userB);
        var listResult = controller.GetActiveAndQueued();
        var ok = listResult.Result as OkObjectResult;
        Assert.NotNull(ok);
        var tasks = ((System.Collections.Generic.IEnumerable<BackgroundTaskInfo>)ok!.Value!).ToList();
        // ensure all tasks belong to userA
        Assert.All(tasks, t => Assert.Equal(userA, t.UserId));
    }

    /// <summary>
    /// Verifies that cancelling a task already in the <c>Running</c> state transitions it to
    /// <c>Cancelled</c> rather than removing it outright, so its final status remains visible.
    /// </summary>
    [Fact]
    public void CancelOrRemove_ShouldCancelRunning()
    {
        var (controller, userA, _, manager) = Create();
        var info = (controller.Enqueue(BackgroundTaskType.BackupRestore, false).Result as OkObjectResult)!.Value as BackgroundTaskInfo;
        // Update to running
        manager.UpdateTaskInfo(info! with { Status = BackgroundTaskStatus.Running, StartedUtc = DateTime.UtcNow });
        var response = controller.CancelOrRemove(info!.Id);
        Assert.IsType<NoContentResult>(response);
        var updated = manager.Get(info.Id);
        Assert.NotNull(updated);
        Assert.Equal(BackgroundTaskStatus.Cancelled, updated!.Status);
    }

    /// <summary>
    /// Verifies that cancelling a task still in the queued (not yet started) state removes it entirely from
    /// the manager, since a queued task has no partial work to report.
    /// </summary>
    [Fact]
    public void CancelOrRemove_ShouldRemoveQueued()
    {
        var (controller, _, _, manager) = Create();
        var info = (controller.Enqueue(BackgroundTaskType.BookAllDrafts, false).Result as OkObjectResult)!.Value as BackgroundTaskInfo;
        var response = controller.CancelOrRemove(info!.Id);
        Assert.IsType<NoContentResult>(response);
        Assert.Null(manager.Get(info.Id));
    }

    /// <summary>
    /// Verifies that requesting task detail for a task owned by a different user returns 404 rather than
    /// exposing another user's task, even though the task ID itself is valid.
    /// </summary>
    [Fact]
    public void GetDetail_ShouldReturnNotFound_ForOtherUser()
    {
        var (controller, _, userB, manager) = Create();
        // add task for userB directly
        var otherTask = manager.Enqueue(BackgroundTaskType.ClassifyAllDrafts, userB);
        var resp = controller.GetDetail(otherTask.Id);
        Assert.IsType<NotFoundResult>(resp.Result);
    }
}
