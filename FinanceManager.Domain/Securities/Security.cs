namespace FinanceManager.Domain.Securities;

/// <summary>
/// Represents a financial security (e.g. stock, bond) tracked by a user. Contains metadata, status and optional price error state.
/// </summary>
public sealed class Security
{
    /// <summary>
    /// Parameterless constructor for ORM/deserialization.
    /// </summary>
    private Security() { }

    /// <summary>
    /// Creates a new <see cref="Security"/> instance for the given owner.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the user who owns the security.</param>
    /// <param name="name">Display name of the security. Must not be null or whitespace.</param>
    /// <param name="identifier">Identifier such as WKN or ISIN. Must not be null or whitespace.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="alphaVantageCode">Optional AlphaVantage symbol/code used for price lookups.</param>
    /// <param name="currencyCode">Currency ISO code (e.g. "EUR"). Must not be null or whitespace.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/>, <paramref name="identifier"/> or <paramref name="currencyCode"/> are null or whitespace. See <see cref="Update"/>.</exception>
    public Security(Guid ownerUserId, string name, string identifier, string? description, string? alphaVantageCode, string currencyCode, Guid? categoryId)
    {
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        Update(name, identifier, description, alphaVantageCode, currencyCode, categoryId);
        CreatedUtc = DateTime.UtcNow;
        IsActive = true;
    }

    /// <summary>
    /// Gets the identifier of the security.
    /// </summary>
    /// <value>The security GUID.</value>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the owner user identifier of the security.
    /// </summary>
    /// <value>The owner's GUID.</value>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Gets the display name of the security.
    /// </summary>
    /// <value>The name string.</value>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the optional description of the security.
    /// </summary>
    /// <value>Description or <c>null</c>.</value>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the primary identifier of the security (e.g. WKN or ISIN).
    /// </summary>
    /// <value>Identifier string.</value>
    public string Identifier { get; private set; } = string.Empty; // WKN / ISIN

    /// <summary>
    /// Optional code used with AlphaVantage or other external price providers.
    /// </summary>
    /// <value>AlphaVantage symbol or <c>null</c>.</value>
    public string? AlphaVantageCode { get; private set; }

    /// <summary>
    /// ISO currency code used for prices (uppercase).
    /// </summary>
    /// <value>Currency code, e.g. "EUR".</value>
    public string CurrencyCode { get; private set; } = "EUR";

    /// <summary>
    /// Optional category identifier for this security.
    /// </summary>
    /// <value>Category GUID or <c>null</c>.</value>
    public Guid? CategoryId { get; private set; }          // NEW

    /// <summary>
    /// UTC timestamp when the security entry was created.
    /// </summary>
    /// <value>Creation time in UTC.</value>
    public DateTime CreatedUtc { get; private set; }

    /// <summary>
    /// Indicates whether the security is active.
    /// </summary>
    /// <value><c>true</c> when active; otherwise <c>false</c>.</value>
    public bool IsActive { get; private set; }

    /// <summary>
    /// UTC timestamp when the security was archived, or <c>null</c> if still active.
    /// </summary>
    /// <value>Archive time in UTC or <c>null</c>.</value>
    public DateTime? ArchivedUtc { get; private set; }

    /// <summary>
    /// Indicates that there was an error fetching prices for this security.
    /// </summary>
    /// <value><c>true</c> if a price error is active; otherwise <c>false</c>.</value>
    public bool HasPriceError { get; private set; }

    /// <summary>
    /// Optional human-readable price error message.
    /// </summary>
    /// <value>Error message or <c>null</c>.</value>
    public string? PriceErrorMessage { get; private set; }

    /// <summary>
    /// UTC timestamp since when the price error state was active, or <c>null</c> if none.
    /// </summary>
    /// <value>UTC time or <c>null</c>.</value>
    public DateTime? PriceErrorSinceUtc { get; private set; }

    /// <summary>
    /// Optional reference to a symbol attachment.
    /// </summary>
    /// <value>Attachment GUID or <c>null</c>.</value>
    public Guid? SymbolAttachmentId { get; private set; }

    /// <summary>
    /// Updates core metadata for the security.
    /// </summary>
    /// <param name="name">Display name. Must not be null or whitespace.</param>
    /// <param name="identifier">Primary identifier (WKN/ISIN). Must not be null or whitespace.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="alphaVantageCode">Optional external provider code.</param>
    /// <param name="currencyCode">ISO currency code. Must not be null or whitespace.</param>
    /// <param name="categoryId">Optional category id.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/>, <paramref name="identifier"/>, or <paramref name="currencyCode"/> are null or whitespace.</exception>
    public void Update(string name, string identifier, string? description, string? alphaVantageCode, string currencyCode, Guid? categoryId)
    {
        if (string.IsNullOrWhiteSpace(name)) { throw new ArgumentException("Name required", nameof(name)); }
        if (string.IsNullOrWhiteSpace(identifier)) { throw new ArgumentException("Identifier required", nameof(identifier)); }
        if (string.IsNullOrWhiteSpace(currencyCode)) { throw new ArgumentException("Currency required", nameof(currencyCode)); }

        Name = name.Trim();
        Identifier = identifier.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        AlphaVantageCode = string.IsNullOrWhiteSpace(alphaVantageCode) ? null : alphaVantageCode.Trim();
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        CategoryId = categoryId;
    }

    /// <summary>
    /// Archives the security if it is currently active. No-op when already archived.
    /// </summary>
    public void Archive()
    {
        if (!IsActive) { return; }
        IsActive = false;
        ArchivedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the security as having a price fetch error and records the message and timestamp.
    /// </summary>
    /// <param name="message">Error message to record. Whitespace-only strings are preserved as provided; if null or whitespace, a default message will be stored by the implementation.</param>
    public void SetPriceError(string message)
    {
        HasPriceError = true;
        PriceErrorMessage = string.IsNullOrWhiteSpace(message) ? "Unknown error" : message;
        PriceErrorSinceUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears the price error state and associated metadata.
    /// </summary>
    public void ClearPriceError()
    {
        HasPriceError = false;
        PriceErrorMessage = null;
        PriceErrorSinceUtc = null;
    }

    /// <summary>
    /// Sets or clears the symbol attachment reference. Passing <see cref="Guid.Empty"/> is treated as <c>null</c>.
    /// </summary>
    /// <param name="attachmentId">Attachment GUID to set, or <see cref="Guid.Empty"/>/<c>null</c> to clear.</param>
    public void SetSymbolAttachment(Guid? attachmentId)
    {
        SymbolAttachmentId = attachmentId == Guid.Empty ? null : attachmentId;
    }
}
