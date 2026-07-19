using System.Net;
using System.Net.Http.Json;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FinanceManager.Tests.Integration;

public sealed class UpdateControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UpdateControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateStatus_RequiresAdmin()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/setup/update/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSettings_RoundTripsForAdmin()
    {
        var client = _factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var update = new UpdateSettingsUpdateRequest(
            true,
            15,
            "martin-stromberg",
            "FinanceManager",
            "update.json",
            new TimeOnly(3, 30),
            "FinanceManagerService",
            null,
            "updates",
            120);

        var put = await client.PutAsJsonAsync("/api/setup/update/settings", update);
        put.EnsureSuccessStatusCode();
        var settings = await put.Content.ReadFromJsonAsync<UpdateSettingsDto>();

        settings!.Enabled.Should().BeTrue();
        settings.CheckIntervalMinutes.Should().Be(15);
        settings.RepositoryOwner.Should().Be("martin-stromberg");
    }

    [Fact]
    public async Task StartInstall_ReturnsConflict_WhenUpdateLockIsActive()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUpdateOrchestrator>();
                services.AddScoped<IUpdateOrchestrator>(_ => new ThrowingUpdateOrchestrator(new IOException("An update lock is active.")));
            });
        });
        var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var response = await client.PostAsJsonAsync("/api/setup/update/install/start", new UpdateStartRequest(true));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StartInstall_ReturnsBadRequest_WhenDowntimeIsNotConfirmed()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUpdateOrchestrator>();
                services.AddScoped<IUpdateOrchestrator>(_ => new ThrowingUpdateOrchestrator(new ArgumentException("Downtime confirmation is required.", "confirmDowntime")));
            });
        });
        var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var response = await client.PostAsJsonAsync("/api/setup/update/install/start", new UpdateStartRequest(false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task AuthenticateAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = TestWebApplicationFactory.BootstrapAdminUsername,
            password = TestWebApplicationFactory.BootstrapAdminPassword
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed class ThrowingUpdateOrchestrator : IUpdateOrchestrator
    {
        private readonly Exception _exception;

        public ThrowingUpdateOrchestrator(Exception exception)
        {
            _exception = exception;
        }

        public Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateSettingsDto> GetSettingsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateSettingsDto> SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateSettingsDto> ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task ResetLockAsync(string? reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default) => Task.FromException<UpdateStatusDto>(_exception);
    }
}
