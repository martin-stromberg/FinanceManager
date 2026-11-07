namespace FinanceManager.Application.Mcp;

/// <summary>
/// One contiguous run of missing sequence numbers inside an audit chain
/// (<see cref="McpAuditChainVerificationResult"/>).
/// </summary>
/// <param name="FirstMissingSequenceNo">First missing sequence number (inclusive).</param>
/// <param name="LastMissingSequenceNo">Last missing sequence number (inclusive).</param>
public sealed record McpAuditChainGap(long FirstMissingSequenceNo, long LastMissingSequenceNo);

/// <summary>
/// Result of an audit chain integrity verification (F-0302, audit-konzept.md §7.2/§7.3):
/// gap-free monotonic <c>SequenceNo</c>, intact <c>PrevHash</c> linkage and a valid
/// recomputed <c>EntryHash</c> of every entry of the chain.
/// </summary>
/// <param name="OwnerUserId">Verified owner chain, or <see langword="null"/> for the global chain.</param>
/// <param name="EntriesChecked">Number of chain entries that were loaded and checked.</param>
/// <param name="IsIntact"><see langword="true"/> when no breach and no gap was found.</param>
/// <param name="FirstBrokenSequenceNo">
/// Sequence number of the first entry that failed verification (dangling
/// <c>PrevHash</c>, invalid recomputed <c>EntryHash</c> or duplicate/out-of-order
/// sequence), or <see langword="null"/> when no breach was found.
/// </param>
/// <param name="FailureReason">Human-readable description of the first breach.</param>
/// <param name="SequenceGaps">Runs of missing sequence numbers (Lücken).</param>
public sealed record McpAuditChainVerificationResult(
    Guid? OwnerUserId,
    long EntriesChecked,
    bool IsIntact,
    long? FirstBrokenSequenceNo,
    string? FailureReason,
    IReadOnlyList<McpAuditChainGap> SequenceGaps);
