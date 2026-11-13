namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Year-over-year depot development section of the portfolio analysis report:
/// one row per calendar year combining the portfolio's market value at year end
/// with the net dividends received during that year.
/// </summary>
/// <param name="Years">Yearly rows in ascending chronological order; empty when the user has no security postings.</param>
/// <returns>A depot development record.</returns>
public sealed record PortfolioDepotDevelopmentDto(IReadOnlyList<PortfolioDepotDevelopmentYear> Years);

/// <summary>
/// A single calendar-year row of the depot development series.
/// </summary>
/// <param name="Year">Calendar year the row describes.</param>
/// <param name="IsYtd">Whether the row represents the current, not-yet-completed year (values up to today).</param>
/// <param name="YearStartValue">
/// Portfolio market value at the start of the year (= <see cref="YearEndValue"/> of the previous year);
/// for the first year of the series the cumulative invested capital of that year is used instead.
/// </param>
/// <param name="YearEndValue">Portfolio market value on December 31st of the year (or today for the YTD year).</param>
/// <param name="NetDividends">Net dividends received during the year (gross dividends minus tax postings).</param>
/// <param name="CumulativeInvestedCapital">
/// Cumulative invested capital up to the year's reference date: FIFO cost basis of the positions held
/// on that date, net of standalone fees.
/// </param>
/// <returns>A depot development year record.</returns>
public sealed record PortfolioDepotDevelopmentYear(
    int Year,
    bool IsYtd,
    decimal YearStartValue,
    decimal YearEndValue,
    decimal NetDividends,
    decimal CumulativeInvestedCapital);
