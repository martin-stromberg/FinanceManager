namespace FinanceManager.Domain.Security;

/// <summary>
/// Stores the configurable security.txt directives.
/// </summary>
public sealed class SecurityTxtSettings : Entity, IAggregateRoot
{
    private SecurityTxtSettings()
    {
    }

    /// <summary>
    /// Creates a placeholder settings row before the admin has configured security.txt.
    /// </summary>
    /// <returns>An unconfigured settings instance.</returns>
    public static SecurityTxtSettings CreateUnconfigured()
    {
        var settings = new SecurityTxtSettings
        {
            Contact = string.Empty,
            Expires = DateTimeOffset.MaxValue
        };
        return settings;
    }

    /// <summary>
    /// Creates a new settings instance.
    /// </summary>
    public SecurityTxtSettings(string contact, DateTimeOffset expires)
    {
        Contact = Guards.NotNullOrWhiteSpace(contact, nameof(contact)).Trim();
        Expires = expires;
    }

    /// <summary>Contact directive.</summary>
    public string Contact { get; private set; } = string.Empty;
    /// <summary>Expires directive.</summary>
    public DateTimeOffset Expires { get; private set; }
    /// <summary>Encryption directive.</summary>
    public string? Encryption { get; private set; }
    /// <summary>Acknowledgments directive.</summary>
    public string? Acknowledgments { get; private set; }
    /// <summary>Preferred-Languages directive.</summary>
    public string? PreferredLanguages { get; private set; }
    /// <summary>Policy directive.</summary>
    public string? Policy { get; private set; }
    /// <summary>Hiring directive.</summary>
    public string? Hiring { get; private set; }

    /// <summary>Updates all directives.</summary>
    public void Update(string contact, DateTimeOffset expires, string? encryption, string? acknowledgments, string? preferredLanguages, string? policy, string? hiring)
    {
        Contact = Guards.NotNullOrWhiteSpace(contact, nameof(contact)).Trim();
        Expires = expires;
        Encryption = Normalize(encryption);
        Acknowledgments = Normalize(acknowledgments);
        PreferredLanguages = Normalize(preferredLanguages);
        Policy = Normalize(policy);
        Hiring = Normalize(hiring);
        Touch();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
