using FinanceManager.Application.Accounts;
using FinanceManager.Application.Attachments;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Postings;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Attachments;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the EP-02 read-only tool catalog: descriptor validity of the live
/// catalog, paging clamp, untrusted_data wrapper convention, foreign-data invisibility
/// and the ownership derivation of <see cref="IStatementImportQueryService"/> (T08).
/// </summary>
public sealed class McpReadOnlyToolsTests
{
    private static McpAccess NewAccess(Guid owner, McpAccessLevel level = McpAccessLevel.Analyse)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", level, McpApprovalMode.PerAction);

    private static Mock<IMcpAccessContext> NewAccessContext(Guid owner, McpAccess? access = null)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);
        ctx.SetupGet(c => c.Access).Returns(access ?? NewAccess(owner));
        return ctx;
    }

    private static McpToolCallContext NewCallContext(IMcpAccessContext access)
        => new(access, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null);

    private static McpRecordScopeFilter NewFilter(params McpAccessScope[] scopes)
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scopes.Length == 0
                ? McpLevelProfiles.GetDefaultScopes(McpAccessLevel.Analyse, isAdmin: true)
                    .Select(t => new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(), t.DataType, t.Right)).ToList()
                : scopes.ToList());
        return new McpRecordScopeFilter(evaluator.Object);
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonDocument Serialize(object result) => JsonDocument.Parse(JsonSerializer.Serialize(result));

    private static IMcpToolDescriptor[] LiveCatalog() => new IMcpToolDescriptor[]
    {
        new FmCapabilitiesDescribeDescriptor(),
        new FmTaskStatusDescriptor(),
        new FmAccountsOverviewDescriptor(),
        new FmPostingsQueryDescriptor(),
        new FmReportingAggregateDescriptor(),
        new FmContactsLookupDescriptor(),
        new FmBudgetOverviewDescriptor(),
        new FmSavingsPlanAnalysisDescriptor(),
        new FmSecuritiesAnalysisDescriptor(),
        new FmStatementImportsReviewDescriptor(),
        new FmAttachmentsReadDescriptor(),
        new FmConfigurationReadDescriptor(),
        new FmStatementDraftsReadDescriptor(),
        new FmStatementDraftValidateDescriptor(),
        new FmPostingReversalCheckDescriptor()
    };

    /// <summary>Test case.</summary>
    [Fact]
    public void Registry_AcceptsTheFullReadOnlyCatalog()
    {
        var registry = new McpToolRegistry(LiveCatalog());
        Assert.Equal(15, registry.Tools.Count);
        Assert.All(registry.Tools, t =>
        {
            Assert.Equal(McpToolSecurityClass.Lesend, t.SecurityClass);
            Assert.StartsWith("fm_", t.Name);
            // "No tool without a scope" plus valid JSON input schema
            Assert.True(t.RequiredScopes.Count > 0 || !string.IsNullOrWhiteSpace(t.MetaScope));
            Assert.Equal(JsonValueKind.Object, t.InputSchema.RootElement.ValueKind);
        });
        Assert.Equal("katalog-v1", registry.CatalogVersion);
        Assert.Null(registry.Find("fm_posting_reverse")); // write tools are not part of step 2
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void DeclaredScopes_MatchTheToolCatalog()
    {
        var expected = new Dictionary<string, (McpDataType, McpRightKind)[]>
        {
            [FmAccountsOverviewDescriptor.ToolName] = new[] { (McpDataType.Accounts, McpRightKind.Read), (McpDataType.AccountTypes, McpRightKind.Read) },
            [FmPostingsQueryDescriptor.ToolName] = new[] { (McpDataType.Postings, McpRightKind.Read) },
            [FmReportingAggregateDescriptor.ToolName] = new[] { (McpDataType.Postings, McpRightKind.Read), (McpDataType.Reports, McpRightKind.Read) },
            [FmContactsLookupDescriptor.ToolName] = new[] { (McpDataType.Contacts, McpRightKind.Read), (McpDataType.ContactCategories, McpRightKind.Read) },
            [FmBudgetOverviewDescriptor.ToolName] = new[] { (McpDataType.Budgets, McpRightKind.Read), (McpDataType.BudgetRules, McpRightKind.Read), (McpDataType.BudgetCategories, McpRightKind.Read) },
            [FmSavingsPlanAnalysisDescriptor.ToolName] = new[] { (McpDataType.SavingsPlans, McpRightKind.Read), (McpDataType.SavingsPlanCategories, McpRightKind.Read) },
            [FmSecuritiesAnalysisDescriptor.ToolName] = new[] { (McpDataType.Securities, McpRightKind.Read), (McpDataType.SecurityCategories, McpRightKind.Read) },
            [FmStatementImportsReviewDescriptor.ToolName] = new[] { (McpDataType.StatementImports, McpRightKind.Read) },
            [FmAttachmentsReadDescriptor.ToolName] = new[] { (McpDataType.Attachments, McpRightKind.Read) },
            [FmConfigurationReadDescriptor.ToolName] = new[] { (McpDataType.Reports, McpRightKind.Read), (McpDataType.KpiConfigurations, McpRightKind.Read) },
            [FmStatementDraftsReadDescriptor.ToolName] = new[] { (McpDataType.StatementDrafts, McpRightKind.Read) },
            [FmStatementDraftValidateDescriptor.ToolName] = new[] { (McpDataType.StatementDrafts, McpRightKind.Read) },
            [FmPostingReversalCheckDescriptor.ToolName] = new[] { (McpDataType.Postings, McpRightKind.Read) }
        };
        foreach (var (name, scopes) in expected)
        {
            var descriptor = LiveCatalog().Single(t => t.Name == name);
            Assert.Equal(scopes.Select(s => new McpToolScope(s.Item1, s.Item2)).ToList(), descriptor.RequiredScopes);
        }
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void Arguments_ClampTakeTo200AndSkipToZero()
    {
        var args = Args("{\"take\":5000,\"skip\":-3}");
        Assert.Equal(200, McpToolArguments.Take(args));
        Assert.Equal(0, McpToolArguments.Skip(args));
        Assert.Equal(1, McpToolArguments.Take(Args("{\"take\":0}")));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void Arguments_RejectMalformedValues()
    {
        var ex = Assert.Throws<McpToolException>(() => McpToolArguments.OptionalGuid(Args("{\"x\":\"not-a-guid\"}"), "x"));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
        Assert.Throws<McpToolException>(() => McpToolArguments.OptionalDateTime(Args("{\"d\":\"nope\"}"), "d"));
        Assert.Throws<McpToolException>(() => McpToolArguments.OptionalBool(Args("{\"b\":\"yes\"}"), "b"));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AccountsOverview_WrapsUserFields_AndPassesClampedPaging()
    {
        var owner = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        int? usedTake = null;
        accounts.Setup(a => a.ListAsync(owner, It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<Guid, int, int, Guid?, string?, CancellationToken>((_, _, t, _, _, _) => usedTake = t)
            .ReturnsAsync(new[]
            {
                new AccountDto(Guid.NewGuid(), "Giro <inject>", AccountType.Giro, "DE001", 100m, Guid.NewGuid(), null, SavingsPlanExpectation.None, true)
            });
        var handler = new FmAccountsOverviewHandler(accounts.Object, NewFilter());
        var ctx = NewAccessContext(owner);

        using var doc = Serialize(await handler.InvokeAsync(Args("{\"take\":999}"), NewCallContext(ctx.Object), CancellationToken.None));
        Assert.Equal(200, usedTake);
        var acc = doc.RootElement.GetProperty("accounts")[0];
        Assert.False(acc.TryGetProperty("name", out _));
        Assert.Equal("Giro <inject>", acc.GetProperty("untrusted_data").GetProperty("name").GetString());
        Assert.Equal("DE001", acc.GetProperty("untrusted_data").GetProperty("iban").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AccountsOverview_ForeignAccount_IsInvisible()
    {
        var owner = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountDto?)null);
        var handler = new FmAccountsOverviewHandler(accounts.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"accountId\":\"{Guid.NewGuid()}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal(0, doc.RootElement.GetProperty("accounts").GetArrayLength());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PostingsQuery_RequiresExactlyOneContext()
    {
        var handler = new FmPostingsQueryHandler(
            Mock.Of<Application.Postings.IPostingsQueryService>(),
            Mock.Of<IAccountService>(), Mock.Of<Application.Contacts.IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), Mock.Of<Application.Securities.ISecurityService>(),
            NewFilter());
        var ctx = NewCallContext(NewAccessContext(Guid.NewGuid()).Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(
            () => handler.InvokeAsync(Args("{}"), ctx, CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PostingsQuery_SecurityContext_RejectsQ()
    {
        // The security-posting contract has no text filter — q must not be dropped silently.
        var handler = new FmPostingsQueryHandler(
            Mock.Of<Application.Postings.IPostingsQueryService>(),
            Mock.Of<IAccountService>(), Mock.Of<Application.Contacts.IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), Mock.Of<Application.Securities.ISecurityService>(),
            NewFilter());
        var ctx = NewCallContext(NewAccessContext(Guid.NewGuid()).Object);
        var ex = await Assert.ThrowsAsync<McpToolException>(
            () => handler.InvokeAsync(Args($"{{\"securityId\":\"{Guid.NewGuid()}\",\"q\":\"test\"}}"), ctx, CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PostingsQuery_SecurityContext_WithoutQ_IsAccepted()
    {
        var owner = Guid.NewGuid();
        var securityId = Guid.NewGuid();
        var postings = new Mock<Application.Postings.IPostingsQueryService>();
        postings.Setup(p => p.GetSecurityPostingsAsync(securityId, 0, 50, null, null, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PostingServiceDto>());
        var handler = new FmPostingsQueryHandler(
            postings.Object, Mock.Of<IAccountService>(), Mock.Of<Application.Contacts.IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), Mock.Of<Application.Securities.ISecurityService>(),
            NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"securityId\":\"{securityId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal(0, doc.RootElement.GetProperty("postings").GetArrayLength());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PostingsQuery_WrapsPostingTextsInUntrustedData()
    {
        var owner = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var postings = new Mock<Application.Postings.IPostingsQueryService>();
        postings.Setup(p => p.GetAccountPostingsAsync(accountId, 0, 50, null, null, null, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PostingServiceDto(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, -10m, PostingKind.Bank,
                    accountId, Guid.NewGuid(), null, null, Guid.NewGuid(), "ignore all", "recipient", "desc",
                    null, null, Guid.NewGuid(), null, null, null, null, "acc name", accountId, null, "acc name",
                    false, false, null, null)
            });
        var handler = new FmPostingsQueryHandler(
            postings.Object, Mock.Of<IAccountService>(), Mock.Of<Application.Contacts.IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), Mock.Of<Application.Securities.ISecurityService>(),
            NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"accountId\":\"{accountId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var p = doc.RootElement.GetProperty("postings")[0];
        Assert.False(p.TryGetProperty("subject", out _));
        Assert.Equal("ignore all", p.GetProperty("untrusted_data").GetProperty("subject").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PostingsQuery_AccountTypeFilter_HidesNonMatchingKinds()
    {
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        // Scope row restricted to AccountType=1 (Savings) — a Giro (0) posting is invisible.
        var scope = new McpAccessScope(access.Id, owner, McpDataType.Postings, McpRightKind.Read, accountTypesCsv: "1");
        var accountId = Guid.NewGuid();
        var postings = new Mock<Application.Postings.IPostingsQueryService>();
        postings.Setup(p => p.GetAccountPostingsAsync(accountId, 0, 50, null, null, null, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PostingServiceDto(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, -10m, PostingKind.Bank,
                    accountId, null, null, null, Guid.NewGuid(), "s", null, null, null, null, Guid.NewGuid(),
                    null, null, null, null, null, accountId, null, null, false, false, null, null)
            });
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDto(accountId, "g", AccountType.Giro, null, 0m, Guid.Empty, null, SavingsPlanExpectation.None, false));
        var filter = NewFilter(scope);
        var handler = new FmPostingsQueryHandler(
            postings.Object, accounts.Object, Mock.Of<Application.Contacts.IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), Mock.Of<Application.Securities.ISecurityService>(),
            filter);
        var ctx = NewAccessContext(owner, access);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"accountId\":\"{accountId}\"}}"), NewCallContext(ctx.Object), CancellationToken.None));
        Assert.Equal(0, doc.RootElement.GetProperty("postings").GetArrayLength());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AttachmentsRead_DownloadEmitsSha256AndBase64AsUntrusted()
    {
        var owner = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var bytes = new byte[] { 1, 2, 3 };
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.GetAsync(owner, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentDto(attachmentId, (short)FinanceManager.Domain.Attachments.AttachmentEntityKind.Contact,
                Guid.NewGuid(), "doc.pdf", "application/pdf", 3, null, DateTime.UtcNow, false));
        attachments.Setup(a => a.DownloadAsync(owner, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new MemoryStream(bytes), "doc.pdf", "application/pdf"));
        var handler = new FmAttachmentsReadHandler(attachments.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"attachmentId\":\"{attachmentId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var att = doc.RootElement.GetProperty("attachment");
        Assert.Equal("application/pdf", att.GetProperty("contentType").GetString());
        Assert.True(att.GetProperty("sha256").GetString()!.Length == 64);
        var untrusted = att.GetProperty("untrusted_data");
        Assert.Equal("doc.pdf", untrusted.GetProperty("fileName").GetString());
        Assert.Equal(Convert.ToBase64String(bytes), untrusted.GetProperty("contentBase64").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task AttachmentsRead_ForeignAttachment_IsInvisible()
    {
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttachmentDto?)null);
        var handler = new FmAttachmentsReadHandler(attachments.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"attachmentId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(Guid.NewGuid()).Object), CancellationToken.None));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("attachment").ValueKind);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftsRead_EmitsDraftWithUntrustedTexts()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        var entry = new StatementDraftEntryDto(Guid.NewGuid(), 1, DateTime.UtcNow, null, -5m, "EUR",
            "subject <inject>", "recip", "desc", false, false, StatementDraftEntryStatus.Open,
            null, null, false, null, null, null, null, null, null);
        drafts.Setup(d => d.GetDraftAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatementDraftDto(draftId, "file.csv", "note", null, null, null,
                StatementDraftStatus.Draft, -5m, false, null, null, null, null, new[] { entry }));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        drafts.Setup(d => d.GetUploadGroupNeighborsAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, null));
        var handler = new FmStatementDraftsReadHandler(drafts.Object,
            Mock.Of<IAccountService>(), Mock.Of<Application.Contacts.IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), Mock.Of<Application.Securities.ISecurityService>(),
            NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var draft = doc.RootElement.GetProperty("draft");
        Assert.False(draft.TryGetProperty("originalFileName", out _));
        Assert.Equal("file.csv", draft.GetProperty("untrusted_data").GetProperty("originalFileName").GetString());
        var e = draft.GetProperty("entries")[0];
        Assert.Equal("subject <inject>", e.GetProperty("untrusted_data").GetProperty("subject").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftsRead_EntryIdWithoutDraftId_Throws()
    {
        // A single-entry lookup only exists inside a draft — entryId alone must not
        // silently fall into the list path (schema: "requires draftId").
        var handler = new FmStatementDraftsReadHandler(Mock.Of<IStatementDraftService>(),
            Mock.Of<IAccountService>(), Mock.Of<Application.Contacts.IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), Mock.Of<Application.Securities.ISecurityService>(),
            NewFilter());
        var ex = await Assert.ThrowsAsync<McpToolException>(
            () => handler.InvokeAsync(Args($"{{\"entryId\":\"{Guid.NewGuid()}\"}}"),
                NewCallContext(NewAccessContext(Guid.NewGuid()).Object), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftValidate_ForeignDraft_ReturnsNull()
    {
        var owner = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StatementDraftDto?)null);
        var handler = ValidateHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{Guid.NewGuid()}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("validation").ValueKind);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftValidate_MessagesAreUntrusted()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatementDraftDto(draftId, "f.csv", null, null, null, null,
                StatementDraftStatus.Draft, 0m, false, null, null, null, null, Array.Empty<StatementDraftEntryDto>()));
        drafts.Setup(d => d.ValidateAsync(draftId, null, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DraftValidationResultDto(draftId, false,
                new[] { new DraftValidationMessageDto("Err_X", "Error", "entry 'x' <inject>", draftId, Guid.NewGuid()) }));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StatementDraftEntryDto>());
        var handler = ValidateHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var msg = doc.RootElement.GetProperty("validation").GetProperty("messages")[0];
        Assert.Equal("Err_X", msg.GetProperty("code").GetString());
        Assert.Equal("entry 'x' <inject>", msg.GetProperty("untrusted_data").GetProperty("message").GetString());
    }

    // ------------------------------------------------------------------
    // T14 §4.4 Zusatzregeln (F-0704): the guards' rule family as findings
    // ------------------------------------------------------------------

    private static IMcpScopeEvaluator AllowAllScopes()
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(),
                It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return evaluator.Object;
    }

    private static FmStatementDraftValidateHandler ValidateHandler(
        Mock<IStatementDraftService> drafts,
        Mock<IContactService>? contacts = null,
        Mock<ISavingsPlanService>? plans = null,
        Mock<ISecurityService>? securities = null,
        Mock<IAccountService>? accounts = null,
        Mock<IStatementImportQueryService>? imports = null,
        IMcpScopeEvaluator? evaluator = null)
        => new(drafts.Object,
            (contacts ?? new Mock<IContactService>()).Object,
            (plans ?? new Mock<ISavingsPlanService>()).Object,
            (securities ?? new Mock<ISecurityService>()).Object,
            (accounts ?? new Mock<IAccountService>()).Object,
            (imports ?? new Mock<IStatementImportQueryService>()).Object,
            evaluator ?? AllowAllScopes(),
            NewFilter());

    private static Mock<IStatementDraftService> DraftsForValidate(
        Guid draftId, Guid owner, Guid? accountId, params StatementDraftEntryDto[] entries)
    {
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatementDraftDto(draftId, "f.csv", null, accountId, null, null,
                StatementDraftStatus.Draft, 0m, false, null, null, null, null,
                Array.Empty<StatementDraftEntryDto>()));
        drafts.Setup(d => d.ValidateAsync(draftId, It.IsAny<Guid?>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DraftValidationResultDto(draftId, true,
                Array.Empty<DraftValidationMessageDto>()));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        return drafts;
    }

    private static StatementDraftEntryDto NewValidateEntry(
        decimal amount = -10m, DateTime? bookingDate = null,
        StatementDraftEntryStatus status = StatementDraftEntryStatus.Open,
        Guid? contactId = null, Guid? savingsPlanId = null, Guid? splitDraftId = null,
        bool archive = false)
        => new(Guid.NewGuid(), 1, bookingDate ?? new DateTime(2026, 1, 10), null, amount,
            "EUR", "subject", "recipient", null, false, false, status,
            contactId, savingsPlanId, archive, splitDraftId, null, null, null, null, null);

    private static List<string?> FindingCodes(JsonDocument doc)
        => doc.RootElement.GetProperty("additionalFindings").EnumerateArray()
            .Select(f => f.GetProperty("code").GetString()).ToList();

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftValidate_AdditionalFindings_FlagsSavingsPlanSign()
    {
        // §4.4 Vorzeichen: a savings-plan contribution books as an outflow — a positive
        // amount is a payout-shaped inconsistency, not a rejection (Befund, kein Abbruch).
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var entry = NewValidateEntry(amount: +25m, contactId: Guid.NewGuid(), savingsPlanId: Guid.NewGuid());
        var drafts = DraftsForValidate(draftId, owner, null, entry);
        var handler = ValidateHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var codes = FindingCodes(doc);
        Assert.Contains("MCP_SIGN_SAVINGSPLAN", codes);
        Assert.All(doc.RootElement.GetProperty("additionalFindings").EnumerateArray(),
            f => Assert.True(f.TryGetProperty("severity", out _) && f.TryGetProperty("entryId", out _)));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftValidate_AdditionalFindings_FlagsPeriodOverlap()
    {
        // §4.4 Zeitraum-Überlappung: draft entry period intersects an already booked
        // import period of the same detected account.
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var importId = Guid.NewGuid();
        var entry = NewValidateEntry(bookingDate: new DateTime(2026, 1, 10), contactId: Guid.NewGuid());
        var drafts = DraftsForValidate(draftId, owner, accountId, entry);
        var imports = new Mock<IStatementImportQueryService>();
        imports.Setup(s => s.ListImportsAsync(owner, accountId, null, null, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new StatementImportListItemDto(importId, accountId, AccountType.Giro,
                    ImportFormat.Csv, DateTime.UtcNow, "i.csv", 2)
            });
        imports.Setup(s => s.ListEntriesAsync(owner, importId, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new StatementImportEntryDto(Guid.NewGuid(), importId, new DateTime(2026, 1, 8),
                    new DateTime(2026, 1, 8), -5m, "EUR", "s", null, null, false, false,
                    null, null, null, FinanceManager.Shared.Dtos.Statements.StatementEntryStatus.Booked),
                new StatementImportEntryDto(Guid.NewGuid(), importId, new DateTime(2026, 1, 15),
                    new DateTime(2026, 1, 15), -5m, "EUR", "s", null, null, false, false,
                    null, null, null, FinanceManager.Shared.Dtos.Statements.StatementEntryStatus.Booked)
            });
        var handler = ValidateHandler(drafts, imports: imports);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var codes = FindingCodes(doc);
        Assert.Contains("MCP_PERIOD_OVERLAP", codes);
        var finding = doc.RootElement.GetProperty("additionalFindings").EnumerateArray()
            .Single(f => f.GetProperty("code").GetString() == "MCP_PERIOD_OVERLAP");
        Assert.Equal("Warning", finding.GetProperty("severity").GetString());
        Assert.Equal(importId, finding.GetProperty("relatedRecordId").GetGuid());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftValidate_AdditionalFindings_FlagsSplitSumMismatch()
    {
        // §4.4 Split-Konsistenz: the split-group sum must cover the entry amount
        // (mirrors the pipeline rule SPLIT_AMOUNT_MISMATCH via GetSplitGroupSumAsync).
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var splitId = Guid.NewGuid();
        var entry = NewValidateEntry(amount: -30m, contactId: Guid.NewGuid(), splitDraftId: splitId);
        var drafts = DraftsForValidate(draftId, owner, null, entry);
        drafts.Setup(d => d.GetSplitGroupSumAsync(splitId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(-20m);
        var handler = ValidateHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var codes = FindingCodes(doc);
        Assert.Contains("MCP_SPLIT_SUM_MISMATCH", codes);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftValidate_AdditionalFindings_FlagsLockedAndUnresolvedEntries()
    {
        // §4.4: fachlich gesperrte Entries (AlreadyBooked) are findings, and a contact
        // that does not resolve owner-scoped is an unresolved reference (Fremd-Bezug).
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var entry = NewValidateEntry(status: StatementDraftEntryStatus.AlreadyBooked,
            contactId: Guid.NewGuid());
        var drafts = DraftsForValidate(draftId, owner, null, entry);
        var handler = ValidateHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var codes = FindingCodes(doc);
        Assert.Contains("MCP_ENTRY_ALREADY_BOOKED", codes);
        Assert.Contains("MCP_CONTACT_UNRESOLVED", codes);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReversalCheck_NormalizesInaccessiblePostings()
    {
        var owner = Guid.NewGuid();
        var postingId = Guid.NewGuid();
        var reversals = new Mock<IPostingReversalService>();
        reversals.Setup(r => r.CanReverseAsync(postingId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReversalValidationDto(false, new[] { $"User {owner} is not authorized to reverse posting {postingId}" }));
        var handler = new FmPostingReversalCheckHandler(reversals.Object, Mock.Of<IMcpScopeEvaluator>());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"postingId\":\"{postingId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.False(doc.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("errors").GetArrayLength());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReversalCheck_ValidPosting_ReturnsGroup()
    {
        var owner = Guid.NewGuid();
        var access = NewAccess(owner);
        var postingId = Guid.NewGuid();
        var related = new FinanceManager.Domain.Postings.Posting(
            Guid.NewGuid(), PostingKind.Bank, Guid.NewGuid(), null, null, null,
            DateTime.UtcNow, DateTime.UtcNow, -10m, "subject", "recip", "desc", null, null);
        var reversals = new Mock<IPostingReversalService>();
        reversals.Setup(r => r.CanReverseAsync(postingId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReversalValidationDto(true, Array.Empty<string>()));
        reversals.Setup(r => r.GetRelatedPostingsAsync(postingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { related });
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.CheckRecordAccessAsync(access, McpDataType.Postings, McpRightKind.Read,
                It.IsAny<McpRecordContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpAccessVerdict.Allowed);
        var handler = new FmPostingReversalCheckHandler(reversals.Object, evaluator.Object);
        var ctx = NewAccessContext(owner, access);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"postingId\":\"{postingId}\"}}"), NewCallContext(ctx.Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("canReverse").GetBoolean());
        var rel = doc.RootElement.GetProperty("relatedPostings")[0];
        Assert.Equal("subject", rel.GetProperty("untrusted_data").GetProperty("subject").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task StatementImportsReview_WrapsEntryTextsInUntrustedData()
    {
        var owner = Guid.NewGuid();
        var importId = Guid.NewGuid();
        var imports = new Mock<IStatementImportQueryService>();
        imports.Setup(s => s.GetImportAsync(owner, importId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StatementImportListItemDto(importId, Guid.NewGuid(), AccountType.Giro,
                ImportFormat.Csv, DateTime.UtcNow, "import.csv", 1));
        imports.Setup(s => s.ListEntriesAsync(owner, importId, 0, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new StatementImportEntryDto(Guid.NewGuid(), importId, DateTime.UtcNow, null, -1m, "EUR",
                    "subject <inject>", "recip", "desc", false, false, null, null, null,
                    FinanceManager.Shared.Dtos.Statements.StatementEntryStatus.Booked)
            });
        imports.Setup(s => s.CountEntriesAsync(owner, importId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var handler = new FmStatementImportsReviewHandler(imports.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"importId\":\"{importId}\"}}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var entry = doc.RootElement.GetProperty("imports")[0].GetProperty("entries")[0];
        Assert.Equal("subject <inject>", entry.GetProperty("untrusted_data").GetProperty("subject").GetString());
        Assert.Equal("import.csv", doc.RootElement.GetProperty("imports")[0]
            .GetProperty("untrusted_data").GetProperty("originalFileName").GetString());
    }
}

/// <summary>
/// Persistence tests of <see cref="StatementImportQueryService"/>: ownership derivation
/// via the anchor account (T08, F-0204).
/// </summary>
public sealed class StatementImportQueryServiceTests
{
    private static AppDbContext NewDb()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return services.BuildServiceProvider().GetRequiredService<AppDbContext>();
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Imports_AreOnlyVisibleViaOwnedAccounts()
    {
        var db = NewDb();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var ownAccount = new Account(owner, AccountType.Giro, "own", null, Guid.NewGuid());
        var foreignAccount = new Account(other, AccountType.Savings, "foreign", null, Guid.NewGuid());
        db.Accounts.AddRange(ownAccount, foreignAccount);
        db.StatementImports.Add(new StatementImport(ownAccount.Id, ImportFormat.Csv, "own.csv"));
        db.StatementImports.Add(new StatementImport(foreignAccount.Id, ImportFormat.Pdf, "foreign.pdf"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new StatementImportQueryService(db);
        var list = await svc.ListImportsAsync(owner, null, null, null, 0, 50, CancellationToken.None);
        Assert.Single(list);
        Assert.Equal("own.csv", list[0].OriginalFileName);
        Assert.Equal(AccountType.Giro, list[0].AccountType);
        Assert.Equal(1, await svc.CountImportsAsync(owner, null, CancellationToken.None));
        Assert.Null(await svc.GetImportAsync(owner, db.StatementImports.First(i => i.AccountId == foreignAccount.Id).Id, CancellationToken.None));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Entries_FollowTheImportOwnership_AndClampTake()
    {
        var db = NewDb();
        var owner = Guid.NewGuid();
        var account = new Account(owner, AccountType.Giro, "a", null, Guid.NewGuid());
        var foreignAccount = new Account(Guid.NewGuid(), AccountType.Giro, "f", null, Guid.NewGuid());
        db.Accounts.AddRange(account, foreignAccount);
        var import = new StatementImport(account.Id, ImportFormat.Csv, "i.csv");
        var foreignImport = new StatementImport(foreignAccount.Id, ImportFormat.Csv, "f.csv");
        db.StatementImports.AddRange(import, foreignImport);
        db.StatementEntries.Add(new StatementEntry(import.Id, DateTime.UtcNow, -1m, "s", "h1", null, null, "EUR", null, false, false));
        db.StatementEntries.Add(new StatementEntry(foreignImport.Id, DateTime.UtcNow, -2m, "sf", "h2", null, null, "EUR", null, false, false));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = new StatementImportQueryService(db);
        var entries = await svc.ListEntriesAsync(owner, import.Id, 0, 500, CancellationToken.None);
        Assert.Single(entries);
        Assert.Equal(FinanceManager.Shared.Dtos.Statements.StatementEntryStatus.Pending, entries[0].Status);
        Assert.Equal(1, await svc.CountEntriesAsync(owner, import.Id, CancellationToken.None));

        // Foreign import: entries invisible (empty, not an error).
        Assert.Empty(await svc.ListEntriesAsync(owner, foreignImport.Id, 0, 50, CancellationToken.None));
        Assert.Equal(0, await svc.CountEntriesAsync(owner, foreignImport.Id, CancellationToken.None));
    }
}
