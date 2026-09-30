using FinanceManager.Shared.WellKnown;

namespace FinanceManager.Domain.WellKnown;

/// <summary>
/// Stores the configurable well-known endpoint settings.
/// </summary>
public sealed class WellKnownSettings : Entity, IAggregateRoot
{
    /// <summary>Default redirect target for the change-password well-known endpoint.</summary>
    public const string DefaultChangePasswordUrl = "/change-password";

    /// <summary>
    /// Fixed identifier of the single settings row. Enforcing one well-known key guarantees that the
    /// table never holds more than one row — a concurrent first insert collides on the primary key
    /// instead of creating a duplicate.
    /// </summary>
    /// <value>The fixed <see cref="Guid"/> of the singleton settings row.</value>
    public static readonly Guid SingletonId = new("8b7f2c1d-4e5a-4b6c-9d0e-1f2a3b4c5d6e");

    private WellKnownSettings()
    {
    }

    /// <summary>
    /// Creates the default settings row used before the admin has configured the endpoints.
    /// The instance carries <see cref="SingletonId"/> so it represents the one and only settings row.
    /// </summary>
    /// <returns>A default settings instance.</returns>
    public static WellKnownSettings CreateDefault()
    {
        return new WellKnownSettings
        {
            Id = SingletonId,
            ChangePasswordUrl = DefaultChangePasswordUrl
        };
    }

    /// <summary>Target URL that the change-password well-known endpoint redirects to.</summary>
    public string ChangePasswordUrl { get; private set; } = DefaultChangePasswordUrl;

    /// <summary>Updates the change-password redirect URL.</summary>
    /// <param name="changePasswordUrl">A local root path or an absolute http/https URL.</param>
    public void Update(string changePasswordUrl)
    {
        var url = Guards.NotNullOrWhiteSpace(changePasswordUrl, nameof(changePasswordUrl)).Trim();
        if (!IsValidUrl(url))
        {
            throw new ArgumentException("URL must be a local root path or an absolute http/https URL.", nameof(changePasswordUrl));
        }

        ChangePasswordUrl = url;
        Touch();
    }

    /// <summary>Checks whether the value is a local root path or an absolute http/https URL.</summary>
    /// <param name="url">The url.</param>
    /// <returns>The result.</returns>
    public static bool IsValidUrl(string? url) => WellKnownUrlValidator.IsValidUrl(url);
}
