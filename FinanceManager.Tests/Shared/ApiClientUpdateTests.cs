using System.Net;
using System.Net.Http.Json;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Update;
using FluentAssertions;

namespace FinanceManager.Tests.ApiClientTests;

public sealed class ApiClientUpdateTests
{
    [Fact]
    public async Task Updates_StartInstallAsync_WhenNotReady404_ThrowsAndPreservesApiError()
    {
        var api = CreateClient(request => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = JsonContent.Create(ApiErrorDto.Create("API_Update", "Err_Update_NotReady", "No ready update package is available."))
        });

        var act = () => api.Updates_StartInstallAsync(new UpdateStartRequest(true));

        await act.Should().ThrowAsync<HttpRequestException>();
        api.LastErrorCode.Should().Be("Err_Update_NotReady");
        api.LastError.Should().Be("No ready update package is available.");
    }

    [Fact]
    public async Task UpdateApiClientFlows_CallExpectedEndpoints()
    {
        var requests = new List<(HttpMethod Method, string Path)>();
        var api = CreateClient(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/setup/update/check" => JsonResponse(new UpdateCheckResultDto(false, Status(UpdateStatusKind.NoUpdate), "No update")),
                "/api/setup/update/schedule" => JsonResponse(Settings(new TimeOnly(3, 15))),
                "/api/setup/update/install/start" => JsonResponse(Status(UpdateStatusKind.Installing)),
                "/api/setup/update/lock/reset" => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });

        var check = await api.Updates_CheckAsync();
        var schedule = await api.Updates_ScheduleAsync(new UpdateScheduleRequest(new TimeOnly(3, 15)));
        var install = await api.Updates_StartInstallAsync(new UpdateStartRequest(true));
        var reset = await api.Updates_ResetLockAsync(new UpdateLockResetRequest("stale"));

        check.UpdateAvailable.Should().BeFalse();
        schedule.ScheduledInstallTime.Should().Be(new TimeOnly(3, 15));
        install!.Status.Should().Be(UpdateStatusKind.Installing);
        reset.Should().BeTrue();
        requests.Should().Contain((HttpMethod.Post, "/api/setup/update/check"));
        requests.Should().Contain((HttpMethod.Post, "/api/setup/update/schedule"));
        requests.Should().Contain((HttpMethod.Post, "/api/setup/update/install/start"));
        requests.Should().Contain((HttpMethod.Post, "/api/setup/update/lock/reset"));
    }

    private static ApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => new(new HttpClient(new DelegatingHandlerStub(handler)) { BaseAddress = new Uri("https://example.test") });

    private static HttpResponseMessage JsonResponse<T>(T value)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static UpdateSettingsDto Settings(TimeOnly? scheduledInstallTime)
        => new(false, 60, "owner", "repo", "update.json", scheduledInstallTime, null, null, null, "updates", 120);

    private static UpdateStatusDto Status(UpdateStatusKind kind)
        => new(kind, "1.0.0", null, null, "win-x64", null, null, null, kind == UpdateStatusKind.Installing, kind == UpdateStatusKind.Installing ? DateTimeOffset.UtcNow : null, null, null);

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
