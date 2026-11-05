using FinanceManager.Application;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Postings;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// EP-10 tests: the <c>executeApproved</c> delegates of the T22–T29 tools perform the real
/// service effect under <see cref="McpToolExecutionMode.ApprovedExecution"/>; the final
/// tools T20 <c>fm_statement_draft_book</c>, T21 <c>fm_statement_draft_discard</c> and T31
/// <c>fm_posting_reverse</c> answer requiresApproval interactively and execute via the
/// regular service paths on the queue; the server-side effect preview classifies the
/// frozen snapshots.
/// </summary>
public sealed class McpFinalToolsAndDelegatesTests
{
    private static McpAccess NewAccess(Guid owner)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", McpAccessLevel.Buchhalter, McpApprovalMode.PerAction);

    private static McpToolCallContext Ctx(Guid owner, bool approved)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);
        ctx.SetupGet(c => c.Access).Returns(NewAccess(owner));
        return new McpToolCallContext(ctx.Object, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null,
            approved ? McpToolExecutionMode.ApprovedExecution : McpToolExecutionMode.Interactive);
    }

    private static Mock<IMcpScopeEvaluator> AllowAll()
    {
        var e = new Mock<IMcpScopeEvaluator>();
        e.Setup(x => x.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(), It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return e;
    }

    private static Mock<IMcpAuditWriter> NullAudit()
    {
        var a = new Mock<IMcpAuditWriter>();
        a.Setup(x => x.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAuditEntry?)null);
        return a;
    }

    private static McpRecordScopeFilter Filter()
    {
        var e = new Mock<IMcpScopeEvaluator>();
        e.Setup(x => x.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<McpAccessScope>());
        return new McpRecordScopeFilter(e.Object);
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    // -------------------------------------------------------------- delegates

    /// <summary>ApprovedExecution runs the real delete effect incl. DataChange staging.</summary>
    [Fact]
    public async Task ContactsDelete_ApprovedExecution_CallsService()
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(id, "n", ContactType.Person, null, null, false, null));
        contacts.Setup(c => c.DeleteAsync(id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new FmContactsManageHandler(contacts.Object,
            new Mock<IContactCategoryService>().Object, AllowAll().Object, NullAudit().Object, Filter());
        var result = await handler.InvokeAsync(Args($"{{\"operation\":\"delete\",\"contactId\":\"{id}\"}}"),
            Ctx(owner, approved: true), CancellationToken.None);
        contacts.Verify(c => c.DeleteAsync(id, owner, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.GetProperty("applied").GetBoolean());
    }

    /// <summary>A vanished target ends the queue execution classified (§5.2).</summary>
    [Fact]
    public async Task ContactsDelete_ApprovedExecution_TargetGone()
    {
        var owner = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactDto?)null);
        var handler = new FmContactsManageHandler(contacts.Object,
            new Mock<IContactCategoryService>().Object, AllowAll().Object, NullAudit().Object, Filter());
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args($"{{\"operation\":\"delete\",\"contactId\":\"{Guid.NewGuid()}\"}}"),
            Ctx(owner, approved: true), CancellationToken.None));
        Assert.Equal("Err_TargetGone", ex.Code);
        contacts.Verify(c => c.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Merge executes the irreversible service merge after approval.</summary>
    [Fact]
    public async Task ContactsMerge_ApprovedExecution_CallsMerge()
    {
        var owner = Guid.NewGuid(); var src = Guid.NewGuid(); var dst = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid o, CancellationToken _) => new ContactDto(id, "n", ContactType.Person, null, null, false, null));
        contacts.Setup(c => c.MergeAsync(owner, src, dst, It.IsAny<CancellationToken>(), MergePreference.DestinationFirst))
            .ReturnsAsync(new ContactDto(dst, "n", ContactType.Person, null, null, false, null));
        var handler = new FmContactsManageHandler(contacts.Object,
            new Mock<IContactCategoryService>().Object, AllowAll().Object, NullAudit().Object, Filter());
        var result = await handler.InvokeAsync(Args($"{{\"operation\":\"merge\",\"contactId\":\"{src}\",\"targetContactId\":\"{dst}\"}}"),
            Ctx(owner, approved: true), CancellationToken.None);
        contacts.Verify(c => c.MergeAsync(owner, src, dst, It.IsAny<CancellationToken>(), MergePreference.DestinationFirst), Times.Once);
        Assert.True(JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.GetProperty("applied").GetBoolean());
    }

    // -------------------------------------------------------------- T20 book

    /// <summary>Interactive calls never book synchronously.</summary>
    [Fact]
    public async Task Book_Interactive_RequiresApproval_NoEffect()
    {
        var owner = Guid.NewGuid(); var draftId = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        var handler = new FmStatementDraftBookHandler(drafts.Object,
            new Mock<IBackgroundTaskManager>().Object, NullAudit().Object);
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"), Ctx(owner, approved: false), CancellationToken.None)));
        Assert.True(doc.RootElement.GetProperty("requiresApproval").GetBoolean());
        drafts.Verify(d => d.BookAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Queue execution books via the regular service path.</summary>
    [Fact]
    public async Task Book_ApprovedExecution_CallsBookAsync()
    {
        var owner = Guid.NewGuid(); var draftId = Guid.NewGuid(); var importId = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId));
        drafts.Setup(d => d.BookAsync(draftId, null, owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingResult(true, false,
                new DraftValidationResultDto(draftId, true, new List<DraftValidationMessageDto>()),
                importId, 3, null));
        var handler = new FmStatementDraftBookHandler(drafts.Object,
            new Mock<IBackgroundTaskManager>().Object, NullAudit().Object);
        var result = await handler.InvokeAsync(Args($"{{\"draftId\":\"{draftId}\",\"forceWarnings\":true}}"),
            Ctx(owner, approved: true), CancellationToken.None);
        drafts.Verify(d => d.BookAsync(draftId, null, owner, true, It.IsAny<CancellationToken>()), Times.Once);
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(importId.ToString(), doc.RootElement.GetProperty("statementImportId").GetString());
    }

    /// <summary>A vanished draft fails classified instead of booking.</summary>
    [Fact]
    public async Task Book_ApprovedExecution_DraftGone()
    {
        var owner = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StatementDraftDto?)null);
        var handler = new FmStatementDraftBookHandler(drafts.Object,
            new Mock<IBackgroundTaskManager>().Object, NullAudit().Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args($"{{\"draftId\":\"{Guid.NewGuid()}\"}}"), Ctx(owner, approved: true), CancellationToken.None));
        Assert.Equal("Err_TargetGone", ex.Code);
    }

    private static StatementDraftDto NewDraft(Guid draftId)
        => new(draftId, "f.csv", null, null, null, null, StatementDraftStatus.Draft, 0m,
            false, null, null, null, null, new List<StatementDraftEntryDto>());

    /// <summary>bookAll enqueues the mass-booking background task.</summary>
    [Fact]
    public async Task Book_BookAll_EnqueuesTask()
    {
        var owner = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var tasks = new Mock<IBackgroundTaskManager>();
        tasks.Setup(t => t.GetAll()).Returns(Array.Empty<BackgroundTaskInfo>());
        tasks.Setup(t => t.Enqueue(BackgroundTaskType.BookAllDrafts, owner, It.IsAny<object>(), false))
            .Returns(new BackgroundTaskInfo(taskId, BackgroundTaskType.BookAllDrafts, owner,
                DateTime.UtcNow, BackgroundTaskStatus.Queued, null, null, null, 0, 0, null,
                null, null, null, null, null, null));
        var audit = new GuardingAuditWriter();
        var handler = new FmStatementDraftBookHandler(new Mock<IStatementDraftService>().Object,
            tasks.Object, audit);
        var result = await handler.InvokeAsync(Args("{\"bookAll\":true}"),
            Ctx(owner, approved: true), CancellationToken.None);
        tasks.Verify(t => t.Enqueue(BackgroundTaskType.BookAllDrafts, owner, It.IsAny<object>(), false), Times.Once);
        Assert.False(JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.GetProperty("alreadyRunning").GetBoolean());
        // The batch event anchors on the enqueued task record — never an empty entity id.
        var change = Assert.Single(audit.LastRequest!.Changes!);
        Assert.Equal(taskId, change.EntityId);
        Assert.Equal("BackgroundTask", change.EntityType);
    }

    // -------------------------------------------------------------- T21 discard

    /// <summary>Queue execution discards via CancelAsync.</summary>
    [Fact]
    public async Task Discard_ApprovedExecution_Cancels()
    {
        var owner = Guid.NewGuid(); var draftId = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId));
        drafts.Setup(d => d.CancelAsync(draftId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new FmStatementDraftDiscardHandler(drafts.Object, NullAudit().Object);
        var result = await handler.InvokeAsync(Args($"{{\"draftId\":\"{draftId}\"}}"),
            Ctx(owner, approved: true), CancellationToken.None);
        drafts.Verify(d => d.CancelAsync(draftId, owner, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.GetProperty("applied").GetBoolean());
    }

    /// <summary>Mass discard requires the explicit confirmation mark.</summary>
    [Fact]
    public async Task Discard_AllWithoutConfirm_Rejected()
    {
        var handler = new FmStatementDraftDiscardHandler(new Mock<IStatementDraftService>().Object, NullAudit().Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args("{\"all\":true}"), Ctx(Guid.NewGuid(), approved: true), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
    }

    /// <summary>Confirmed mass discard calls DeleteAllAsync.</summary>
    [Fact]
    public async Task Discard_AllConfirmed_DeletesAll()
    {
        var owner = Guid.NewGuid();
        var open = new[] { NewDraft(Guid.NewGuid()), NewDraft(Guid.NewGuid()), NewDraft(Guid.NewGuid()), NewDraft(Guid.NewGuid()) };
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetOpenDraftsAsync(owner, It.IsAny<CancellationToken>())).ReturnsAsync(open);
        drafts.Setup(d => d.DeleteAllAsync(owner, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        var audit = new GuardingAuditWriter();
        var handler = new FmStatementDraftDiscardHandler(drafts.Object, audit);
        var result = await handler.InvokeAsync(Args("{\"all\":true,\"confirmDiscardAll\":true}"),
            Ctx(owner, approved: true), CancellationToken.None);
        drafts.Verify(d => d.DeleteAllAsync(owner, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(4, JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.GetProperty("discardedCount").GetInt32());
        // The batch event carries one delta row per discarded draft — real ids, no placeholder.
        Assert.Equal(open.Select(d => d.DraftId).OrderBy(id => id),
            audit.LastRequest!.Changes!.Select(c => c.EntityId).OrderBy(id => id));
    }

    // -------------------------------------------------------------- T31 reverse

    /// <summary>Queue execution reverses via the regular service path.</summary>
    [Fact]
    public async Task Reverse_ApprovedExecution_Reverses()
    {
        var owner = Guid.NewGuid(); var postingId = Guid.NewGuid();
        var reversals = new Mock<IPostingReversalService>();
        reversals.Setup(r => r.CanReverseAsync(postingId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReversalValidationDto(true, new List<string>()));
        reversals.Setup(r => r.ReversePostingAsync(postingId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReversalResultDto(new List<Guid> { postingId }, new List<Guid> { Guid.NewGuid() }, Guid.NewGuid()));
        var handler = new FmPostingReverseHandler(reversals.Object, NullAudit().Object);
        var result = await handler.InvokeAsync(Args($"{{\"postingId\":\"{postingId}\",\"reasonText\":\"x\"}}"),
            Ctx(owner, approved: true), CancellationToken.None);
        reversals.Verify(r => r.ReversePostingAsync(postingId, owner, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.GetProperty("applied").GetBoolean());
    }

    /// <summary>A changed situation (already reversed) fails classified — no double reversal.</summary>
    [Fact]
    public async Task Reverse_AlreadyReversed_Classified()
    {
        var owner = Guid.NewGuid(); var postingId = Guid.NewGuid();
        var reversals = new Mock<IPostingReversalService>();
        reversals.Setup(r => r.CanReverseAsync(postingId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReversalValidationDto(false, new List<string> { "Posting already reversed" }));
        var handler = new FmPostingReverseHandler(reversals.Object, NullAudit().Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args($"{{\"postingId\":\"{postingId}\"}}"), Ctx(owner, approved: true), CancellationToken.None));
        Assert.Equal("Err_AlreadyReversed", ex.Code);
        reversals.Verify(r => r.ReversePostingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The enqueue-time hook (F-1003): the request-creation validation of T31 —
    /// an already reversed posting is rejected before the queue entry exists.</summary>
    [Fact]
    public async Task Reverse_EnqueueValidation_AlreadyReversed_Classified()
    {
        var owner = Guid.NewGuid(); var postingId = Guid.NewGuid();
        var reversals = new Mock<IPostingReversalService>();
        reversals.Setup(r => r.CanReverseAsync(postingId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReversalValidationDto(false, new List<string> { "Posting already reversed" }));
        var handler = new FmPostingReverseHandler(reversals.Object, NullAudit().Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.ValidateRequestAsync(
            Args($"{{\"postingId\":\"{postingId}\"}}"), Ctx(owner, approved: false), CancellationToken.None));
        Assert.Equal("Err_AlreadyReversed", ex.Code);
    }

    /// <summary>The enqueue-time hook lets a reversible posting pass.</summary>
    [Fact]
    public async Task Reverse_EnqueueValidation_Reversible_Passes()
    {
        var owner = Guid.NewGuid(); var postingId = Guid.NewGuid();
        var reversals = new Mock<IPostingReversalService>();
        reversals.Setup(r => r.CanReverseAsync(postingId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReversalValidationDto(true, new List<string>()));
        var handler = new FmPostingReverseHandler(reversals.Object, NullAudit().Object);
        await handler.ValidateRequestAsync(
            Args($"{{\"postingId\":\"{postingId}\"}}"), Ctx(owner, approved: false), CancellationToken.None);
        reversals.Verify(r => r.CanReverseAsync(postingId, owner, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------- effect preview

    /// <summary>The server-side preview classifies final tools as destructive.</summary>
    [Fact]
    public void EffectPreview_ClassifiesTools()
    {
        var book = McpApprovalEffectPreview.Build("fm_statement_draft_book", "fm_statement_draft_book", "{\"draftId\":\"x\",\"forceWarnings\":true}");
        Assert.Equal("book", book!.Kind);
        Assert.True(book.IsDestructive);
        var reverse = McpApprovalEffectPreview.Build("fm_posting_reverse", "fm_posting_reverse", "{\"postingId\":\"x\"}");
        Assert.Equal("reverse", reverse!.Kind);
        var merge = McpApprovalEffectPreview.Build("fm_contacts_manage", "merge", "{}");
        Assert.Equal("merge", merge!.Kind);
        Assert.True(merge.IsDestructive);
        var discardAll = McpApprovalEffectPreview.Build("fm_statement_draft_discard", "fm_statement_draft_discard", "{\"all\":true}");
        Assert.Equal("discardAll", discardAll!.Kind);
    }
}
