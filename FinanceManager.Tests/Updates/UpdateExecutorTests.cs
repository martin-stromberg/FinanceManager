using System.IO.Compression;
using System.Security.Cryptography;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateExecutorTests
{
    [Fact]
    public async Task StartAsync_WhenGeneratorFails_RemovesLockAndWritesFailedStatus()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestEnvironment(root.FullName);
            var fileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var executor = new UpdateExecutor(
                fileStore,
                new TestResolver(),
                new ThrowingGenerator(),
                new TestRunner(),
                new TestTerminator(),
                new UpdateValidator(Options.Create(new UpdateOptions())),
                Options.Create(new UpdateOptions { MaxAssetBytes = 1024 * 1024 }));
            var status = await ReadyStatusAsync(fileStore);

            var act = () => executor.StartAsync(Settings(), status);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("script failed");
            File.Exists(fileStore.LockPath).Should().BeFalse();
            executor.IsInstallRunning.Should().BeFalse();
            var stored = await fileStore.ReadStatusAsync();
            stored.Should().NotBeNull();
            stored!.Status.Should().Be(UpdateStatusKind.Failed);
            stored.IsLocked.Should().BeFalse();
            stored.LastError.Should().Be("script failed");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_WhenRunnerFails_RemovesLockAndWritesFailedStatus()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestEnvironment(root.FullName);
            var fileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var scriptPath = fileStore.ScriptPath("ps1");
            var executor = new UpdateExecutor(
                fileStore,
                new TestResolver(),
                new TestGenerator(scriptPath),
                new ThrowingRunner(),
                new TestTerminator(),
                new UpdateValidator(Options.Create(new UpdateOptions())),
                Options.Create(new UpdateOptions { MaxAssetBytes = 1024 * 1024 }));

            var act = async () => await executor.StartAsync(Settings(), await ReadyStatusAsync(fileStore));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("runner failed");
            File.Exists(fileStore.LockPath).Should().BeFalse();
            executor.IsInstallRunning.Should().BeFalse();
            var stored = await fileStore.ReadStatusAsync();
            stored!.Status.Should().Be(UpdateStatusKind.Failed);
            stored.LastError.Should().Be("runner failed");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_RevalidatesPendingZipBeforeGeneratingScript()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var env = new TestEnvironment(root.FullName);
            var fileStore = new UpdateFileStore(env, Options.Create(new UpdateOptions { WorkingDirectory = "updates" }));
            var status = await ReadyStatusAsync(fileStore);
            await File.WriteAllTextAsync(fileStore.PendingAssetPath(status.DownloadedAssetName!), "tampered");
            var generator = new TrackingGenerator(fileStore.ScriptPath("ps1"));
            var executor = new UpdateExecutor(
                fileStore,
                new TestResolver(),
                generator,
                new TestRunner(),
                new TestTerminator(),
                new UpdateValidator(Options.Create(new UpdateOptions())),
                Options.Create(new UpdateOptions { MaxAssetBytes = 1024 * 1024 }));

            var act = () => executor.StartAsync(Settings(), status);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Update package size does not match the manifest.");
            generator.WasCalled.Should().BeFalse();
            File.Exists(fileStore.LockPath).Should().BeFalse();
            var stored = await fileStore.ReadStatusAsync();
            stored!.Status.Should().Be(UpdateStatusKind.Failed);
            stored.LastError.Should().Be("Update package size does not match the manifest.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static UpdateSettingsDto Settings()
        => new(false, 60, "martin-stromberg", "FinanceManager", "update.json", null, "FinanceManager", null, "updates", 120);

    private static async Task<UpdateStatusDto> ReadyStatusAsync(IUpdateFileStore fileStore)
    {
        Directory.CreateDirectory(fileStore.PendingDirectory);
        var zipPath = fileStore.PendingAssetPath("release.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("app.txt");
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream);
            await writer.WriteAsync("content");
        }

        var asset = new UpdateAssetDto("windows", "win-x64", "release.zip", "https://example.test/release.zip", await Sha256Async(zipPath), new FileInfo(zipPath).Length);
        var metadata = new UpdateMetadataDto("1.2.3", null, null, "martin-stromberg", "FinanceManager", new[] { asset });
        return new UpdateStatusDto(UpdateStatusKind.Ready, "1.2.2", null, "1.2.3", "win-x64", DateTimeOffset.UtcNow, null, "release.zip", false, null, null, metadata);
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private sealed class TestResolver : IUpdateServiceResolver
    {
        public UpdateInstallationTarget Resolve(UpdateSettingsDto settings) => new("windows", "FinanceManager", null);
    }

    private sealed class ThrowingGenerator : IUpdateScriptGenerator
    {
        public Task<string> GenerateAsync(UpdateAssetDto asset, string zipPath, UpdateSettingsDto settings, UpdateInstallationTarget target, CancellationToken ct = default)
            => throw new InvalidOperationException("script failed");
    }

    private sealed class TestGenerator : IUpdateScriptGenerator
    {
        private readonly string _scriptPath;

        public TestGenerator(string scriptPath)
        {
            _scriptPath = scriptPath;
        }

        public Task<string> GenerateAsync(UpdateAssetDto asset, string zipPath, UpdateSettingsDto settings, UpdateInstallationTarget target, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_scriptPath)!);
            File.WriteAllText(_scriptPath, string.Empty);
            return Task.FromResult(_scriptPath);
        }
    }

    private sealed class TrackingGenerator : IUpdateScriptGenerator
    {
        private readonly string _scriptPath;

        public TrackingGenerator(string scriptPath)
        {
            _scriptPath = scriptPath;
        }

        public bool WasCalled { get; private set; }

        public Task<string> GenerateAsync(UpdateAssetDto asset, string zipPath, UpdateSettingsDto settings, UpdateInstallationTarget target, CancellationToken ct = default)
        {
            WasCalled = true;
            Directory.CreateDirectory(Path.GetDirectoryName(_scriptPath)!);
            File.WriteAllText(_scriptPath, string.Empty);
            return Task.FromResult(_scriptPath);
        }
    }

    private sealed class TestRunner : IUpdateProcessRunner
    {
        public void StartScript(string scriptPath)
        {
        }
    }

    private sealed class ThrowingRunner : IUpdateProcessRunner
    {
        public void StartScript(string scriptPath) => throw new InvalidOperationException("runner failed");
    }

    private sealed class TestTerminator : IUpdateHostTerminator
    {
        public void StopApplication()
        {
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = root;
        }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
