namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Lifecycle state of an <see cref="AiActionRequest"/> (approval-konzept.md §3.2;
/// datenmodell-erweiterungen.md §2.2). The five normative core states plus the documented
/// additional terminal state <see cref="Expired"/>.
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. Value list is normative and must not
/// be changed without updating the concept.
/// </remarks>
public enum AiActionRequestStatus
{
    /// <summary>Created; the owner decision is open and the expiry deadline is running.</summary>
    Pending = 0,

    /// <summary>Approved by the owner (or pre-approved); waits in the persistent execution queue.</summary>
    Approved = 1,

    /// <summary>Rejected by the owner or withdrawn by the system (terminal state).</summary>
    Rejected = 2,

    /// <summary>Execution finished successfully; <c>ExecutionResult</c> is set (terminal state).</summary>
    Executed = 3,

    /// <summary>Execution was attempted and failed; <c>FailureReason</c> is set (terminal state).</summary>
    Failed = 4,

    /// <summary>The decision deadline passed without a decision (terminal state).</summary>
    Expired = 5
}
