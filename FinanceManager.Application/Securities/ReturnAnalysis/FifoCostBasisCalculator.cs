using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Calculates FIFO cost basis and realized gains for a sequence of security transactions.
/// Implements <see cref="IFifoCostBasisCalculator"/> (FR-6). Stateless and thread-safe.
/// </summary>
public sealed class FifoCostBasisCalculator : IFifoCostBasisCalculator
{
    private readonly ILogger<FifoCostBasisCalculator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FifoCostBasisCalculator"/>.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public FifoCostBasisCalculator(ILogger<FifoCostBasisCalculator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public FifoCostBasisResult Calculate(IReadOnlyList<SecurityTransaction> transactions)
    {
        if (transactions is null || transactions.Count == 0)
        {
            return new FifoCostBasisResult(
                TotalCostBasis: 0m,
                RealizedGains: 0m,
                RemainingLots: Array.Empty<FifoLot>(),
                TotalSharesHeld: 0m,
                HasOversellWarning: false,
                OversellWarningMessage: null,
                StandaloneFeeTotal: 0m
            );
        }

        // Sort by Date ascending, then by Id ascending (deterministic tiebreaker)
        var sorted = transactions
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToList();

        // Queue holding open FIFO lots (mutable internal representation)
        var lotQueue = new Queue<MutableFifoLot>();

        // Map GroupId → lot for fee-to-buy association
        // Stores the lot created by a Buy transaction keyed by its GroupId
        var groupIdToLot = new Dictionary<Guid, MutableFifoLot>();

        decimal realizedGains = 0m;
        decimal standaloneFeeTotal = 0m; // fees not linked to any lot via GroupId
        bool hasOversellWarning = false;
        string? oversellWarningMessage = null;

        foreach (var tx in sorted)
        {
            switch (tx.Type)
            {
                case SecurityPostingSubType.Buy:
                    ProcessBuy(tx, lotQueue, groupIdToLot);
                    break;

                case SecurityPostingSubType.Sell:
                    (realizedGains, hasOversellWarning, oversellWarningMessage) = ProcessSell(
                        tx, lotQueue, realizedGains, hasOversellWarning, oversellWarningMessage);
                    break;

                case SecurityPostingSubType.Fee:
                    standaloneFeeTotal += ProcessFee(tx, groupIdToLot);
                    break;

                case SecurityPostingSubType.Dividend:
                case SecurityPostingSubType.Tax:
                    // Dividends and taxes do not affect FIFO cost basis
                    break;

                default:
                    _logger.LogDebug(
                        "FifoCostBasisCalculator: Unknown transaction type {Type} for posting {Id} – skipped.",
                        tx.Type, tx.Id);
                    break;
            }
        }

        // Build result from remaining lots
        decimal totalCostBasis = 0m;
        decimal totalSharesHeld = 0m;
        var remainingLots = new List<FifoLot>(lotQueue.Count);

        foreach (var lot in lotQueue)
        {
            if (lot.Quantity <= 0m) continue;

            decimal costPerUnit = lot.Quantity > 0m ? lot.TotalCost / lot.Quantity : 0m;
            remainingLots.Add(new FifoLot(lot.PurchaseDate, lot.Quantity, costPerUnit));
            totalCostBasis += lot.TotalCost;
            totalSharesHeld += lot.Quantity;
        }

        // Design decision (Option A): standalone fees – fees whose GroupId did not match any Buy lot –
        // are always added to TotalCostBasis regardless of GroupId linkage. Fees linked via GroupId
        // are already embedded in the respective lot's TotalCost above.
        // Rationale: GroupId assignment depends on the UI data-entry path and is not guaranteed for
        // every import or manual entry. Excluding standalone fees would silently underreport invested
        // capital for users who recorded fees without a matching GroupId.
        totalCostBasis += standaloneFeeTotal;

        return new FifoCostBasisResult(
            TotalCostBasis: totalCostBasis,
            RealizedGains: realizedGains,
            RemainingLots: remainingLots.AsReadOnly(),
            TotalSharesHeld: totalSharesHeld,
            HasOversellWarning: hasOversellWarning,
            OversellWarningMessage: oversellWarningMessage,
            StandaloneFeeTotal: standaloneFeeTotal
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void ProcessBuy(
        SecurityTransaction tx,
        Queue<MutableFifoLot> lotQueue,
        Dictionary<Guid, MutableFifoLot> groupIdToLot)
    {
        decimal quantity = tx.Quantity ?? 0m;

        if (quantity <= 0m)
        {
            _logger.LogDebug(
                "FifoCostBasisCalculator: Buy transaction {Id} has zero or null quantity – skipping lot creation.",
                tx.Id);
            return;
        }

        // Cost basis is the absolute value of the buy amount (outflow is negative by convention)
        decimal totalCost = Math.Abs(tx.Amount);

        var lot = new MutableFifoLot(tx.Date, quantity, totalCost);
        lotQueue.Enqueue(lot);

        // Register lot for potential fee augmentation via GroupId
        groupIdToLot[tx.GroupId] = lot;

        _logger.LogDebug(
            "FifoCostBasisCalculator: Buy {Id}: qty={Qty}, cost={Cost:F2}, groupId={GroupId}.",
            tx.Id, quantity, totalCost, tx.GroupId);
    }

    private (decimal RealizedGains, bool HasOversellWarning, string? OversellWarningMessage) ProcessSell(
        SecurityTransaction tx,
        Queue<MutableFifoLot> lotQueue,
        decimal currentRealizedGains,
        bool currentOversellWarning,
        string? currentOversellMessage)
    {
        // Domain convention: sell Quantity is stored as a negative value (shares leaving the portfolio).
        // Use Math.Abs so that both negative (domain imports) and positive (manual entries) work correctly.
        decimal sellQuantity = Math.Abs(tx.Quantity ?? 0m);

        if (sellQuantity <= 0m)
        {
            _logger.LogDebug(
                "FifoCostBasisCalculator: Sell transaction {Id} has zero or null quantity – skipping.",
                tx.Id);
            return (currentRealizedGains, currentOversellWarning, currentOversellMessage);
        }

        decimal sellProceeds = tx.Amount; // positive for sell inflows
        decimal remainingToSell = sellQuantity;
        decimal costOfSold = 0m;

        while (remainingToSell > 0m && lotQueue.Count > 0)
        {
            var lot = lotQueue.Peek();

            if (lot.Quantity <= remainingToSell)
            {
                // Consume the entire lot
                costOfSold += lot.TotalCost;
                remainingToSell -= lot.Quantity;
                lotQueue.Dequeue();
            }
            else
            {
                // Partially consume the lot
                decimal fraction = remainingToSell / lot.Quantity;
                decimal portionCost = lot.TotalCost * fraction;
                costOfSold += portionCost;

                lot.Quantity -= remainingToSell;
                lot.TotalCost -= portionCost;
                remainingToSell = 0m;
            }
        }

        if (remainingToSell > 0m)
        {
            // Oversell: more shares sold than available lots
            string message = $"Sell transaction {tx.Id} on {tx.Date:yyyy-MM-dd}: " +
                             $"attempted to sell {sellQuantity} shares but only {sellQuantity - remainingToSell} were available in lots. " +
                             "Result may be incomplete due to missing historical data.";

            _logger.LogWarning("FifoCostBasisCalculator: {Message}", message);

            currentOversellWarning = true;
            currentOversellMessage = message;
        }

        decimal gain = sellProceeds - costOfSold;
        currentRealizedGains += gain;

        _logger.LogDebug(
            "FifoCostBasisCalculator: Sell {Id}: qty={Qty}, proceeds={Proceeds:F2}, costOfSold={Cost:F2}, gain={Gain:F2}.",
            tx.Id, sellQuantity, sellProceeds, costOfSold, gain);

        return (currentRealizedGains, currentOversellWarning, currentOversellMessage);
    }

    private decimal ProcessFee(
        SecurityTransaction tx,
        Dictionary<Guid, MutableFifoLot> groupIdToLot)
    {
        decimal feeAmount = Math.Abs(tx.Amount);

        if (groupIdToLot.TryGetValue(tx.GroupId, out MutableFifoLot? associatedLot))
        {
            // Fee is linked to a Buy via GroupId: add to that lot's cost basis (per-unit cost increases).
            associatedLot.TotalCost += feeAmount;

            _logger.LogDebug(
                "FifoCostBasisCalculator: Fee {Id}: amount={Fee:F2} added to lot purchased on {PurchaseDate:d} (groupId={GroupId}).",
                tx.Id, feeAmount, associatedLot.PurchaseDate, tx.GroupId);

            return 0m; // linked fee: already captured in lot.TotalCost, not standalone
        }
        else
        {
            // Standalone fee: GroupId has no matching Buy lot.
            // Per Option-A design: the caller accumulates these and adds them to TotalCostBasis
            // so that invested capital is never under-reported when GroupId is absent or wrong.
            _logger.LogDebug(
                "FifoCostBasisCalculator: Standalone fee {Id}: amount={Fee:F2}, groupId={GroupId} – added to TotalCostBasis (no matching Buy lot).",
                tx.Id, feeAmount, tx.GroupId);

            return feeAmount; // caller adds this to standaloneFeeTotal → TotalCostBasis
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal mutable lot for FIFO processing
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class MutableFifoLot
    {
        /// <summary>Gets the purchase date of the lot.</summary>
        public DateTime PurchaseDate { get; }

        /// <summary>Gets or sets the remaining quantity in the lot.</summary>
        public decimal Quantity { get; set; }

        /// <summary>Gets or sets the total cost basis of the remaining quantity.</summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Initializes a new mutable FIFO lot.
        /// </summary>
        /// <param name="purchaseDate">Date of purchase.</param>
        /// <param name="quantity">Initial quantity.</param>
        /// <param name="totalCost">Initial total cost basis.</param>
        public MutableFifoLot(DateTime purchaseDate, decimal quantity, decimal totalCost)
        {
            PurchaseDate = purchaseDate;
            Quantity = quantity;
            TotalCost = totalCost;
        }
    }
}
