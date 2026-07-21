using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Tests.Updates;
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
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.code.Should().Be("Err_Update_Locked");
    }

    [Fact]
    public async Task StartInstall_ReturnsNotFoundWithLocalizableCode_WhenNoReadyPackage()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUpdateOrchestrator>();
                services.AddScoped<IUpdateOrchestrator>(_ => new ThrowingUpdateOrchestrator(new FileNotFoundException("No ready update package is available.")));
            });
        });
        var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var response = await client.PostAsJsonAsync("/api/setup/update/install/start", new UpdateStartRequest(true));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.code.Should().Be("Err_Update_NotReady");
    }

    [Fact]
    public async Task ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<UpdateOptions>(o => o.WorkingDirectory = tempDir.FullName);
                });
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);

            var lockPath = Path.Combine(tempDir.FullName, "update.lock");
            await File.WriteAllTextAsync(lockPath, DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"));

            var response = await client.PostAsJsonAsync("/api/setup/update/lock/reset", new UpdateLockResetRequest("integration test"));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            File.Exists(lockPath).Should().BeFalse();
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Status_WhenInstallingAndVersionMatchesAfterRestart_ReportsNoUpdate()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<UpdateOptions>(o => o.WorkingDirectory = tempDir.FullName);
                    services.RemoveAll<IInstalledReleaseMetadataProvider>();
                    services.AddSingleton<IInstalledReleaseMetadataProvider>(new FixedInstalledReleaseMetadataProvider("1.2.3"));
                });
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);
            await WriteStatusAsync(tempDir.FullName, UpdateStatusTestData.InstallingStatus("1.2.3"));

            var response = await client.GetAsync("/api/setup/update/status");

            response.EnsureSuccessStatusCode();
            var status = await response.Content.ReadFromJsonAsync<UpdateStatusDto>();
            status!.Status.Should().Be(UpdateStatusKind.NoUpdate);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Status_WhenInstallingAndVersionMismatchAfterRestart_ReportsFailed()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<UpdateOptions>(o => o.WorkingDirectory = tempDir.FullName);
                    services.RemoveAll<IInstalledReleaseMetadataProvider>();
                    services.AddSingleton<IInstalledReleaseMetadataProvider>(new FixedInstalledReleaseMetadataProvider("1.2.3"));
                });
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);
            await WriteStatusAsync(tempDir.FullName, UpdateStatusTestData.InstallingStatus("9.9.9"));

            var response = await client.GetAsync("/api/setup/update/status");

            response.EnsureSuccessStatusCode();
            var status = await response.Content.ReadFromJsonAsync<UpdateStatusDto>();
            status!.Status.Should().Be(UpdateStatusKind.Failed);
            status.LastError.Should().Be("Installed version '1.2.3' does not match the expected version '9.9.9' after the update process finished.");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static Task WriteStatusAsync(string workingDirectory, UpdateStatusDto status)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return File.WriteAllTextAsync(Path.Combine(workingDirectory, "status.json"), JsonSerializer.Serialize(status, options));
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
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Admin login failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
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

    private sealed class FixedInstalledReleaseMetadataProvider : IInstalledReleaseMetadataProvider
    {
        private readonly string _version;

        public FixedInstalledReleaseMetadataProvider(string version)
        {
            _version = version;
        }

        public Task<InstalledReleaseMetadataDto> GetAsync(CancellationToken ct = default)
            => Task.FromResult(new InstalledReleaseMetadataDto(_version, null, null, null, null));
    }
}
