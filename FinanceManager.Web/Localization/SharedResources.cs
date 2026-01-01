namespace FinanceManager.Web.Localization;

/// <summary>
/// Marker class used to group localization resources that are shared across the web project.
/// Instances of this type are not created at runtime; its primary purpose is to provide a
/// type to associate resource files (resx) with the <see cref="IStringLocalizer"/> infrastructure.
/// </summary>
/// <remarks>
/// Place shared .resx files alongside this class (for example, "SharedResources.resx" and
/// culture-specific variants) so they can be discovered by the localization system.
/// This class intentionally has no members and should remain sealed.
/// </remarks>
public sealed class SharedResources { }