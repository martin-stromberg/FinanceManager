using FinanceManager.Application.Mcp;
using FinanceManager.Web.Mcp;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the SSE subscription layer of <see cref="McpSessionHub"/> (F-1101):
/// per-access fan-out isolation, the subscriber cap, clean unsubscribe and bounded-queue
/// drop semantics.
/// </summary>
public sealed class McpSessionHubTests
{
    private static McpRequestStatusEvent Event(Guid accessId, string status = "Executed")
        => new(accessId, Guid.NewGuid(), Guid.NewGuid(), status);

    /// <summary>Publish reaches only the subscribers of the owning access.</summary>
    [Fact]
    public async Task Publish_ReachesOnlyOwningAccess()
    {
        var hub = new McpSessionHub();
        var accessA = Guid.NewGuid();
        var accessB = Guid.NewGuid();
        using var subA = hub.Subscribe(accessA, 4)!;
        using var subB = hub.Subscribe(accessB, 4)!;

        var evt = Event(accessA);
        var delivered = hub.Publish(evt);

        Assert.Equal(1, delivered);
        var received = await subA.Reader.ReadAsync();
        Assert.Equal(evt.RequestId, received.RequestId);
        Assert.False(subB.Reader.TryRead(out _));
    }

    /// <summary>All subscribers of an access receive the same event.</summary>
    [Fact]
    public async Task Publish_FansOutToAllSubscribersOfAccess()
    {
        var hub = new McpSessionHub();
        var access = Guid.NewGuid();
        using var s1 = hub.Subscribe(access, 4)!;
        using var s2 = hub.Subscribe(access, 4)!;

        var delivered = hub.Publish(Event(access));

        Assert.Equal(2, delivered);
        Assert.True(s1.Reader.TryRead(out _));
        Assert.True(s2.Reader.TryRead(out _));
    }

    /// <summary>The per-access cap rejects excess subscriptions.</summary>
    [Fact]
    public void Subscribe_RespectsCap()
    {
        var hub = new McpSessionHub();
        var access = Guid.NewGuid();
        using var s1 = hub.Subscribe(access, 2);
        using var s2 = hub.Subscribe(access, 2);
        var s3 = hub.Subscribe(access, 2);

        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.Null(s3);
        Assert.Equal(2, hub.SubscriberCount(access));
    }

    /// <summary>Disposing detaches the subscription and completes its channel.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        var hub = new McpSessionHub();
        var access = Guid.NewGuid();
        var sub = hub.Subscribe(access, 4)!;

        sub.Dispose();

        Assert.Equal(0, hub.SubscriberCount(access));
        Assert.Equal(0, hub.Publish(Event(access)));
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await sub.Reader.ReadAsync());
        Assert.NotNull(ex);
    }

    /// <summary>Queue overflow never blocks the publisher; the queue stays bounded and the
    /// freshest event is always delivered (DropWrite evicts the newest queued item).</summary>
    [Fact]
    public async Task Publish_OverflowNeverBlocks_KeepsQueueBounded()
    {
        var hub = new McpSessionHub();
        var access = Guid.NewGuid();
        using var sub = hub.Subscribe(access, 4)!;

        var last = Event(access);
        for (var i = 0; i < 200; i++)
        {
            hub.Publish(i == 199 ? last : Event(access));
        }

        var read = 0;
        McpRequestStatusEvent? final = null;
        while (sub.Reader.TryRead(out var evt))
        {
            read++;
            final = evt;
        }
        Assert.Equal(128, read); // bounded capacity
        Assert.Equal(last.RequestId, final!.RequestId); // freshest event survived
    }

    /// <summary>Sessions keep their transport semantics next to the subscription map.</summary>
    [Fact]
    public void Sessions_StillTouchAndExpire()
    {
        var hub = new McpSessionHub();
        var id = hub.Create();
        Assert.True(hub.Touch(id));
        Assert.True(hub.Remove(id));
        Assert.False(hub.Touch(id));
    }
}
