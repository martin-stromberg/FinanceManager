namespace FinanceManager.Domain.Securities;

/// <summary>
/// Represents a stored foreign-exchange reference rate: how many units of
/// <see cref="CurrencyCode"/> one EUR buys on <see cref="Date"/> (ECB reference-rate
/// convention "foreign currency per 1 EUR", currently sourced via the Frankfurter API).
/// </summary>
/// <remarks>
/// Global, cross-user reference data: the entity deliberately carries no
/// <c>OwnerUserId</c>. Rates are objective market reference values identical for every
/// user, so a shared pool avoids duplicate provider calls and keeps the table small
/// (documented OwnerUserId exception — see <c>Docs/help/wertpapiermanagement/datenmodell.md</c>).
/// Rows are unique per (<see cref="CurrencyCode"/>, <see cref="Date"/>, <see cref="Source"/>).
/// </remarks>
public sealed class CurrencyExchangeRate : Entity
{
    /// <summary>
    /// Parameterless constructor for ORM/deserialization.
    /// </summary>
    private CurrencyExchangeRate() { }

    /// <summary>
    /// Creates a new <see cref="CurrencyExchangeRate"/> for the given currency and date.
    /// </summary>
    /// <param name="currencyCode">ISO-4217 currency code of the foreign currency (e.g. "USD"); normalized to uppercase.</param>
    /// <param name="date">Date the rate applies to. The time component is ignored.</param>
    /// <param name="ratePerEur">Units of <paramref name="currencyCode"/> per 1 EUR. Must be positive.</param>
    /// <param name="source">Origin of the rate (e.g. "Frankfurter" for ECB reference rates served by api.frankfurter.dev).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="currencyCode"/> or <paramref name="source"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="ratePerEur"/> is not positive.</exception>
    public CurrencyExchangeRate(string currencyCode, DateTime date, decimal ratePerEur, string source)
    {
        CurrencyCode = Guards.NotNullOrWhiteSpace(currencyCode, nameof(currencyCode)).Trim().ToUpperInvariant();
        Date = date.Date;
        RatePerEur = ratePerEur > 0m ? ratePerEur : throw new ArgumentOutOfRangeException(nameof(ratePerEur), "Rate must be positive.");
        Source = Guards.NotNullOrWhiteSpace(source, nameof(source));
    }

    /// <summary>
    /// Gets the ISO-4217 code of the foreign currency (uppercase).
    /// </summary>
    /// <value>The normalized currency code.</value>
    public string CurrencyCode { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the date the rate applies to (date component only).
    /// </summary>
    /// <value>The rate date.</value>
    public DateTime Date { get; private set; }

    /// <summary>
    /// Gets how many units of <see cref="CurrencyCode"/> one EUR buys on <see cref="Date"/>.
    /// </summary>
    /// <value>Foreign currency units per 1 EUR.</value>
    public decimal RatePerEur { get; private set; }

    /// <summary>
    /// Gets the origin of the rate (e.g. "Frankfurter").
    /// </summary>
    /// <value>The provider/source tag.</value>
    public string Source { get; private set; } = string.Empty;

    // Backup DTO
    /// <summary>
    /// DTO carrying the serializable state of a <see cref="CurrencyExchangeRate"/> for backup purposes.
    /// </summary>
    /// <param name="Id">Identifier of the exchange-rate record.</param>
    /// <param name="CurrencyCode">ISO-4217 code of the foreign currency (uppercase).</param>
    /// <param name="Date">Date the rate applies to (date component only).</param>
    /// <param name="RatePerEur">Foreign currency units per 1 EUR.</param>
    /// <param name="Source">Origin of the rate (e.g. "Frankfurter").</param>
    /// <param name="CreatedUtc">UTC timestamp when the record was created.</param>
    /// <returns>The result.</returns>
    public sealed record CurrencyExchangeRateBackupDto(Guid Id, string CurrencyCode, DateTime Date, decimal RatePerEur, string Source, DateTime CreatedUtc);

    /// <summary>
    /// Creates a backup DTO for this exchange-rate record.
    /// </summary>
    /// <returns>A <see cref="CurrencyExchangeRateBackupDto"/> containing the serializable state of this record.</returns>
    public CurrencyExchangeRateBackupDto ToBackupDto()
        => new CurrencyExchangeRateBackupDto(Id, CurrencyCode, Date, RatePerEur, Source, CreatedUtc);

    /// <summary>
    /// Assigns values from a backup DTO to this entity.
    /// </summary>
    /// <param name="dto">Backup DTO to read values from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(CurrencyExchangeRateBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        CurrencyCode = dto.CurrencyCode;
        Date = dto.Date;
        RatePerEur = dto.RatePerEur;
        Source = dto.Source;
        SetDates(dto.CreatedUtc, null);
    }
}
