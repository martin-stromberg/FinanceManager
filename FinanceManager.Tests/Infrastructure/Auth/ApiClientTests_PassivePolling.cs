using System.Net;
using FinanceManager.Shared;

namespace FinanceManager.Tests.Infrastructure.Auth;

/// <summary>Distinguishes passive background polling from protected user actions.</summary>
public sealed class ApiClientTests_PassivePolling
{
    /// <summary>An expired passive poll still fails but never requests navigation.</summary>
    [Fact]
    public async Task BackgroundTasks_GetActiveForPollingAsync_Unauthorized_DoesNotRequestLogin()
    {
        var client = CreateClient();
        var raised = false;
        client.AuthenticationRequired += (_, _) => raised = true;

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.BackgroundTasks_GetActiveForPollingAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.False(raised);
    }

    /// <summary>The same endpoint remains protected when called as a user action.</summary>
    [Fact]
    public async Task BackgroundTasks_GetActiveAsync_Unauthorized_RequestsLogin()
    {
        var client = CreateClient();
        var raised = false;
        client.AuthenticationRequired += (_, _) => raised = true;

        await Assert.ThrowsAsync<HttpRequestException>(() => client.BackgroundTasks_GetActiveAsync(TestContext.Current.CancellationToken));

        Assert.True(raised);
    }

    private static ApiClient CreateClient() => new(new HttpClient(new UnauthorizedHandler()) { BaseAddress = new Uri("https://example.test") });

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Session expired") });
    }
}
