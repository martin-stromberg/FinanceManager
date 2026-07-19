namespace FinanceManager.Web.Services.Help;

/// <summary>
/// Validates help files against the build-generated SHA-256 manifest.
/// </summary>
public interface IHelpAssetIntegrityValidator
{
    /// <summary>
    /// Determines whether a help file matches the manifest generated during build.
    /// </summary>
    /// <param name="fullPath">The absolute path of the help file.</param>
    /// <returns><c>true</c> when the file is listed and has the expected hash.</returns>
    bool IsTrustedHelpFile(string fullPath);
}
