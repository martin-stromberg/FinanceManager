#pragma warning disable CS1591
using System.Runtime.InteropServices;
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdatePlatformResolver : IUpdatePlatformResolver
{
    private readonly Func<OSPlatform, bool> _isOSPlatform;
    private readonly string _runtimeIdentifier;

    public UpdatePlatformResolver()
        : this(RuntimeInformation.IsOSPlatform, RuntimeInformation.RuntimeIdentifier)
    {
    }

    public UpdatePlatformResolver(Func<OSPlatform, bool> isOSPlatform, string runtimeIdentifier)
    {
        _isOSPlatform = isOSPlatform;
        _runtimeIdentifier = runtimeIdentifier;
    }

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

    public string CurrentPlatform
        => _isOSPlatform(OSPlatform.Windows)
            ? "windows"
            : _isOSPlatform(OSPlatform.Linux)
                ? "linux"
                : RuntimeInformation.OSDescription;

    public UpdateAssetDto? SelectAsset(UpdateMetadataDto manifest)
        => manifest.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Platform, CurrentPlatform, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(asset.RuntimeIdentifier, CurrentRuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
}
#pragma warning restore CS1591
