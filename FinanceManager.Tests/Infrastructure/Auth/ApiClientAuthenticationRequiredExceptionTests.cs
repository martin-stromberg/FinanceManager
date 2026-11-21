using System.Net;
using FinanceManager.Shared;

namespace FinanceManager.Tests.Infrastructure.Auth;

/// <summary>
/// Covers the exception contract of <see cref="ApiClient"/>: responses classified as authentication
/// failures surface as <see cref="ApiAuthenticationRequiredException"/> (a HttpRequestException
/// specialization), while other failures keep throwing a plain <see cref="HttpRequestException"/>.
/// </summary>
public sealed class ApiClientAuthenticationRequiredExceptionTests
{
    /// <summary>A 401 response throws the authentication-specific exception carrying the status code.</summary>
    [Fact]
    public async Task EnsureSuccess_Unauthorized_ThrowsApiAuthenticationRequiredException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var error = await Assert.ThrowsAsync<ApiAuthenticationRequiredException>(
            () => client.BackgroundTasks_GetActiveAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    /// <summary>A 403 response carrying a WWW-Authenticate header is treated as an authentication failure.</summary>
    [Fact]
    public async Task EnsureSuccess_ForbiddenWithAuthenticateHeader_ThrowsApiAuthenticationRequiredException()
    {
        var client = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", "Bearer error=\"invalid_token\"");
            return response;
        });

        var error = await Assert.ThrowsAsync<ApiAuthenticationRequiredException>(
            () => client.BackgroundTasks_GetActiveAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    /// <summary>A non-authentication failure keeps throwing the plain <see cref="HttpRequestException"/>.</summary>
    [Fact]
    public async Task EnsureSuccess_ServerError_ThrowsPlainHttpRequestException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.BackgroundTasks_GetActiveAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
    }

    private static ApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://example.test") });

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
