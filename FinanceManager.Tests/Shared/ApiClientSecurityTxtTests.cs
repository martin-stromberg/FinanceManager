using System.Net;
using System.Net.Http.Json;
using FinanceManager.Shared;
using FinanceManager.Tests.TestHelpers;
using FluentAssertions;

namespace FinanceManager.Tests.ApiClientTests;

public sealed class ApiClientSecurityTxtTests
{
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
