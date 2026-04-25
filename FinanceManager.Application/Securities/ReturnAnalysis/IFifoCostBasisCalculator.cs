namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Calculates FIFO cost basis and realized gains for a sequence of security transactions (FR-6).
/// Thread-safe, stateless.
/// </summary>
public interface IFifoCostBasisCalculator
{
    /// <summary>
    /// Processes a chronologically sorted list of security transactions using FIFO.
    /// Sort order: BookingDate ascending, then by Id (tiebreaker).
    /// Fee postings with same GroupId as a Buy are added to that lot's cost basis.
    /// Fee postings without a matching Buy GroupId are tracked separately (standalone fees).
    /// Oversell (sell > available lots): returns result with HasOversellWarning = true.
    /// </summary>
    /// <param name="transactions">Transactions, expected sorted by date, then Id. Mix of Buy, Sell, Fee, Dividend, Tax.</param>
    /// <returns>FIFO cost basis result.</returns>
    FifoCostBasisResult Calculate(IReadOnlyList<SecurityTransaction> transactions);
}
