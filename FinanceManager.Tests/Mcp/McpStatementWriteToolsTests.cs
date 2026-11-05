using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.Infrastructure.MassImport;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the EP-07 write-preparing tools (T16 <c>fm_statement_draft_import</c>,
/// T17 <c>fm_statement_draft_complete</c>, T18 <c>fm_statement_draft_edit</c>) plus the
/// fachliche Zusatzvalidierung (F-0704): descriptor/scope contract, gap-only completion
/// semantics, dryRun, atomic rejection, record-scope denials, audit staging and the
/// untrusted_data wrapper convention.
/// </summary>
public sealed class McpStatementWriteToolsTests
{
    private static McpAccess NewAccess(Guid owner, McpAccessLevel level = McpAccessLevel.Sachbearbeiter)
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
                ? McpLevelProfiles.GetDefaultScopes(McpAccessLevel.Sachbearbeiter, isAdmin: true)
                    .Select(t => new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(), t.DataType, t.Right)).ToList()
                : scopes.ToList());
        return new McpRecordScopeFilter(evaluator.Object);
    }

    private static Mock<IMcpAuditWriter> NullAudit()
    {
        var audit = new Mock<IMcpAuditWriter>();
        audit.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAuditEntry?)null);
        return audit;
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement ArgsObj(object args) => JsonDocument.Parse(JsonSerializer.Serialize(args)).RootElement;

    private static JsonDocument Serialize(object result) => JsonDocument.Parse(JsonSerializer.Serialize(result));

    private static async IAsyncEnumerable<StatementDraftDto> AsyncEnum(params StatementDraftDto[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static StatementDraftDto NewDraft(Guid? draftId = null, Guid? accountId = null, string fileName = "statement.pdf")
        => new(draftId ?? Guid.NewGuid(), fileName, null, accountId, null, null,
            StatementDraftStatus.Draft, 0m, false, null, null, null, Guid.NewGuid(),
            Array.Empty<StatementDraftEntryDto>());

    private static StatementDraftEntryDto NewEntry(
        Guid? id = null, Guid? contactId = null, Guid? savingsPlanId = null, Guid? securityId = null,
        StatementDraftEntryStatus status = StatementDraftEntryStatus.Open,
        bool isCostNeutral = false, bool archive = false, Guid? splitDraftId = null,
        SecurityTransactionType? txType = null, decimal amount = -10m)
        => new(id ?? Guid.NewGuid(), 1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 10), amount,
            "EUR", "subject", "recipient", null, false, isCostNeutral, status,
            contactId, savingsPlanId, archive, splitDraftId, securityId, txType, null, null, null);

    private static ContactDto NewContact(Guid? id = null, ContactType type = ContactType.Organization, bool intermediary = false)
        => new(id ?? Guid.NewGuid(), "contact", type, null, null, intermediary, null);

    private static AccountDto NewAccount(Guid? id = null, Guid? bankContactId = null,
        SavingsPlanExpectation expectation = SavingsPlanExpectation.Optional, bool securities = true)
        => new(id ?? Guid.NewGuid(), "giro", AccountType.Giro, "DE001", 0m,
            bankContactId ?? Guid.NewGuid(), null, expectation, securities);

    // ------------------------------------------------------------------
    // Catalog contract
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public void Registry_AcceptsTheWriteCatalog_AndScopesMatchTheCatalog()
    {
        var descriptors = new IMcpToolDescriptor[]
        {
            new FmStatementDraftImportDescriptor(),
            new FmStatementDraftCompleteDescriptor(),
            new FmStatementDraftEditDescriptor()
        };
        var registry = new McpToolRegistry(descriptors);
        Assert.Equal(3, registry.Tools.Count);
        Assert.All(registry.Tools, t =>
        {
            Assert.Equal(McpToolSecurityClass.Schreibvorbereitend, t.SecurityClass);
            Assert.All(t.RequiredScopes, s =>
                Assert.True(McpScopeMatrix.IsValidCombination(s.DataType, s.Right)));
            Assert.Equal(JsonValueKind.Object, t.InputSchema.RootElement.ValueKind);
        });

        Assert.Equal(new[] { new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Create) },
            registry.Find("fm_statement_draft_import")!.RequiredScopes);
        Assert.Equal(new[]
            {
                new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Complete),
                new McpToolScope(McpDataType.Contacts, McpRightKind.Read),
                new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Read),
                new McpToolScope(McpDataType.Securities, McpRightKind.Read),
                new McpToolScope(McpDataType.Accounts, McpRightKind.Read)
            },
            registry.Find("fm_statement_draft_complete")!.RequiredScopes);
        Assert.Equal(new[] { new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Modify) },
            registry.Find("fm_statement_draft_edit")!.RequiredScopes);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void Complete_Descriptor_DocumentsPreValidationThenSuccessiveApply()
    {
        // Acceptance fix A2: the apply phase commits per entry — the description must
        // not promise an atomic multi-entry transaction.
        var descriptor = new FmStatementDraftCompleteDescriptor();
        Assert.DoesNotContain("atomic", descriptor.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("all-or-error", descriptor.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pre-validation", descriptor.Description);
        Assert.Contains("no multi-entry transaction frame", descriptor.Description);
    }

    // ------------------------------------------------------------------
    // T16 fm_statement_draft_import
    // ------------------------------------------------------------------

    private static FmStatementDraftImportHandler ImportHandler(
        Mock<IStatementDraftService> drafts, Mock<IMassImportOrchestrator>? mass = null,
        Mock<IAccountService>? accounts = null, Mock<IMcpAuditWriter>? audit = null)
        => new(drafts.Object,
            (mass ?? new Mock<IMassImportOrchestrator>()).Object,
            (accounts ?? new Mock<IAccountService>()).Object,
            (audit ?? NullAudit()).Object,
            Options.Create(new MassImportOptions()));

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Import_Upload_PassesBytesToPipeline_AndMarksUntrustedData()
    {
        var owner = Guid.NewGuid();
        var draft = NewDraft();
        var drafts = new Mock<IStatementDraftService>();
        byte[]? captured = null;
        string? capturedName = null;
        drafts.Setup(d => d.CreateDraftAsync(owner, It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, byte[], CancellationToken>((_, n, b, _) => { capturedName = n; captured = b; })
            .Returns(AsyncEnum(draft));
        var staged = NullAudit();
        McpAuditWriteRequest? stagedRequest = null;
        staged.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<McpAuditWriteRequest, CancellationToken>((r, _) => stagedRequest = r)
            .ReturnsAsync((McpAuditEntry?)null);

        var content = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var handler = ImportHandler(drafts, audit: staged);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"originalFileName\":\"a.pdf\",\"contentBase64\":\"{content}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal(new byte[] { 1, 2, 3 }, captured);
        Assert.Equal("a.pdf", capturedName);
        Assert.True(doc.RootElement.GetProperty("recognized").GetBoolean());
        Assert.Equal(1, doc.RootElement.GetProperty("draftCount").GetInt32());
        var d = doc.RootElement.GetProperty("drafts")[0];
        Assert.Equal(draft.DraftId, d.GetProperty("draftId").GetGuid());
        Assert.Equal("a.pdf", doc.RootElement.GetProperty("untrusted_data").GetProperty("originalFileName").GetString());

        // Audit staged before the effect, carrying metadata + sha256 — never the content.
        Assert.NotNull(stagedRequest);
        Assert.Equal(McpAuditCategory.DataChange, stagedRequest!.Category);
        Assert.Equal("DraftImported", stagedRequest.EventType);
        Assert.Contains("sha256", stagedRequest.PayloadJson);
        Assert.DoesNotContain(content, stagedRequest.PayloadJson);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Import_Upload_UnrecognizedFile_ReportsNotRecognized()
    {
        var owner = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.CreateDraftAsync(owner, It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnum());
        var handler = ImportHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"originalFileName\":\"x.bin\",\"contentBase64\":\"{Convert.ToBase64String(new byte[] { 9 })}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.False(doc.RootElement.GetProperty("recognized").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("draftCount").GetInt32());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Import_RejectsInvalidBase64AndOversizedContent()
    {
        var drafts = new Mock<IStatementDraftService>();
        var handler = ImportHandler(drafts);
        var ctx = NewCallContext(NewAccessContext(Guid.NewGuid()).Object);

        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args("{\"originalFileName\":\"a.pdf\",\"contentBase64\":\"!!!\"}"), ctx, CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);

        var oversized = Convert.ToBase64String(new byte[McpStatementDraftGuards.MaxUploadBytes + 1]);
        ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args($"{{\"originalFileName\":\"a.pdf\",\"contentBase64\":\"{oversized}\"}}"), ctx, CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
        drafts.Verify(d => d.CreateDraftAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Import_Empty_CreatesEmptyDraft()
    {
        var owner = Guid.NewGuid();
        var draft = NewDraft();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.CreateEmptyDraftAsync(owner, "leer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        var handler = ImportHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"mode\":\"empty\",\"originalFileName\":\"leer\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal(1, doc.RootElement.GetProperty("draftCount").GetInt32());
        Assert.Equal(draft.DraftId, doc.RootElement.GetProperty("drafts")[0].GetProperty("draftId").GetGuid());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Import_Preliminary_RejectsForeignAccountReference()
    {
        var owner = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountDto?)null);
        var drafts = new Mock<IStatementDraftService>();
        var handler = ImportHandler(drafts, accounts: accounts);

        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args($"{{\"mode\":\"preliminary\",\"accountId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
        drafts.Verify(d => d.CreatePreliminaryDraftAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Import_MassImport_RoutesToOrchestrator_WithConfirmExecution()
    {
        var owner = Guid.NewGuid();
        var mass = new Mock<IMassImportOrchestrator>();
        MassImportBatchRequestDto? captured = null;
        mass.Setup(m => m.ProcessAsync(owner, It.IsAny<MassImportBatchRequestDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, MassImportBatchRequestDto, string, CancellationToken>((_, r, _, _) => captured = r)
            .ReturnsAsync(new MassImportBatchResultDto
            {
                BatchId = Guid.NewGuid(),
                Files = new[]
                {
                    new MassImportBatchFileResultDto
                    {
                        FileName = "a.pdf", FileType = MassImportFileType.AccountStatement,
                        CanImport = true, ExecutionStatus = MassImportFileExecutionStatus.Imported,
                        StatementDraftId = Guid.NewGuid()
                    }
                }
            });
        var handler = ImportHandler(new Mock<IStatementDraftService>(), mass: mass);
        var b64 = Convert.ToBase64String(new byte[] { 5 });
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"files\":[{{\"fileName\":\"a.pdf\",\"contentBase64\":\"{b64}\"}}]}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.NotNull(captured);
        Assert.True(captured!.ConfirmExecution);
        Assert.Single(captured.Files);
        Assert.Equal("Imported", doc.RootElement.GetProperty("files")[0].GetProperty("executionStatus").GetString());
        Assert.Equal("a.pdf", doc.RootElement.GetProperty("files")[0].GetProperty("untrusted_data").GetProperty("fileName").GetString());
    }

    // ------------------------------------------------------------------
    // T17 fm_statement_draft_complete
    // ------------------------------------------------------------------

    private static FmStatementDraftCompleteHandler CompleteHandler(
        Mock<IStatementDraftService> drafts, Mock<IContactService>? contacts = null,
        Mock<ISavingsPlanService>? plans = null, Mock<ISecurityService>? securities = null,
        Mock<IAccountService>? accounts = null, Mock<IMcpAuditWriter>? audit = null,
        McpRecordScopeFilter? filter = null)
        => new(drafts.Object,
            (contacts ?? new Mock<IContactService>()).Object,
            (plans ?? new Mock<ISavingsPlanService>()).Object,
            (securities ?? new Mock<ISecurityService>()).Object,
            (accounts ?? new Mock<IAccountService>()).Object,
            (audit ?? NullAudit()).Object,
            filter ?? NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Complete_FillsOnlyGaps_AndNeverOverwritesExistingValues()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var existingContact = Guid.NewGuid();
        var newContact = Guid.NewGuid();
        var entryWithContact = NewEntry(id: Guid.NewGuid(), contactId: existingContact);
        var entryWithout = NewEntry(id: Guid.NewGuid());
        var header = NewDraft(draftId, Guid.NewGuid());

        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(header);
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entryWithContact, entryWithout });
        drafts.Setup(d => d.SaveEntryAllAsync(
                draftId, It.IsAny<Guid>(), owner,
                It.IsAny<Guid?>(), It.IsAny<bool?>(), It.IsAny<Guid?>(), It.IsAny<bool?>(),
                It.IsAny<Guid?>(), It.IsAny<SecurityTransactionType?>(),
                It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entryWithout);

        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(newContact, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(newContact));
        contacts.Setup(c => c.GetAsync(existingContact, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(existingContact));

        var handler = CompleteHandler(drafts, contacts: contacts);
        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new
        {
            draftId,
            entries = new object[]
                {
                    new { entryId = entryWithContact.Id, contactId = newContact },
                    new { entryId = entryWithout.Id, contactId = newContact }
                }
        }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        // Gap entry got its fill via SaveEntryAll with the effective contact value …
        drafts.Verify(d => d.SaveEntryAllAsync(
            draftId, entryWithout.Id, owner,
            newContact, It.IsAny<bool?>(), It.IsAny<Guid?>(), It.IsAny<bool?>(),
            It.IsAny<Guid?>(), It.IsAny<SecurityTransactionType?>(),
            It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        // … while the already-assigned entry was left untouched (no write at all).
        drafts.Verify(d => d.SaveEntryAllAsync(
            draftId, entryWithContact.Id, owner,
            It.IsAny<Guid?>(), It.IsAny<bool?>(), It.IsAny<Guid?>(), It.IsAny<bool?>(),
            It.IsAny<Guid?>(), It.IsAny<SecurityTransactionType?>(),
            It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
            It.IsAny<CancellationToken>()), Times.Never);

        var entries = doc.RootElement.GetProperty("result").GetProperty("entries");
        var skip = entries[0].GetProperty("skipped");
        Assert.Equal("alreadySet", skip[0].GetProperty("reason").GetString());
        Assert.Contains("ContactId", entries[1].GetProperty("filled").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.Equal(1, doc.RootElement.GetProperty("result").GetProperty("appliedFieldCount").GetInt32());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Complete_DryRun_WritesNothing()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var entry = NewEntry();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(contactId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(contactId));
        var audit = NullAudit();
        var handler = CompleteHandler(drafts, contacts: contacts, audit: audit);

        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new { draftId, dryRun = true, entries = new[] { new { entryId = entry.Id, contactId } } }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.Equal(1, doc.RootElement.GetProperty("result").GetProperty("plannedFieldCount").GetInt32());
        drafts.Verify(d => d.SaveEntryAllAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid?>(), It.IsAny<bool?>(), It.IsAny<Guid?>(), It.IsAny<bool?>(),
            It.IsAny<Guid?>(), It.IsAny<SecurityTransactionType?>(),
            It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        audit.Verify(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Complete_ForeignReference_FailsAtomically_BeforeAnyWrite()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var foreignContact = Guid.NewGuid();
        var entry1 = NewEntry();
        var entry2 = NewEntry();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry1, entry2 });
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(foreignContact, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactDto?)null); // foreign/missing

        var handler = CompleteHandler(drafts, contacts: contacts);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(ArgsObj(new
        {
            draftId,
            entries = new object[]
                {
                    new { entryId = entry1.Id, contactId = foreignContact },
                    new { entryId = entry2.Id, contactId = foreignContact }
                }
        }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
        // Atomicity: the whole call was rejected before the first write.
        drafts.Verify(d => d.SaveEntryAllAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid?>(), It.IsAny<bool?>(), It.IsAny<Guid?>(), It.IsAny<bool?>(),
            It.IsAny<Guid?>(), It.IsAny<SecurityTransactionType?>(),
            It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Complete_SecurityFill_RequiresTransactionType()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var bankContactId = Guid.NewGuid();
        var account = NewAccount(bankContactId: bankContactId);
        var accountId = account.Id;
        var securityId = Guid.NewGuid();
        var entry = NewEntry(contactId: bankContactId);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, accountId));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(bankContactId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(bankContactId, ContactType.Bank));
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(securityId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityDto { Id = securityId, IsActive = true });
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = CompleteHandler(drafts, contacts: contacts, securities: securities, accounts: accounts);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(ArgsObj(new { draftId, entries = new[] { new { entryId = entry.Id, securityId } } }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
        Assert.Contains("SecurityTransactionType", ex.Message);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Complete_RecordFilteredDraft_Denies()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        // Complete row restricted to account type 1 — the draft has no detected account → denied.
        var filtered = new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(),
            McpDataType.StatementDrafts, McpRightKind.Complete);
        filtered.SetFilters("1", null, null, null, null);
        var filter = NewFilter(filtered);

        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId));
        var handler = CompleteHandler(drafts, filter: filter);

        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\",\"description\":\"x\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal("Err_Mcp_ScopeDenied", ex.Code);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Complete_NotFound_IsInvisible()
    {
        var owner = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StatementDraftDto?)null);
        var handler = CompleteHandler(drafts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{Guid.NewGuid()}\",\"description\":\"x\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("result").ValueKind);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Complete_StagesDataChangeAudit_WithFieldDelta()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var entry = NewEntry();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        drafts.Setup(d => d.SaveEntryAllAsync(
                draftId, entry.Id, owner,
                It.IsAny<Guid?>(), It.IsAny<bool?>(), It.IsAny<Guid?>(), It.IsAny<bool?>(),
                It.IsAny<Guid?>(), It.IsAny<SecurityTransactionType?>(),
                It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(contactId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(contactId));
        var audit = NullAudit();
        McpAuditWriteRequest? stagedRequest = null;
        audit.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<McpAuditWriteRequest, CancellationToken>((r, _) => stagedRequest = r)
            .ReturnsAsync((McpAuditEntry?)null);

        var handler = CompleteHandler(drafts, contacts: contacts, audit: audit);
        await handler.InvokeAsync(ArgsObj(new { draftId, entries = new[] { new { entryId = entry.Id, contactId } } }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None);

        Assert.NotNull(stagedRequest);
        Assert.Equal(McpAuditCategory.DataChange, stagedRequest!.Category);
        Assert.Equal("DraftCompleted", stagedRequest.EventType);
        var change = Assert.Single(stagedRequest.Changes!);
        Assert.Equal("ContactId", change.Field);
        Assert.Null(change.OldValue);
        Assert.Equal(contactId.ToString(), change.NewValue);
    }

    // ------------------------------------------------------------------
    // T18 fm_statement_draft_edit
    // ------------------------------------------------------------------

    private static FmStatementDraftEditHandler EditHandler(
        Mock<IStatementDraftService> drafts, Mock<IMcpScopeEvaluator>? evaluator = null,
        Mock<IMcpAuditWriter>? audit = null, McpRecordScopeFilter? filter = null)
        => new(drafts.Object,
            new Mock<IContactService>().Object,
            new Mock<ISavingsPlanService>().Object,
            new Mock<ISecurityService>().Object,
            new Mock<IAccountService>().Object,
            (evaluator ?? AllowAllEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            filter ?? NewFilter());

    private static Mock<IMcpScopeEvaluator> AllowAllEvaluator()
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(),
                It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        evaluator.Setup(e => e.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpLevelProfiles.GetDefaultScopes(McpAccessLevel.Sachbearbeiter, isAdmin: true)
                .Select(t => new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(), t.DataType, t.Right)).ToList());
        return evaluator;
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Edit_UpdateEntry_MergesProvidedFields_AndAuditsDelta()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var entry = NewEntry();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntryAsync(draftId, entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        decimal? capturedAmount = null;
        string? capturedSubject = null;
        drafts.Setup(d => d.UpdateEntryCoreAsync(
                draftId, entry.Id, owner,
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, DateTime, DateTime?, decimal, string, string?, string?, string?, CancellationToken>(
                (_, _, _, _, _, amount, subject, _, _, _, _) => { capturedAmount = amount; capturedSubject = subject; })
            .ReturnsAsync(entry with { Amount = -99m });
        var audit = NullAudit();
        McpAuditWriteRequest? stagedRequest = null;
        audit.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<McpAuditWriteRequest, CancellationToken>((r, _) => stagedRequest = r)
            .ReturnsAsync((McpAuditEntry?)null);

        var handler = EditHandler(drafts, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new { draftId, operation = "updateEntry", entryId = entry.Id, entry = new { amount = -99m } }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal(-99m, capturedAmount);
        Assert.Equal("subject", capturedSubject); // untouched fields keep current values
        Assert.NotNull(stagedRequest);
        var change = Assert.Single(stagedRequest!.Changes!);
        Assert.Equal("Amount", change.Field);
        Assert.Equal("-10", change.OldValue);
        Assert.Equal("-99", change.NewValue);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Edit_UpdateEntry_RejectsNonCorrectableField()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var entry = NewEntry();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntryAsync(draftId, entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        var handler = EditHandler(drafts);

        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(ArgsObj(new { draftId, operation = "updateEntry", entryId = entry.Id, entry = new { contactId = Guid.NewGuid() } }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidArguments", ex.Code);
    }

    /// <summary>
    /// AlreadyBooked stays a hard lock; a zero amount is a legal domain value
    /// (StatementEntry: "Zero is allowed for neutral items") and must be applied.
    /// </summary>
    [Fact]
    public async Task Edit_UpdateEntry_RejectsAlreadyBooked_AllowsZeroAmount()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var booked = NewEntry(status: StatementDraftEntryStatus.AlreadyBooked);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntryAsync(draftId, booked.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booked);
        var handler = EditHandler(drafts);
        var ctx = NewCallContext(NewAccessContext(owner).Object);

        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(ArgsObj(new { draftId, operation = "updateEntry", entryId = booked.Id, entry = new { amount = -1m } }), ctx, CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidState", ex.Code);

        var open = NewEntry();
        drafts.Setup(d => d.GetDraftEntryAsync(draftId, open.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(open);
        drafts.Setup(d => d.UpdateEntryCoreAsync(
                draftId, open.Id, owner,
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(), 0m,
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(open with { Amount = 0m });
        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new { draftId, operation = "updateEntry", entryId = open.Id, entry = new { amount = 0m } }), ctx, CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
    }

    /// <summary>
    /// Regression: addEntry with a zero amount is domain-legal and must reach the
    /// service (neutral items — same contract as updateEntry).
    /// </summary>
    [Fact]
    public async Task Edit_AddEntry_ZeroAmount_IsApplied()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var header = NewDraft(draftId, Guid.NewGuid());
        var newEntry = NewEntry(id: Guid.NewGuid(), amount: 0m);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(header);
        drafts.Setup(d => d.AddEntryAsync(draftId, owner, It.IsAny<DateTime>(), 0m, "x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(header with { Entries = new[] { newEntry } });
        var handler = EditHandler(drafts);

        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new
        {
            draftId,
            operation = "addEntry",
            entry = new { bookingDate = "2026-01-10", amount = 0m, subject = "x" }
        }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        drafts.Verify(d => d.AddEntryAsync(draftId, owner, It.IsAny<DateTime>(), 0m, "x", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Edit_AddEntry_RequiresCreateScope()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), McpDataType.StatementDrafts,
                McpRightKind.Create, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        evaluator.Setup(e => e.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<McpAccessScope>());

        var handler = EditHandler(drafts, evaluator: evaluator);
        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(ArgsObj(new { draftId, operation = "addEntry", entry = new { bookingDate = "2026-01-10", amount = -5m, subject = "x" } }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal("Err_Mcp_ScopeDenied", ex.Code);
        drafts.Verify(d => d.AddEntryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Edit_AddEntry_AddsViaService()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var header = NewDraft(draftId, Guid.NewGuid());
        var newEntry = NewEntry(id: Guid.NewGuid());
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(header);
        drafts.Setup(d => d.AddEntryAsync(draftId, owner, It.IsAny<DateTime>(), -5m, "x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(header with { Entries = new[] { newEntry } });
        var handler = EditHandler(drafts);

        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new
        {
            draftId,
            operation = "addEntry",
            entry = new { bookingDate = "2026-01-10", amount = -5m, subject = "x" }
        }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal(newEntry.Id, doc.RootElement.GetProperty("entry").GetProperty("entryId").GetGuid());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Edit_DeleteEntry_DeletesViaService()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var entry = NewEntry();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntryAsync(draftId, entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        drafts.Setup(d => d.DeleteEntryAsync(draftId, entry.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = EditHandler(drafts);

        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new { draftId, operation = "deleteEntry", entryId = entry.Id }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        drafts.Verify(d => d.DeleteEntryAsync(draftId, entry.Id, owner, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Edit_ResetDuplicate_ResetsFlaggedEntry()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var entry = NewEntry(status: StatementDraftEntryStatus.AlreadyBooked);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewDraft(draftId, Guid.NewGuid()));
        drafts.Setup(d => d.GetDraftEntryAsync(draftId, entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        drafts.Setup(d => d.ResetDuplicateEntryAsync(draftId, entry.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry with { });
        var handler = EditHandler(drafts);

        using var doc = Serialize(await handler.InvokeAsync(ArgsObj(new { draftId, operation = "resetDuplicate", entryId = entry.Id }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        drafts.Verify(d => d.ResetDuplicateEntryAsync(draftId, entry.Id, owner, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Edit_CommittedDraft_IsInvalidState()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var committed = NewDraft(draftId, Guid.NewGuid()) with { Status = StatementDraftStatus.Committed };
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(committed);
        var handler = EditHandler(drafts);

        var ex = await Assert.ThrowsAsync<McpToolException>(() => handler.InvokeAsync(ArgsObj(new { draftId, operation = "deleteEntry", entryId = Guid.NewGuid() }), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal("Err_Mcp_InvalidState", ex.Code);
    }
}
