using FinanceManager.Shared;

namespace FinanceManager.Tests.ApiClientTests;

/// <summary>
/// Shared <see cref="HttpMessageHandler"/> stub for <see cref="ApiClient"/> unit tests: each request is
/// delegated to a caller-supplied callback so tests can capture the outgoing request and dictate the
/// response without a real HTTP server.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpMessageHandler"/> class.
    /// </summary>
    /// <param name="handler">Callback invoked for every outgoing request; returns the response to surface.</param>
    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Creates an <see cref="ApiClient"/> backed by this stub handler with a fixed test base address.
    /// </summary>
    /// <param name="handler">Callback invoked for every outgoing request; returns the response to surface.</param>
    /// <returns>An <see cref="ApiClient"/> that never performs real HTTP calls.</returns>
    internal static ApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => new(new HttpClient(new StubHttpMessageHandler(handler)) { BaseAddress = new Uri("https://example.test") });

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}
