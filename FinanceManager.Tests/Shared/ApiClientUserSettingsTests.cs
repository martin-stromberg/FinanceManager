using System.Net;
using FinanceManager.Shared;
using FluentAssertions;

namespace FinanceManager.Tests.ApiClientTests;

/// <summary>
/// Verifies that <see cref="ApiClient"/>'s user-settings password method targets the correct
/// endpoint and HTTP verb, and that a failing response is reported via the boolean result while
/// the server's error message is exposed through <see cref="ApiClient.LastError"/>.
/// </summary>
public sealed class ApiClientUserSettingsTests
{
    /// <summary>Verifies UserSettings_ChangePasswordAsync issues a PUT against /api/user/settings/password and returns true on success.</summary>
    [Fact]
    public async Task UserSettings_ChangePassword_UsesPutOnPasswordPath()
    {
        HttpRequestMessage? capturedRequest = null;
        var api = StubHttpMessageHandler.CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var request = new ChangePasswordRequest("old-pw", "new-pw-123");

        var result = await api.UserSettings_ChangePasswordAsync(request, TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Put);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/api/user/settings/password");
    }

    /// <summary>Verifies a rejected password change returns false and surfaces the server error message via LastError.</summary>
    [Fact]
    public async Task UserSettings_ChangePassword_WhenApiFails_ReturnsFalseAndSetsLastError()
    {
        var api = StubHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"origin\":\"API_UserSettings\",\"code\":\"Err_InvalidCurrentPassword\",\"message\":\"The current password is not correct.\"}", System.Text.Encoding.UTF8, "application/json")
        });
        var request = new ChangePasswordRequest("wrong", "new-pw-123");

        var result = await api.UserSettings_ChangePasswordAsync(request, TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        api.LastError.Should().Be("The current password is not correct.");
        api.LastErrorCode.Should().Be("Err_InvalidCurrentPassword");
    }
}
