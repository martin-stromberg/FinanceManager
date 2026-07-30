using System.Runtime.InteropServices;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdatePlatformResolver"/> implementation based on <see cref="RuntimeInformation"/>.
/// </summary>
public sealed class AutoUpdatePlatformResolver : IAutoUpdatePlatformResolver
{
    /// <summary>
    /// The <see cref="IAutoUpdatePlatformResolver.CurrentPlatform"/> value reported on Windows.
    /// </summary>
    public const string WindowsPlatform = "windows";

    /// <summary>
    /// The <see cref="IAutoUpdatePlatformResolver.CurrentPlatform"/> value reported on Linux.
    /// </summary>
    public const string LinuxPlatform = "linux";

    private readonly Func<OSPlatform, bool> _isOSPlatform;
    private readonly string _runtimeIdentifier;
    private readonly string _osDescription;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdatePlatformResolver"/> class using the actual
    /// runtime platform.
    /// </summary>
    public AutoUpdatePlatformResolver()
        : this(RuntimeInformation.IsOSPlatform, RuntimeInformation.RuntimeIdentifier, RuntimeInformation.OSDescription)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdatePlatformResolver"/> class with an overridable
    /// platform detection function, used for testing.
    /// </summary>
    /// <param name="isOSPlatform">Determines whether the current process is running on the given platform.</param>
    /// <param name="runtimeIdentifier">The fallback runtime identifier for unsupported platforms.</param>
    /// <param name="osDescription">The fallback platform description for unsupported platforms, used by <see cref="CurrentPlatform"/>.</param>
    public AutoUpdatePlatformResolver(Func<OSPlatform, bool> isOSPlatform, string runtimeIdentifier, string osDescription = "")
    {
        _isOSPlatform = isOSPlatform;
        _runtimeIdentifier = runtimeIdentifier;
        _osDescription = osDescription;
    }

    /// <inheritdoc />
    public string CurrentRuntimeIdentifier
    {
        get
        {
            if (_isOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }

            if (_isOSPlatform(OSPlatform.Linux))
            {
                return "linux-x64";
            }

            return _runtimeIdentifier;
        }
    }

    /// <inheritdoc />
    public string CurrentPlatform
        => _isOSPlatform(OSPlatform.Windows)
            ? WindowsPlatform
            : _isOSPlatform(OSPlatform.Linux)
                ? LinuxPlatform
                : _osDescription;

    /// <inheritdoc />
    public AutoUpdatePackageDescriptor? SelectPackage(AutoUpdateReleaseInfo release)
        => release.Packages.FirstOrDefault(package =>
            string.Equals(package.Platform, CurrentPlatform, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(package.RuntimeIdentifier, CurrentRuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
}
