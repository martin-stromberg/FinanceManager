namespace FinanceManager.Application.Securities;

/// <summary>
/// A resolved foreign-exchange rate: how many units of a foreign currency one EUR buys
/// on <see cref="Date"/> (ECB convention "foreign currency per 1 EUR").
/// </summary>
/// <param name="Date">Date the rate applies to (date component only; may differ from the requested date when the provider answers with the previous business day).</param>
/// <param name="RatePerEur">Foreign currency units per 1 EUR (positive).</param>
/// <param name="Source">Origin of the rate (e.g. "Frankfurter", or "Fixed" for the synthetic EUR identity rate).</param>
/// <returns>The result.</returns>
public sealed record FxRate(DateTime Date, decimal RatePerEur, string Source);
