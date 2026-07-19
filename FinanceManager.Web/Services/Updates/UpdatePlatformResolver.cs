#pragma warning disable CS1591
using System.Runtime.InteropServices;
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdatePlatformResolver : IUpdatePlatformResolver
{
    public string CurrentRuntimeIdentifier
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux-x64";
            }

            return RuntimeInformation.RuntimeIdentifier;
        }
    }

    public string CurrentPlatform
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "linux"
                : RuntimeInformation.OSDescription;

    public UpdateAssetDto? SelectAsset(UpdateMetadataDto manifest)
        => manifest.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Platform, CurrentPlatform, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(asset.RuntimeIdentifier, CurrentRuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
}
#pragma warning restore CS1591
