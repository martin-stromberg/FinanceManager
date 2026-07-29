using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

/// <summary>
/// Builds a fully wired, isolated auto-update stack (orchestrator, status service, events, store and validator)
/// rooted at a temporary directory, for use in orchestrator- and status-level tests. Disposing deletes the
/// temporary directory.
/// </summary>
public sealed class AutoUpdateTestContext : IDisposable
{
    private readonly DirectoryInfo _tempDirectory;

    public AutoUpdateTestContext()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("autoupdate-tests-");
        Environment = new TestAutoUpdateEnvironment(_tempDirectory.FullName);
        Options = new AutoUpdateOptions
        {
            Enabled = true,
            EnableAutomaticDownload = true,
            EnableAutomaticInstallation = true,
            DownloadPath = "updates",
            ServiceName = "TestService",
            MaxAssetBytes = 10 * 1024 * 1024
        };
        Source = new FakeAutoUpdateSource();
        Options.Source = Source;
        TimeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-28T10:00:00+00:00"));

        PackageStore = new FileSystemAutoUpdatePackageStore(Environment, Options, TimeProvider);
        StateStore = new FileSystemAutoUpdateStateStore(PackageStore, NullLogger<FileSystemAutoUpdateStateStore>.Instance);
        Validator = new AutoUpdatePackageValidator();
        InstalledVersionProvider = new TestInstalledVersionProvider();
        Events = new AutoUpdateEvents();
        StatusService = new AutoUpdateStatusService(StateStore, InstalledVersionProvider);

        var platformResolver = new AutoUpdatePlatformResolver();
        var serviceProbe = new DefaultAutoUpdateServiceProbe(NullLogger<DefaultAutoUpdateServiceProbe>.Instance);
        var serviceResolver = new AutoUpdateServiceResolver(Environment, serviceProbe, Options);
        var scriptGenerator = new AutoUpdateScriptGenerator(Environment, PackageStore);
        ProcessRunner = new RecordingProcessRunner();
        HostTerminator = new RecordingHostTerminator();
        Installer = new AutoUpdateInstaller(Validator, serviceResolver, scriptGenerator, ProcessRunner, Options);

        Orchestrator = new AutoUpdateOrchestrator(
            Options,
            Events,
            StatusService,
            PackageStore,
            Validator,
            InstalledVersionProvider,
            Installer,
            HostTerminator,
            TimeProvider);
    }

    public TestAutoUpdateEnvironment Environment { get; }

    public AutoUpdateOptions Options { get; }

    public FakeAutoUpdateSource Source { get; }

    public IAutoUpdatePackageStore PackageStore { get; }

    public IAutoUpdateStateStore StateStore { get; }

    public IAutoUpdatePackageValidator Validator { get; }

    public TestInstalledVersionProvider InstalledVersionProvider { get; }

    public AutoUpdateEvents Events { get; }

    public AutoUpdateStatusService StatusService { get; }

    public FakeTimeProvider TimeProvider { get; }

    public IAutoUpdateInstaller Installer { get; }

    public RecordingProcessRunner ProcessRunner { get; }

    public RecordingHostTerminator HostTerminator { get; }

    public AutoUpdateOrchestrator Orchestrator { get; }

    public string TempDirectory => _tempDirectory.FullName;

    /// <summary>
    /// Builds a valid ZIP package, wires it up as the fake source's next check result and package content, and
    /// returns the matching descriptor.
    /// </summary>
    /// <param name="version">The version to advertise as available.</param>
    /// <returns>The descriptor matching the generated package.</returns>
    public AutoUpdatePackageDescriptor CreateAvailablePackage(string version = "2.0.0")
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("app.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("content");
        }

        var bytes = memory.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var fileName = $"app-{version}.zip";
        var descriptor = new AutoUpdatePackageDescriptor(
            version,
            "windows",
            "win-x64",
            fileName,
            new Uri(Path.Combine(_tempDirectory.FullName, fileName)),
            sha256,
            bytes.Length);

        Source.PackageContent = bytes;
        Source.AvailableVersion = version;
        Source.Package = descriptor;
        return descriptor;
    }

    public void Dispose()
    {
        try
        {
            _tempDirectory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    public sealed class TestInstalledVersionProvider : IInstalledVersionProvider
    {
        public string? Version { get; set; }

        public Task<InstalledReleaseInfo> GetAsync(CancellationToken ct = default)
            => Task.FromResult(new InstalledReleaseInfo(Version, null, null, null, null));
    }

    public sealed class RecordingProcessRunner : IAutoUpdateProcessRunner
    {
        public int PrepareEnvironmentCallCount { get; private set; }

        public int StartScriptCallCount { get; private set; }

        public string? LastScriptPath { get; private set; }

        public void EnsureUpdateUnitAvailable(string scriptPath) => PrepareEnvironmentCallCount++;

        public void StartScript(string scriptPath)
        {
            StartScriptCallCount++;
            LastScriptPath = scriptPath;
        }
    }

    public sealed class RecordingHostTerminator : IAutoUpdateHostTerminator
    {
        public int StopApplicationCallCount { get; private set; }

        public void StopApplication() => StopApplicationCallCount++;
    }
}
