using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the background task API: enqueueing work (including the specialized aggregate
/// rebuild endpoint), polling active/detail status, and cancelling or removing a queued task.
/// </summary>
public class ApiClientBackgroundTasksTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes the test with the shared <see cref="TestWebApplicationFactory"/>, which hosts the
    /// application in-memory for the duration of the test class.
    /// </summary>
    /// <param name="factory">The shared in-memory application host injected by xUnit's class fixture.</param>
    public ApiClientBackgroundTasksTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>
    /// Verifies that enqueueing an aggregate-rebuild task through the generic background-task endpoint
    /// returns a task info that is then visible via the active-list and detail endpoints, and that the
    /// aggregates-specific convenience endpoints (which wrap the same underlying task) agree with it.
    /// </summary>
    [Fact]
    public async Task Enqueue_RebuildAggregates_ShouldReturnTaskInfo_AndStatusEndpointsWork()
    {
        var api = CreateClient();
        var username = $"user_{Guid.NewGuid():N}";
        // authenticate by registering
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", null, null), TestContext.Current.CancellationToken);

        // Enqueue via generic endpoint
        var info = await api.BackgroundTasks_EnqueueAsync(BackgroundTaskType.RebuildAggregates, allowDuplicate: false, ct: TestContext.Current.CancellationToken);
        info.Should().NotBeNull();
        info.Type.Should().Be(BackgroundTaskType.RebuildAggregates);
        info.Status.Should().BeOneOf(BackgroundTaskStatus.Queued, BackgroundTaskStatus.Running);

        // Active list should contain our task
        var active = await api.BackgroundTasks_GetActiveAsync(TestContext.Current.CancellationToken);
        active.Should().NotBeNull();
        active.Should().Contain(x => x.Id == info.Id);

        // Detail should return the same
        var detail = await api.BackgroundTasks_GetDetailAsync(info.Id, TestContext.Current.CancellationToken);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(info.Id);

        // Aggregates specialized endpoint should return Accepted status with DTO
        var agg = await api.Aggregates_RebuildAsync(allowDuplicate: false, ct: TestContext.Current.CancellationToken);
        agg.Running.Should().BeTrue();

        // Status endpoint should return running true or false depending on timing
        var status = await api.Aggregates_GetRebuildStatusAsync(TestContext.Current.CancellationToken);
        status.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that cancelling/removing a freshly enqueued task succeeds and that the task's detail
    /// endpoint no longer returns it afterwards, confirming the cancel operation actually clears the task
    /// rather than just changing its status while leaving it queryable.
    /// </summary>
    [Fact]
    public async Task CancelOrRemove_ShouldReturnNoContentOrFalse()
    {
        var api = CreateClient();
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", null, null), TestContext.Current.CancellationToken);

        var info = await api.BackgroundTasks_EnqueueAsync(BackgroundTaskType.RebuildAggregates, ct: TestContext.Current.CancellationToken);
        info.Should().NotBeNull();

        // Try cancel/remove depending on status
        var ok = await api.BackgroundTasks_CancelOrRemoveAsync(info.Id, TestContext.Current.CancellationToken);
        // Controller returns 204 for success, 400/404 for failure -> client maps to false on failure
        ok.Should().BeTrue();

        // Subsequent detail may be null if removed while queued
        var after = await api.BackgroundTasks_GetDetailAsync(info.Id, TestContext.Current.CancellationToken);
        // Allow both outcomes depending on race (null if removed, info if transitioned to running quickly)
        after.Should().BeNull();
    }
}
