using System.Net;
using System.Net.Http.Json;
using FinanceManager.Shared;
using FinanceManager.Tests.TestHelpers;
using FluentAssertions;

namespace FinanceManager.Tests.ApiClientTests;

/// <summary>
/// Verifies that <see cref="ApiClient"/>'s security.txt methods target the correct admin endpoint and HTTP
/// verbs, and that a failing response is surfaced as an exception rather than swallowed.
/// </summary>
public sealed class ApiClientSecurityTxtTests
{
    /// <summary>Verifies GetSecurityTxtSettingsAsync issues a GET against /api/admin/security-txt and deserializes the returned settings unchanged.</summary>
    [Fact]
    public async Task SecurityTxt_GetSettingsAsync_CallsExpectedEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var expected = new SecurityTxtSettingsDto
        {
            Contact = "mailto:security@example.com",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Canonical = "https://security.example.com/.well-known/security.txt"
        };
        var api = CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            };
        });

        var result = await api.GetSecurityTxtSettingsAsync(TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/api/admin/security-txt");
    }

    /// <summary>Verifies UpdateSecurityTxtSettingsAsync issues a PUT against /api/admin/security-txt with the supplied request payload.</summary>
    [Fact]
    public async Task SecurityTxt_UpdateSettingsAsync_CallsExpectedEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var api = CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var request = SecurityTxtSettingsTestData.ValidRequest();

        await api.UpdateSecurityTxtSettingsAsync(request, TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Put);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/api/admin/security-txt");
    }

    /// <summary>Ensures a non-success HTTP response from the security.txt endpoint propagates as an HttpRequestException instead of returning a null or default settings object.</summary>
    [Fact]
    public async Task SecurityTxt_GetSettingsAsync_WhenApiFails_Throws()
    {
        var api = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var act = () => api.GetSecurityTxtSettingsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static ApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => new(new HttpClient(new DelegatingHandlerStub(handler)) { BaseAddress = new Uri("https://example.test") });

    private sealed class DelegatingHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
