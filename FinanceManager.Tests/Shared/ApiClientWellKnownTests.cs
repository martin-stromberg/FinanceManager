using System.Net;
using System.Net.Http.Json;
using FinanceManager.Shared;
using FluentAssertions;

namespace FinanceManager.Tests.ApiClientTests;

/// <summary>
/// Verifies that <see cref="ApiClient"/>'s well-known settings methods target the correct admin
/// endpoint and HTTP verbs, and that a failing response is surfaced as an exception rather than
/// swallowed.
/// </summary>
public sealed class ApiClientWellKnownTests
{
    /// <summary>Verifies GetWellKnownSettingsAsync issues a GET against /api/admin/well-known and deserializes the returned settings unchanged.</summary>
    [Fact]
    public async Task GetWellKnownSettings_UsesGetOnAdminPath()
    {
        HttpRequestMessage? capturedRequest = null;
        var expected = new WellKnownSettingsDto { ChangePasswordUrl = "/account/password" };
        var api = StubHttpMessageHandler.CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            };
        });

        var result = await api.GetWellKnownSettingsAsync(TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/api/admin/well-known");
    }

    /// <summary>Verifies UpdateWellKnownSettingsAsync issues a PUT against /api/admin/well-known with the supplied request payload.</summary>
    [Fact]
    public async Task UpdateWellKnownSettings_UsesPutOnAdminPath()
    {
        HttpRequestMessage? capturedRequest = null;
        var api = StubHttpMessageHandler.CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var request = new WellKnownSettingsUpdateRequest("https://idp.example.com/account/password");

        await api.UpdateWellKnownSettingsAsync(request, TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Put);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/api/admin/well-known");
    }

    /// <summary>Ensures a non-success HTTP response from the well-known endpoint propagates as an HttpRequestException instead of returning a null or default settings object.</summary>
    [Fact]
    public async Task WellKnown_GetSettingsAsync_WhenApiFails_Throws()
    {
        var api = StubHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var act = () => api.GetWellKnownSettingsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
