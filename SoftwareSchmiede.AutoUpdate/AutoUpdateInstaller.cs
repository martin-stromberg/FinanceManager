namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateInstaller"/> implementation: re-validates the downloaded package, resolves the
/// installation target and generates the platform-specific installation script. Event raising is the
/// responsibility of the caller (<see cref="AutoUpdateOrchestrator"/>).
/// </summary>
public sealed class AutoUpdateInstaller : IAutoUpdateInstaller
{
    private readonly IAutoUpdatePackageValidator _validator;
    private readonly IAutoUpdateServiceResolver _serviceResolver;
    private readonly IAutoUpdateScriptGenerator _scriptGenerator;
    private readonly IAutoUpdateProcessRunner _processRunner;
    private readonly AutoUpdateOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateInstaller"/> class.
    /// </summary>
    /// <param name="validator">Used to re-validate the downloaded package before installation.</param>
    /// <param name="serviceResolver">Used to resolve the installation target.</param>
    /// <param name="scriptGenerator">Used to generate the installation script.</param>
    /// <param name="processRunner">Used to start the installation script.</param>
    /// <param name="options">The runtime-mutable auto-update options.</param>
    public AutoUpdateInstaller(
        IAutoUpdatePackageValidator validator,
        IAutoUpdateServiceResolver serviceResolver,
        IAutoUpdateScriptGenerator scriptGenerator,
        IAutoUpdateProcessRunner processRunner,
        AutoUpdateOptions options)
    {
        _validator = validator;
        _serviceResolver = serviceResolver;
        _scriptGenerator = scriptGenerator;
        _processRunner = processRunner;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<string> PrepareAsync(AutoUpdatePackageDescriptor package, string zipPath, CancellationToken ct = default)
    {
        await _validator.ValidateDownloadedPackageAsync(package, zipPath, _options.MaxAssetBytes, ct);
        var target = _serviceResolver.Resolve();
        return await _scriptGenerator.GenerateAsync(package, zipPath, target, ct);
    }

    /// <inheritdoc />
    public void Start(string scriptPath)
    {
        _processRunner.EnsureUpdateUnitAvailable(scriptPath);
        _processRunner.StartScript(scriptPath);
    }
}
