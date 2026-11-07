using FinanceManager.Domain.Mcp;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the <see cref="AiActionRequest"/> state machine (approval-konzept.md §3.2/§3.3):
/// allowed transitions, technically excluded forbidden transitions (terminal states,
/// Rejected → Approved, Pending → Executed, unclaimed completion) and the queue-claim fields.
/// </summary>
public sealed class AiActionRequestTests
{
    private static AiActionRequest NewRequest(DateTime? expiresUtc = null)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), "access", "fm_test_tool", "delete", "katalog-v1",
            "{}", null, expiresUtc ?? DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "key-1");

    private static AiActionRequest ClaimedRequest()
    {
        var r = NewRequest();
        r.Approve(Guid.NewGuid(), null);
        r.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow);
        return r;
    }

    /// <summary>NewRequest ShouldStartPending WithDefaults.</summary>
    [Fact]
    public void NewRequest_ShouldStartPending_WithDefaults()
    {
        var r = NewRequest();
        Assert.Equal(AiActionRequestStatus.Pending, r.Status);
        Assert.False(r.IsTerminal());
        Assert.Equal(0, r.AttemptCount);
        Assert.Null(r.ClaimToken);
        Assert.Null(r.DecidedUtc);
        Assert.Null(r.ExecutedUtc);
    }

    /// <summary>Approve ShouldMovePendingToApproved.</summary>
    [Fact]
    public void Approve_ShouldMovePendingToApproved()
    {
        var r = NewRequest();
        var decider = Guid.NewGuid();
        r.Approve(decider, "ok");
        Assert.Equal(AiActionRequestStatus.Approved, r.Status);
        Assert.Equal(decider, r.DecidedByUserId);
        Assert.Equal("ok", r.DecisionNote);
        Assert.NotNull(r.DecidedUtc);
    }

    /// <summary>Reject ShouldMovePendingToRejected.</summary>
    [Fact]
    public void Reject_ShouldMovePendingToRejected()
    {
        var r = NewRequest();
        r.Reject(Guid.NewGuid(), "no");
        Assert.Equal(AiActionRequestStatus.Rejected, r.Status);
        Assert.True(r.IsTerminal());
    }

    /// <summary>Approve OnRejected ShouldThrow.</summary>
    [Fact]
    public void Approve_OnRejected_ShouldThrow()
    {
        var r = NewRequest();
        r.Reject(Guid.NewGuid(), null);
        Assert.Throws<InvalidOperationException>(() => r.Approve(Guid.NewGuid(), null));
    }

    /// <summary>MarkExecuted OnPending ShouldThrow NoDirectJump.</summary>
    [Fact]
    public void MarkExecuted_OnPending_ShouldThrow_NoDirectJump()
    {
        var r = NewRequest();
        Assert.Throws<InvalidOperationException>(() => r.MarkExecuted("{}"));
    }

    /// <summary>MarkExecuted OnApprovedButUnclaimed ShouldThrow.</summary>
    [Fact]
    public void MarkExecuted_OnApprovedButUnclaimed_ShouldThrow()
    {
        var r = NewRequest();
        r.Approve(Guid.NewGuid(), null);
        Assert.Throws<InvalidOperationException>(() => r.MarkExecuted("{}"));
    }

    /// <summary>ClaimThenExecute ShouldReachExecuted.</summary>
    [Fact]
    public void ClaimThenExecute_ShouldReachExecuted()
    {
        var r = ClaimedRequest();
        r.MarkExecuted("{\"postingIds\":[]}");
        Assert.Equal(AiActionRequestStatus.Executed, r.Status);
        Assert.True(r.IsTerminal());
        Assert.Equal(1, r.AttemptCount);
        Assert.Null(r.ClaimToken);
        Assert.NotNull(r.ExecutedUtc);
    }

    /// <summary>ClaimThenFail ShouldReachFailed.</summary>
    [Fact]
    public void ClaimThenFail_ShouldReachFailed()
    {
        var r = ClaimedRequest();
        r.MarkFailed("Err_AlreadyBooked");
        Assert.Equal(AiActionRequestStatus.Failed, r.Status);
        Assert.Equal("Err_AlreadyBooked", r.FailureReason);
        Assert.True(r.IsTerminal());
    }

    /// <summary>ReleaseClaim ShouldKeepApproved.</summary>
    [Fact]
    public void ReleaseClaim_ShouldKeepApproved()
    {
        var r = ClaimedRequest();
        r.ReleaseClaim();
        Assert.Equal(AiActionRequestStatus.Approved, r.Status);
        Assert.Null(r.ClaimToken);
        Assert.Equal(1, r.AttemptCount);
    }

    /// <summary>WithdrawBySystem ShouldWorkFromPendingAndApproved.</summary>
    [Fact]
    public void WithdrawBySystem_ShouldWorkFromPendingAndApproved()
    {
        var pending = NewRequest();
        pending.WithdrawBySystem("Zugang entzogen");
        Assert.Equal(AiActionRequestStatus.Rejected, pending.Status);
        Assert.Null(pending.DecidedByUserId);
        Assert.Equal("Zugang entzogen", pending.DecisionNote);

        var approved = ClaimedRequest();
        approved.WithdrawBySystem("Recht entfallen");
        Assert.Equal(AiActionRequestStatus.Rejected, approved.Status);
        Assert.Null(approved.ClaimToken);
    }

    /// <summary>WithdrawBySystem OnExecuted ShouldThrow.</summary>
    [Fact]
    public void WithdrawBySystem_OnExecuted_ShouldThrow()
    {
        var r = ClaimedRequest();
        r.MarkExecuted("{}");
        Assert.Throws<InvalidOperationException>(() => r.WithdrawBySystem("x"));
    }

    /// <summary>Expire ShouldWorkFromPendingAndApproved WhenDeadlinePassed.</summary>
    [Fact]
    public void Expire_ShouldWorkFromPendingAndApproved_WhenDeadlinePassed()
    {
        var now = DateTime.UtcNow;
        var pending = NewRequest(now.AddHours(-1));
        pending.Expire(now);
        Assert.Equal(AiActionRequestStatus.Expired, pending.Status);
        Assert.True(pending.IsTerminal());

        var approved = NewRequest(now.AddHours(-1));
        approved.Approve(Guid.NewGuid(), null);
        approved.Expire(now);
        Assert.Equal(AiActionRequestStatus.Expired, approved.Status);
    }

    /// <summary>Expire BeforeDeadline ShouldThrow.</summary>
    [Fact]
    public void Expire_BeforeDeadline_ShouldThrow()
    {
        var r = NewRequest(DateTime.UtcNow.AddHours(1));
        Assert.Throws<InvalidOperationException>(() => r.Expire(DateTime.UtcNow));
    }

    /// <summary>Expire OnRejected ShouldThrow.</summary>
    [Fact]
    public void Expire_OnRejected_ShouldThrow()
    {
        var r = NewRequest(DateTime.UtcNow.AddHours(-1));
        r.Reject(Guid.NewGuid(), null);
        Assert.Throws<InvalidOperationException>(() => r.Expire(DateTime.UtcNow));
    }

    /// <summary>TerminalStates ShouldRejectAllTransitions.</summary>
    [Fact]
    public void TerminalStates_ShouldRejectAllTransitions()
    {
        var executed = ClaimedRequest();
        executed.MarkExecuted("{}");
        Assert.Throws<InvalidOperationException>(() => executed.Approve(Guid.NewGuid(), null));
        Assert.Throws<InvalidOperationException>(() => executed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => executed.MarkFailed("x"));

        var failed = ClaimedRequest();
        failed.MarkFailed("x");
        Assert.Throws<InvalidOperationException>(() => failed.MarkClaimed(Guid.NewGuid(), DateTime.UtcNow));
    }

    /// <summary>CreatePreApproved ShouldStartApproved WithDocumentedDecision.</summary>
    [Fact]
    public void CreatePreApproved_ShouldStartApproved_WithDocumentedDecision()
    {
        var owner = Guid.NewGuid();
        var r = AiActionRequest.CreatePreApproved(
            owner, Guid.NewGuid(), "access", "fm_test_tool", "book", "katalog-v1",
            "{}", null, DateTime.UtcNow.AddHours(72), Guid.NewGuid(), "key-1",
            McpApprovalServiceNote);
        Assert.Equal(AiActionRequestStatus.Approved, r.Status);
        Assert.Equal(owner, r.DecidedByUserId);
        Assert.Equal(McpApprovalServiceNote, r.DecisionNote);
        Assert.NotNull(r.DecidedUtc);
    }

    private const string McpApprovalServiceNote = "Vorab-Genehmigung Zugangseinstellung";

    /// <summary>BackupDto Roundtrip ShouldRestoreState AndClearClaim.</summary>
    [Fact]
    public void BackupDto_Roundtrip_ShouldRestoreState_AndClearClaim()
    {
        var r = ClaimedRequest();
        var dto = r.ToBackupDto();
        var restored = new AiActionRequest(
            Guid.NewGuid(), Guid.NewGuid(), "x", "t", "a", "v", "{}", null,
            DateTime.UtcNow.AddHours(1), Guid.NewGuid(), "k");
        restored.AssignBackupDto(dto);
        Assert.Equal(dto.Status, restored.Status);
        Assert.Equal(dto.ParameterSnapshot, restored.ParameterSnapshot);
        Assert.Equal(dto.IdempotencyKey, restored.IdempotencyKey);
        Assert.Null(restored.ClaimToken);
        Assert.Equal(dto.AttemptCount, restored.AttemptCount);
    }
}
