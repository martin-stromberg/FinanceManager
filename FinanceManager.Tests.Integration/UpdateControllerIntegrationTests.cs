using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Tests.Updates;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using msTools.Updater;
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
            "martin-stromberg",
            "FinanceManager",
            "update.json",
            new TimeOnly(20, 0),
            new TimeOnly(6, 0),
            new TimeOnly(3, 30),
            "FinanceManagerService",
            null,
            "updates",
            120,
            true);

        var put = await client.PutAsJsonAsync("/api/setup/update/settings", update);
        put.EnsureSuccessStatusCode();
        var settings = await put.Content.ReadFromJsonAsync<UpdateSettingsDto>();

        settings!.Enabled.Should().BeTrue();
        settings.SourceCheckStartTime.Should().Be(new TimeOnly(20, 0));
        settings.SourceCheckEndTime.Should().Be(new TimeOnly(6, 0));
        settings.RepositoryOwner.Should().Be("martin-stromberg");
        settings.IncludePrereleases.Should().BeTrue();
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
                builder.ConfigureServices(services => SetDownloadPath(services, tempDir.FullName));
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

    [Theory]
    [InlineData(UpdateLockResetFailureKind.NoLock, HttpStatusCode.Conflict, "Err_Update_Reset_NoLock")]
    [InlineData(UpdateLockResetFailureKind.LockNotStale, HttpStatusCode.Conflict, "Err_Update_Reset_LockNotStale")]
    [InlineData(UpdateLockResetFailureKind.LockDeleteFailed, HttpStatusCode.Conflict, "Err_Update_Reset_DeleteFailed")]
    [InlineData(UpdateLockResetFailureKind.ResetFailed, HttpStatusCode.InternalServerError, "Err_Update_Reset_Failed")]
    public async Task ResetLock_ReturnsSpecificErrorCode_WhenResetFailureIsClassified(
        UpdateLockResetFailureKind kind,
        HttpStatusCode statusCode,
        string errorCode)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUpdateOrchestrator>();
                services.AddScoped<IUpdateOrchestrator>(_ => new ThrowingUpdateOrchestrator(
                    new NotSupportedException(),
                    new UpdateLockResetException(kind, UpdateLockResetFailureSource.FinanceManager, "reset failed")));
            });
        });
        var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var response = await client.PostAsJsonAsync("/api/setup/update/lock/reset", new UpdateLockResetRequest("integration test"));

        response.StatusCode.Should().Be(statusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.code.Should().Be(errorCode);
        error.code.Should().NotBe("Err_Update_InstallRunning");
    }

    [Fact]
    public async Task ResetLock_ReturnsResetFailed_WhenResetThrowsUnclassifiedIOException()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUpdateOrchestrator>();
                services.AddScoped<IUpdateOrchestrator>(_ => new ThrowingUpdateOrchestrator(
                    new NotSupportedException(),
                    new IOException("unclassified reset failure")));
            });
        });
        var client = factory.CreateClient();
        await AuthenticateAdminAsync(client);

        var response = await client.PostAsJsonAsync("/api/setup/update/lock/reset", new UpdateLockResetRequest("integration test"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.code.Should().Be("Err_Update_Reset_Failed");
    }

    [Fact]
    public async Task ResetLock_Returns409NoLock_WhenNoLockFileExists()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services => SetDownloadPath(services, tempDir.FullName));
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);

            var response = await client.PostAsJsonAsync("/api/setup/update/lock/reset", new UpdateLockResetRequest("integration test"));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
            error!.code.Should().Be("Err_Update_Reset_NoLock");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ResetLock_Returns409LockNotStale_WhenLockFileIsTooYoung()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services => SetDownloadPath(services, tempDir.FullName));
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);

            var lockPath = Path.Combine(tempDir.FullName, "update.lock");
            await File.WriteAllTextAsync(lockPath, DateTimeOffset.UtcNow.ToString("O"));

            var response = await client.PostAsJsonAsync("/api/setup/update/lock/reset", new UpdateLockResetRequest("integration test"));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
            error!.code.Should().Be("Err_Update_Reset_LockNotStale");
            File.Exists(lockPath).Should().BeTrue();
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task StartInstall_SucceedsAndLockRemains_WhenInstallerDoesNotCleanUpLock()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    SetDownloadPath(services, tempDir.FullName);
                    services.RemoveAll<IAutoUpdateOrchestrator>();
                    services.AddSingleton<IAutoUpdateOrchestrator>(new SucceedingAutoUpdateOrchestrator());
                });
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);

            var lockPath = Path.Combine(tempDir.FullName, "update.lock");
            await File.WriteAllTextAsync(lockPath, DateTimeOffset.UtcNow.ToString("O"));

            var response = await client.PostAsJsonAsync("/api/setup/update/install/start", new UpdateStartRequest(true));

            response.EnsureSuccessStatusCode();
            File.Exists(lockPath).Should().BeTrue();
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PersistedSettings_AreAppliedToAutoUpdateOptions_OnStartup_WithoutManualSave()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            // Simulates settings saved through the setup UI during a previous run, persisted to disk, but never
            // re-applied to the auto-update library's runtime options because the process later restarted.
            var persisted = new UpdateSettingsDto(
                true,
                "martin-stromberg",
                "FinanceManager",
                "update.json",
                new TimeOnly(21, 0),
                new TimeOnly(5, 0),
                null,
                "PersistedServiceName",
                null,
                tempDir.FullName,
                250,
                true);
            var json = JsonSerializer.Serialize(persisted, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await File.WriteAllTextAsync(Path.Combine(tempDir.FullName, "settings.json"), json);

            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services => SetDownloadPath(services, tempDir.FullName));
            });
            using var client = factory.CreateClient();

            var options = factory.Services.GetRequiredService<AutoUpdateOptions>();

            options.ServiceName.Should().Be("PersistedServiceName");
            options.SourceCheck.Interval.Should().Be(AutoUpdateOptionsMapper.DailySourceCheckIntervalMinutes);
            options.SourceCheck.TimeRanges.Should().HaveCount(14);
            options.HealthTimeoutSeconds.Should().Be(250);
            options.AllowPrereleaseUpdates.Should().BeTrue();
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
                    SetDownloadPath(services, tempDir.FullName);
                    services.RemoveAll<IInstalledReleaseMetadataProvider>();
                    services.AddSingleton<IInstalledReleaseMetadataProvider>(new FixedInstalledReleaseMetadataProvider("1.2.3"));
                    services.RemoveAll<IInstalledVersionProvider>();
                    services.AddSingleton<IInstalledVersionProvider>(new FixedInstalledVersionProvider("1.2.3"));
                });
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);
            await WriteStatusAsync(tempDir.FullName, UpdateStatusTestData.InstallingSnapshot("1.2.3"));

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
                    SetDownloadPath(services, tempDir.FullName);
                    services.RemoveAll<IInstalledReleaseMetadataProvider>();
                    services.AddSingleton<IInstalledReleaseMetadataProvider>(new FixedInstalledReleaseMetadataProvider("1.2.3"));
                    services.RemoveAll<IInstalledVersionProvider>();
                    services.AddSingleton<IInstalledVersionProvider>(new FixedInstalledVersionProvider("1.2.3"));
                });
            });
            var client = factory.CreateClient();
            await AuthenticateAdminAsync(client);
            await WriteStatusAsync(tempDir.FullName, UpdateStatusTestData.InstallingSnapshot("9.9.9"));

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

    private static Task WriteStatusAsync(string downloadPath, AutoUpdateStatusSnapshot snapshot)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Directory.CreateDirectory(downloadPath);
        return File.WriteAllTextAsync(Path.Combine(downloadPath, "status.json"), JsonSerializer.Serialize(snapshot, options));
    }

    /// <summary>
    /// Mutates <see cref="AutoUpdateOptions.DownloadPath"/> on the already-registered singleton instance before the
    /// test server starts, so status/lock files are read from an isolated temp directory instead of the real
    /// <c>updates</c> folder.
    /// </summary>
    /// <param name="services">The service collection to locate the registered <see cref="AutoUpdateOptions"/> instance in.</param>
    /// <param name="downloadPath">The temporary directory to redirect <see cref="AutoUpdateOptions.DownloadPath"/> to.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown with an actionable message if <see cref="AutoUpdateOptions"/> is no longer registered as a singleton
    /// instance descriptor (see <c>AutoUpdateHostBuilderExtensions.UseAutoUpdate</c>), instead of failing later with
    /// an unexplained <see cref="NullReferenceException"/>.
    /// </exception>
    private static void SetDownloadPath(IServiceCollection services, string downloadPath)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AutoUpdateOptions));
        if (descriptor?.ImplementationInstance is not AutoUpdateOptions options)
        {
            throw new InvalidOperationException(
                $"Expected {nameof(AutoUpdateOptions)} to be registered as a singleton instance " +
                "(see AutoUpdateHostBuilderExtensions.UseAutoUpdate's `builder.Services.AddSingleton(options)`), " +
                "so this test helper can mutate DownloadPath before the test server starts. " +
                "The registration style has changed - update this helper to match.");
        }

        options.DownloadPath = downloadPath;
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
        private readonly Exception _startException;
        private readonly Exception? _resetException;

        public ThrowingUpdateOrchestrator(Exception startException, Exception? resetException = null)
        {
            _startException = startException;
            _resetException = resetException;
        }

        public Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateSettingsDto> GetSettingsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateSettingsDto> SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateSettingsDto> ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task ResetLockAsync(string? reason, CancellationToken ct = default)
            => _resetException is null ? throw new NotSupportedException() : Task.FromException(_resetException);

        public Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default) => Task.FromException<UpdateStatusDto>(_startException);
    }

    private sealed class SucceedingAutoUpdateOrchestrator : IAutoUpdateOrchestrator
    {
        public Task<AutoUpdateResult> RunUpdateAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AutoUpdateResult> CheckForUpdateAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AutoUpdateResult> DownloadAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AutoUpdateResult> InstallAsync(bool confirmDowntime, bool force, CancellationToken ct = default)
            => Task.FromResult(new AutoUpdateResult(AutoUpdateOutcome.Success, AutoUpdateState.Success, "installed", null));
        public Task<AutoUpdateStatusSnapshot> GetStatusAsync(CancellationToken ct = default) => throw new NotSupportedException();
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

    private sealed class FixedInstalledVersionProvider : IInstalledVersionProvider
    {
        private readonly string _version;

        public FixedInstalledVersionProvider(string version)
        {
            _version = version;
        }

        public Task<InstalledReleaseInfo> GetAsync(CancellationToken ct = default)
            => Task.FromResult(new InstalledReleaseInfo(_version, null, null, null, null));
    }
}
