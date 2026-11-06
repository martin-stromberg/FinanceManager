using FinanceManager.Application.Accounts;
using FinanceManager.Application.Attachments;
using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Reports;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Attachments;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.HomeKpi;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Reports;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the EP-08 write-preparing master-data tools (T22–T29): descriptor/scope
/// contract per toolkatalog §3.4, direct create/update/alias/link operations with
/// transactional DataChange audit coupling, owner-scoped foreign-reference validation,
/// record-level scope denial, untrusted_data marking and the structured
/// <c>Err_Mcp_ApprovalRequired</c> answer of the genehmigungspflichtigen Teiloperationen
/// (merge/delete/archive) — which must produce <b>no</b> service effect until EP-09.
/// </summary>
public sealed class McpMasterDataWriteToolsTests
{
    private static McpAccess NewAccess(Guid owner, McpAccessLevel level = McpAccessLevel.Buchhalter)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", level, McpApprovalMode.PerAction);

    private static Mock<IMcpAccessContext> NewAccessContext(Guid owner, McpAccess? access = null)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);
        ctx.SetupGet(c => c.Access).Returns(access ?? NewAccess(owner));
        return ctx;
    }

    private static McpToolCallContext NewCallContext(IMcpAccessContext access,
        McpToolExecutionMode mode = McpToolExecutionMode.Interactive)
        => new(access, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null, mode);

    private static McpRecordScopeFilter NewFilter(params McpAccessScope[] scopes)
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scopes.Length == 0
                ? McpLevelProfiles.GetDefaultScopes(McpAccessLevel.Buchhalter, isAdmin: true)
                    .Select(t => new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(), t.DataType, t.Right)).ToList()
                : scopes.ToList());
        return new McpRecordScopeFilter(evaluator.Object);
    }

    private static Mock<IMcpScopeEvaluator> NewScopeEvaluator(bool allowDelete = true, bool allowModify = true)
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(), McpRightKind.Delete, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowDelete);
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(), McpRightKind.Modify, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowModify);
        return evaluator;
    }

    private static Mock<IMcpAuditWriter> NullAudit()
    {
        var audit = new Mock<IMcpAuditWriter>();
        audit.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((McpAuditEntry?)null);
        return audit;
    }

    private static Mock<IMcpAuditWriter> CapturingAudit(out Func<McpAuditWriteRequest?> staged)
    {
        var audit = NullAudit();
        McpAuditWriteRequest? captured = null;
        audit.Setup(a => a.StageAsync(It.IsAny<McpAuditWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<McpAuditWriteRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync((McpAuditEntry?)null);
        staged = () => captured;
        return audit;
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonDocument Serialize(object result) => JsonDocument.Parse(JsonSerializer.Serialize(result));

    private static ContactDto NewContact(Guid? id = null, ContactType type = ContactType.Organization, Guid? categoryId = null, string name = "contact")
        => new(id ?? Guid.NewGuid(), name, type, categoryId, "desc", false, null);

    private static ContactCategoryDto NewContactCategory(Guid? id = null, string name = "vip")
        => new(id ?? Guid.NewGuid(), name);

    private static AccountDto NewAccount(Guid? id = null, Guid? bankContactId = null)
        => new(id ?? Guid.NewGuid(), "giro", AccountType.Giro, "DE001", 0m,
            bankContactId ?? Guid.NewGuid(), null, SavingsPlanExpectation.Optional, true);

    private static SavingsPlanDto NewPlan(Guid? id = null, Guid? categoryId = null)
        => new(id ?? Guid.NewGuid(), "plan", SavingsPlanType.Recurring, 1000m, null,
            SavingsPlanInterval.Monthly, true, DateTime.UtcNow, null, categoryId, "CN-1");

    private static SavingsPlanCategoryDto NewPlanCategory(Guid? id = null)
        => new() { Id = id ?? Guid.NewGuid(), Name = "cat" };

    private static SecurityDto NewSecurity(Guid? id = null, Guid? categoryId = null)
        => new() { Id = id ?? Guid.NewGuid(), Name = "sec", Identifier = "DE000A", CurrencyCode = "EUR", IsActive = true, CategoryId = categoryId };

    private static SecurityCategoryDto NewSecurityCategory(Guid? id = null)
        => new() { Id = id ?? Guid.NewGuid(), Name = "secCat" };

    private static BudgetPurposeDto NewPurpose(Guid? id = null, Guid? sourceId = null)
        => new(id ?? Guid.NewGuid(), Guid.NewGuid(), "purpose", null, BudgetSourceType.Contact, sourceId ?? Guid.NewGuid(), null);

    private static BudgetRuleDto NewRule(Guid? id = null, Guid? purposeId = null)
        => new(id ?? Guid.NewGuid(), Guid.NewGuid(), purposeId, null, 100m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null);

    private static BudgetOverrideDto NewOverride(Guid? id = null, Guid? purposeId = null)
        => new(id ?? Guid.NewGuid(), Guid.NewGuid(), purposeId ?? Guid.NewGuid(), 2026, 3, 150m);

    private static BudgetCategoryDto NewBudgetCategory(Guid? id = null, string name = "bcat")
        => new(id ?? Guid.NewGuid(), Guid.NewGuid(), name);

    private static AttachmentDto NewAttachment(Guid? id = null, AttachmentEntityKind kind = AttachmentEntityKind.Contact, Guid? entityId = null, Guid? categoryId = null)
        => new(id ?? Guid.NewGuid(), (short)kind, entityId ?? Guid.NewGuid(), "file.pdf", "application/pdf", 12, categoryId, DateTime.UtcNow, false);

    private static AttachmentCategoryDto NewAttachmentCategory(Guid? id = null)
        => new(id ?? Guid.NewGuid(), "acat", false, false);

    private static ReportFavoriteDto NewFavorite(Guid? id = null)
        => new(id ?? Guid.NewGuid(), "fav", PostingKind.Bank, false, ReportInterval.Month, 24,
            false, false, false, false, false, DateTime.UtcNow, null,
            new List<PostingKind> { PostingKind.Bank }, null, false);

    private static HomeKpiDto NewKpi(Guid? id = null, Guid? favoriteId = null)
        => new(id ?? Guid.NewGuid(), HomeKpiKind.ReportFavorite, favoriteId, "fav", null,
            null, HomeKpiDisplayMode.TotalOnly, 0, DateTime.UtcNow, null);

    private static async Task AssertApprovalRequiredAsync(Task<object> call)
    {
        using var doc = Serialize(await call);
        Assert.True(doc.RootElement.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal("Err_Mcp_ApprovalRequired", doc.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("effect").GetString()));
    }

    private static async Task AssertMcpErrorAsync(Task<object> call, string code)
    {
        var ex = await Assert.ThrowsAsync<McpToolException>(() => call);
        Assert.Equal(code, ex.Code);
    }

    // ------------------------------------------------------------------
    // Catalog contract
    // ------------------------------------------------------------------

    /// <summary>Test case.</summary>
    [Fact]
    public void Registry_AcceptsTheMasterDataCatalog_AndScopesMatchTheCatalog()
    {
        var descriptors = new IMcpToolDescriptor[]
        {
            new FmContactsManageDescriptor(),
            new FmAccountsManageDescriptor(),
            new FmSavingsPlansManageDescriptor(),
            new FmSecuritiesManageDescriptor(),
            new FmBudgetsManageDescriptor(),
            new FmCategoriesManageDescriptor(),
            new FmAttachmentsManageDescriptor(),
            new FmReportsConfigurationManageDescriptor()
        };
        var registry = new McpToolRegistry(descriptors);
        Assert.Equal(8, registry.Tools.Count);
        Assert.All(registry.Tools, t =>
        {
            Assert.Equal(McpToolSecurityClass.Schreibvorbereitend, t.SecurityClass);
            Assert.All(t.RequiredScopes, s =>
                Assert.True(McpScopeMatrix.IsValidCombination(s.DataType, s.Right)));
            Assert.Equal(JsonValueKind.Object, t.InputSchema.RootElement.ValueKind);
        });

        Assert.Equal(new[]
            {
                new McpToolScope(McpDataType.Contacts, McpRightKind.Create),
                new McpToolScope(McpDataType.Contacts, McpRightKind.Modify),
                new McpToolScope(McpDataType.Contacts, McpRightKind.Complete),
                new McpToolScope(McpDataType.ContactCategories, McpRightKind.Read)
            },
            registry.Find("fm_contacts_manage")!.RequiredScopes);
        Assert.Equal(new[]
            {
                new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Create),
                new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Modify),
                new McpToolScope(McpDataType.SavingsPlanCategories, McpRightKind.Read)
            },
            registry.Find("fm_savings_plans_manage")!.RequiredScopes);
        Assert.Equal(new[]
            {
                new McpToolScope(McpDataType.Attachments, McpRightKind.Create),
                new McpToolScope(McpDataType.Attachments, McpRightKind.Modify),
                new McpToolScope(McpDataType.Attachments, McpRightKind.Complete),
                new McpToolScope(McpDataType.AttachmentCategories, McpRightKind.Read)
            },
            registry.Find("fm_attachments_manage")!.RequiredScopes);
        Assert.Equal(new[]
            {
                new McpToolScope(McpDataType.Reports, McpRightKind.Create),
                new McpToolScope(McpDataType.Reports, McpRightKind.Modify),
                new McpToolScope(McpDataType.KpiConfigurations, McpRightKind.Create),
                new McpToolScope(McpDataType.KpiConfigurations, McpRightKind.Modify)
            },
            registry.Find("fm_reports_configuration_manage")!.RequiredScopes);
    }

    // ------------------------------------------------------------------
    // T22 fm_contacts_manage
    // ------------------------------------------------------------------

    private static FmContactsManageHandler ContactsHandler(
        Mock<IContactService> contacts, Mock<IContactCategoryService>? categories = null,
        Mock<IMcpScopeEvaluator>? evaluator = null, Mock<IMcpAuditWriter>? audit = null,
        McpRecordScopeFilter? filter = null)
        => new(contacts.Object,
            (categories ?? new Mock<IContactCategoryService>()).Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            filter ?? NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Create_PassesOwner_AndMarksUntrustedData()
    {
        var owner = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.CreateAsync(owner, "name", ContactType.Organization, categoryId, "desc", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(categoryId: categoryId, name: "name"));
        var categories = new Mock<IContactCategoryService>();
        categories.Setup(c => c.GetAsync(categoryId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContactCategory(categoryId));
        var audit = CapturingAudit(out var staged);

        var handler = ContactsHandler(contacts, categories, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"create\",\"name\":\"name\",\"contactType\":\"Organization\",\"categoryId\":\"{categoryId}\",\"description\":\"desc\",\"isPaymentIntermediary\":true}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        contacts.Verify(c => c.CreateAsync(owner, "name", ContactType.Organization, categoryId, "desc", true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal("name", doc.RootElement.GetProperty("contact").GetProperty("untrusted_data").GetProperty("name").GetString());
        Assert.Equal("ContactCreated", staged()!.EventType);
        Assert.Equal(McpAuditCategory.DataChange, staged()!.Category);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Create_ForeignCategory_RejectedBeforeEffect()
    {
        var owner = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        var categories = new Mock<IContactCategoryService>();
        categories.Setup(c => c.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactCategoryDto?)null);

        var handler = ContactsHandler(contacts, categories);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"create\",\"name\":\"n\",\"contactType\":\"Person\",\"categoryId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        contacts.Verify(c => c.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ContactType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Create_SelfType_RejectedBeforeEffect()
    {
        var owner = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        var handler = ContactsHandler(contacts);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args("{\"operation\":\"create\",\"name\":\"me\",\"contactType\":\"Self\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        contacts.Verify(c => c.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ContactType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Update_MergesProvidedFields_AndStagesDeltas()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(id));
        contacts.Setup(c => c.UpdateAsync(id, owner, "new", ContactType.Organization, null, "desc", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(id, "new", ContactType.Organization, null, "desc", false, null));
        var audit = CapturingAudit(out var staged);

        var handler = ContactsHandler(contacts, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"contactId\":\"{id}\",\"name\":\"new\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal("ContactUpdated", staged()!.EventType);
        Assert.Contains(staged()!.Changes!, c => c.Field == "Name" && c.OldValue == "contact" && c.NewValue == "new");
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Update_ForeignContact_ReturnsNull()
    {
        var owner = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactDto?)null);
        var handler = ContactsHandler(contacts);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"contactId\":\"{Guid.NewGuid()}\",\"name\":\"x\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("contact").ValueKind);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Update_ExplicitNull_ClearsCategoryDescriptionAndFlag()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(id, "contact", ContactType.Organization, Guid.NewGuid(), "desc", true, null));
        contacts.Setup(c => c.UpdateAsync(id, owner, "contact", ContactType.Organization, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(id, "contact", ContactType.Organization, null, null, false, null));
        var audit = CapturingAudit(out var staged);
        var handler = ContactsHandler(contacts, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"contactId\":\"{id}\",\"categoryId\":null,\"description\":null,\"isPaymentIntermediary\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        contacts.Verify(c => c.UpdateAsync(id, owner, "contact", ContactType.Organization, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(staged()!.Changes!, c => c.Field == "CategoryId" && c.NewValue is null);
        Assert.Contains(staged()!.Changes!, c => c.Field == "Description" && c.NewValue is null);
        Assert.Contains(staged()!.Changes!, c => c.Field == "IsPaymentIntermediary" && c.NewValue is null);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Update_FilteredRecordScope_DeniesOutOfScopeCategory()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(id, categoryId: Guid.NewGuid()));
        // Filtered individual profile: the Modify row whitelists a different category.
        var filtered = new McpAccessScope(Guid.NewGuid(), owner, McpDataType.Contacts, McpRightKind.Modify,
            contactCategoryIdsCsv: Guid.NewGuid().ToString());
        var handler = ContactsHandler(contacts, filter: NewFilter(filtered));
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"contactId\":\"{id}\",\"name\":\"x\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_ScopeDenied");
        contacts.Verify(c => c.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ContactType>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Alias_AddAndRemove_CallService()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(id));
        var handler = ContactsHandler(contacts);

        await handler.InvokeAsync(Args($"{{\"operation\":\"addAlias\",\"contactId\":\"{id}\",\"pattern\":\"acme\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None);
        contacts.Verify(c => c.AddAliasAsync(id, owner, "acme", It.IsAny<CancellationToken>()), Times.Once);

        var aliasId = Guid.NewGuid();
        await handler.InvokeAsync(Args($"{{\"operation\":\"removeAlias\",\"contactId\":\"{id}\",\"aliasId\":\"{aliasId}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None);
        contacts.Verify(c => c.DeleteAliasAsync(id, owner, aliasId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Merge_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(sourceId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(sourceId));
        var handler = ContactsHandler(contacts);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"merge\",\"contactId\":\"{sourceId}\",\"targetContactId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        contacts.Verify(c => c.MergeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<MergePreference>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Delete_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(id));
        var handler = ContactsHandler(contacts);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"delete\",\"contactId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        contacts.Verify(c => c.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Contacts_Delete_WithoutDeleteScope_Denied()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContact(id));
        var handler = ContactsHandler(contacts, evaluator: NewScopeEvaluator(allowDelete: false));
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"delete\",\"contactId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_ScopeDenied");
        contacts.Verify(c => c.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // T27 fm_categories_manage
    // ------------------------------------------------------------------

    private static FmCategoriesManageHandler CategoriesHandler(
        Mock<IContactCategoryService>? contacts = null,
        Mock<ISavingsPlanCategoryService>? savings = null,
        Mock<ISecurityCategoryService>? securities = null,
        Mock<IAttachmentCategoryService>? attachments = null,
        Mock<IMcpScopeEvaluator>? evaluator = null,
        Mock<IMcpAuditWriter>? audit = null)
        => new((contacts ?? new Mock<IContactCategoryService>()).Object,
            (savings ?? new Mock<ISavingsPlanCategoryService>()).Object,
            (securities ?? new Mock<ISecurityCategoryService>()).Object,
            (attachments ?? new Mock<IAttachmentCategoryService>()).Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Categories_CreateAndRename_ContactCategory()
    {
        var owner = Guid.NewGuid();
        var contacts = new Mock<IContactCategoryService>();
        contacts.Setup(c => c.CreateAsync(owner, "vip", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContactCategory(name: "vip"));
        var id = Guid.NewGuid();
        contacts.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewContactCategory(id, "old"));
        var audit = CapturingAudit(out var staged);
        var handler = CategoriesHandler(contacts: contacts, audit: audit);
        var call = NewCallContext(NewAccessContext(owner).Object);

        await handler.InvokeAsync(Args("{\"categoryType\":\"contact\",\"operation\":\"create\",\"name\":\"vip\"}"),
            call, CancellationToken.None);
        contacts.Verify(c => c.CreateAsync(owner, "vip", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("CategoryCreated", staged()!.EventType);

        await handler.InvokeAsync(Args($"{{\"categoryType\":\"contact\",\"operation\":\"update\",\"categoryId\":\"{id}\",\"name\":\"new\"}}"),
            call, CancellationToken.None);
        contacts.Verify(c => c.UpdateAsync(id, owner, "new", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("CategoryUpdated", staged()!.EventType);
        Assert.Contains(staged()!.Changes!, c => c.Field == "Name" && c.OldValue == "old" && c.NewValue == "new");
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Categories_Delete_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var savings = new Mock<ISavingsPlanCategoryService>();
        savings.Setup(c => c.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPlanCategory(id));
        var handler = CategoriesHandler(savings: savings);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"categoryType\":\"savingsPlan\",\"operation\":\"delete\",\"categoryId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        savings.Verify(c => c.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // T23 fm_accounts_manage
    // ------------------------------------------------------------------

    private static FmAccountsManageHandler AccountsHandler(
        Mock<IAccountService> accounts, Mock<IMcpScopeEvaluator>? evaluator = null,
        Mock<IMcpAuditWriter>? audit = null, McpRecordScopeFilter? filter = null)
        => new(accounts.Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            filter ?? NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Accounts_CreateAndLinkedIbans_CallService()
    {
        var owner = Guid.NewGuid();
        var bankContactId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.CreateAsync(owner, "giro", AccountType.Giro, "DE001", bankContactId,
                SavingsPlanExpectation.Required, false, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(accountId, bankContactId));
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(accountId, bankContactId));
        accounts.Setup(a => a.RemoveLinkedIbanAsync(accountId, owner, "DE999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = AccountsHandler(accounts);
        var call = NewCallContext(NewAccessContext(owner).Object);

        await handler.InvokeAsync(Args(
            $"{{\"operation\":\"create\",\"name\":\"giro\",\"accountType\":\"Giro\",\"iban\":\"DE001\",\"bankContactId\":\"{bankContactId}\",\"savingsPlanExpectation\":\"Required\",\"securityProcessingEnabled\":false,\"isCollectionAccount\":true}}"),
            call, CancellationToken.None);
        accounts.Verify(a => a.CreateAsync(owner, "giro", AccountType.Giro, "DE001", bankContactId,
            SavingsPlanExpectation.Required, false, true, It.IsAny<CancellationToken>()), Times.Once);

        await handler.InvokeAsync(Args($"{{\"operation\":\"addLinkedIban\",\"accountId\":\"{accountId}\",\"iban\":\"DE999\"}}"),
            call, CancellationToken.None);
        accounts.Verify(a => a.AddLinkedIbanAsync(accountId, owner, "DE999", It.IsAny<CancellationToken>()), Times.Once);

        await handler.InvokeAsync(Args($"{{\"operation\":\"removeLinkedIban\",\"accountId\":\"{accountId}\",\"iban\":\"DE999\"}}"),
            call, CancellationToken.None);
        accounts.Verify(a => a.RemoveLinkedIbanAsync(accountId, owner, "DE999", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Accounts_Create_CreditCard_CallsService()
    {
        var owner = Guid.NewGuid();
        var bankContactId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.CreateAsync(owner, "karte", AccountType.CreditCard, null, bankContactId,
                SavingsPlanExpectation.Optional, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDto(accountId, "karte", AccountType.CreditCard, null, 0m,
                bankContactId, null, SavingsPlanExpectation.Optional, true));
        var handler = AccountsHandler(accounts);

        using var doc = Serialize(await handler.InvokeAsync(Args(
            $"{{\"operation\":\"create\",\"name\":\"karte\",\"accountType\":\"CreditCard\",\"bankContactId\":\"{bankContactId}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        accounts.Verify(a => a.CreateAsync(owner, "karte", AccountType.CreditCard, null, bankContactId,
            SavingsPlanExpectation.Optional, true, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("CreditCard", doc.RootElement.GetProperty("account").GetProperty("accountType").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Accounts_Delete_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(id));
        var handler = AccountsHandler(accounts);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"delete\",\"accountId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        accounts.Verify(a => a.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Accounts_Update_MergesAndStagesDeltas()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(id));
        accounts.Setup(a => a.UpdateAsync(id, owner, "new", "DE001", It.IsAny<Guid>(),
                SavingsPlanExpectation.Optional, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(id));
        var audit = CapturingAudit(out var staged);
        var handler = AccountsHandler(accounts, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"accountId\":\"{id}\",\"name\":\"new\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal("AccountUpdated", staged()!.EventType);
        Assert.Contains(staged()!.Changes!, c => c.Field == "Name" && c.NewValue == "new");
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Accounts_Update_ExplicitNull_ClearsIban()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var bankContactId = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(id, bankContactId));
        accounts.Setup(a => a.UpdateAsync(id, owner, "giro", null, bankContactId,
                SavingsPlanExpectation.Optional, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(id, bankContactId));
        var audit = CapturingAudit(out var staged);
        var handler = AccountsHandler(accounts, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"accountId\":\"{id}\",\"iban\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        accounts.Verify(a => a.UpdateAsync(id, owner, "giro", null, bankContactId,
            SavingsPlanExpectation.Optional, true, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(staged()!.Changes!, c => c.Field == "Iban" && c.NewValue is null);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Accounts_Results_WrapIbanFields_InUntrustedData()
    {
        var owner = Guid.NewGuid();
        var bankContactId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.CreateAsync(owner, "giro", AccountType.Giro, "DE001", bankContactId,
                SavingsPlanExpectation.Optional, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(accountId, bankContactId));
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAccount(accountId, bankContactId));
        var handler = AccountsHandler(accounts);
        var call = NewCallContext(NewAccessContext(owner).Object);

        using var created = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"create\",\"name\":\"giro\",\"accountType\":\"Giro\",\"iban\":\"DE001\",\"bankContactId\":\"{bankContactId}\"}}"),
            call, CancellationToken.None));
        var account = created.RootElement.GetProperty("account");
        Assert.Equal("DE001", account.GetProperty("untrusted_data").GetProperty("iban").GetString());
        Assert.Equal(JsonValueKind.Array, account.GetProperty("untrusted_data").GetProperty("linkedIbans").ValueKind);
        Assert.False(account.TryGetProperty("iban", out _));
        Assert.False(account.TryGetProperty("linkedIbans", out _));

        using var linked = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"addLinkedIban\",\"accountId\":\"{accountId}\",\"iban\":\"DE999\"}}"),
            call, CancellationToken.None));
        Assert.Equal("DE999", linked.RootElement.GetProperty("untrusted_data").GetProperty("iban").GetString());
        Assert.False(linked.RootElement.TryGetProperty("iban", out _));
    }

    // ------------------------------------------------------------------
    // T24 fm_savings_plans_manage
    // ------------------------------------------------------------------

    private static FmSavingsPlansManageHandler PlansHandler(
        Mock<ISavingsPlanService> plans, Mock<ISavingsPlanCategoryService>? categories = null,
        Mock<IMcpScopeEvaluator>? evaluator = null, Mock<IMcpAuditWriter>? audit = null)
        => new(plans.Object,
            (categories ?? new Mock<ISavingsPlanCategoryService>()).Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task SavingsPlans_Create_PassesFields_AndStagesAudit()
    {
        var owner = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var plans = new Mock<ISavingsPlanService>();
        plans.Setup(p => p.CreateAsync(owner, "plan", SavingsPlanType.Recurring, 100m, null,
                SavingsPlanInterval.Quarterly, categoryId, "CN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPlan(categoryId: categoryId));
        var categories = new Mock<ISavingsPlanCategoryService>();
        categories.Setup(c => c.GetAsync(categoryId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPlanCategory(categoryId));
        var audit = CapturingAudit(out var staged);
        var handler = PlansHandler(plans, categories, audit: audit);

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"create\",\"name\":\"plan\",\"savingsPlanType\":\"Recurring\",\"targetAmount\":100,\"interval\":\"Quarterly\",\"categoryId\":\"{categoryId}\",\"contractNumber\":\"CN\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal("SavingsPlanCreated", staged()!.EventType);
        plans.Verify(p => p.CreateAsync(owner, "plan", SavingsPlanType.Recurring, 100m, null,
            SavingsPlanInterval.Quarterly, categoryId, "CN", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The non-negative <c>targetAmount</c> bound is a documented MCP-side convention
    /// (inputSchema of <c>fm_savings_plans_manage</c>) — the domain itself is permissive.
    /// The tool-level rejection is intentional and verified here.
    /// </summary>
    [Fact]
    public async Task SavingsPlans_Create_NegativeTargetAmount_RejectedByMcpConvention()
    {
        var owner = Guid.NewGuid();
        var plans = new Mock<ISavingsPlanService>();
        var handler = PlansHandler(plans);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args("{\"operation\":\"create\",\"name\":\"plan\",\"savingsPlanType\":\"Recurring\",\"targetAmount\":-100}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        plans.Verify(p => p.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<SavingsPlanType>(),
            It.IsAny<decimal?>(), It.IsAny<DateTime?>(), It.IsAny<SavingsPlanInterval?>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The documented MCP convention only excludes <i>negative</i> target amounts — zero
    /// stays within it and must reach the service.
    /// </summary>
    [Fact]
    public async Task SavingsPlans_Create_ZeroTargetAmount_IsApplied()
    {
        var owner = Guid.NewGuid();
        var plans = new Mock<ISavingsPlanService>();
        plans.Setup(p => p.CreateAsync(owner, "plan", SavingsPlanType.Recurring, 0m, null,
                null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPlan());
        var handler = PlansHandler(plans);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"operation\":\"create\",\"name\":\"plan\",\"savingsPlanType\":\"Recurring\",\"targetAmount\":0}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        plans.Verify(p => p.CreateAsync(owner, "plan", SavingsPlanType.Recurring, 0m, null,
            null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task SavingsPlans_Update_ArchivedPlan_Rejected()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var plans = new Mock<ISavingsPlanService>();
        var archived = NewPlan(id);
        archived.ArchivedUtc = DateTime.UtcNow;
        archived.IsActive = false;
        plans.Setup(p => p.GetAsync(id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(archived);
        var handler = PlansHandler(plans);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"savingsPlanId\":\"{id}\",\"name\":\"x\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidState");
        plans.Verify(p => p.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<SavingsPlanType>(),
            It.IsAny<decimal?>(), It.IsAny<DateTime?>(), It.IsAny<SavingsPlanInterval?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task SavingsPlans_Archive_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var plans = new Mock<ISavingsPlanService>();
        plans.Setup(p => p.GetAsync(id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewPlan(id));
        var handler = PlansHandler(plans);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"archive\",\"savingsPlanId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        plans.Verify(p => p.ArchiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        plans.Verify(p => p.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task SavingsPlans_Update_ExplicitNull_ClearsNullableFields()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var plans = new Mock<ISavingsPlanService>();
        plans.Setup(p => p.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanDto(id, "plan", SavingsPlanType.Recurring, 1000m,
                DateTime.UtcNow, SavingsPlanInterval.Monthly, true, DateTime.UtcNow, null, categoryId, "CN-1"));
        plans.Setup(p => p.UpdateAsync(id, owner, "plan", SavingsPlanType.Recurring, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPlan(id));
        var audit = CapturingAudit(out var staged);
        var handler = PlansHandler(plans, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"savingsPlanId\":\"{id}\",\"targetAmount\":null,\"targetDate\":null,\"interval\":null,\"categoryId\":null,\"contractNumber\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        plans.Verify(p => p.UpdateAsync(id, owner, "plan", SavingsPlanType.Recurring, null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(staged()!.Changes!, c => c.Field == "ContractNumber" && c.NewValue is null);
        Assert.Contains(staged()!.Changes!, c => c.Field == "CategoryId" && c.NewValue is null);
    }

    // ------------------------------------------------------------------
    // T25 fm_securities_manage
    // ------------------------------------------------------------------

    private static FmSecuritiesManageHandler SecuritiesHandler(
        Mock<ISecurityService> securities, Mock<ISecurityCategoryService>? categories = null,
        Mock<IMcpScopeEvaluator>? evaluator = null, Mock<IMcpAuditWriter>? audit = null)
        => new(securities.Object,
            (categories ?? new Mock<ISecurityCategoryService>()).Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Create_PassesFields()
    {
        var owner = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.CreateAsync(owner, "sec", "DE000A", "desc", "AV", "USD", null,
                It.IsAny<CancellationToken>(), "EU", "tech"))
            .ReturnsAsync(NewSecurity());
        var handler = SecuritiesHandler(securities);
        await handler.InvokeAsync(
            Args("{\"operation\":\"create\",\"name\":\"sec\",\"identifier\":\"DE000A\",\"description\":\"desc\",\"alphaVantageCode\":\"AV\",\"currencyCode\":\"USD\",\"region\":\"EU\",\"sector\":\"tech\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None);
        securities.Verify(s => s.CreateAsync(owner, "sec", "DE000A", "desc", "AV", "USD", null,
            It.IsAny<CancellationToken>(), "EU", "tech"), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Create_InvalidCurrency_RejectedBeforeEffect()
    {
        var owner = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        var handler = SecuritiesHandler(securities);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args("{\"operation\":\"create\",\"name\":\"sec\",\"identifier\":\"X\",\"currencyCode\":\"TOOLONG\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        securities.Verify(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Archive_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewSecurity(id));
        var handler = SecuritiesHandler(securities);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"archive\",\"securityId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        securities.Verify(s => s.ArchiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        securities.Verify(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Unarchive_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityDto
            {
                Id = id,
                Name = "sec",
                Identifier = "DE000A",
                CurrencyCode = "EUR",
                IsActive = false,
                ArchivedUtc = DateTime.UtcNow
            });
        var handler = SecuritiesHandler(securities);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"unarchive\",\"securityId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        securities.Verify(s => s.UnarchiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Unarchive_ApprovedExecution_ReactivatesAndStagesAudit()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var archived = new SecurityDto
        {
            Id = id,
            Name = "sec",
            Identifier = "DE000A",
            CurrencyCode = "EUR",
            IsActive = false,
            ArchivedUtc = DateTime.UtcNow
        };
        var securities = new Mock<ISecurityService>();
        securities.SetupSequence(s => s.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(archived)
            .ReturnsAsync(new SecurityDto
            {
                Id = id,
                Name = "sec",
                Identifier = "DE000A",
                CurrencyCode = "EUR",
                IsActive = true,
                ArchivedUtc = null
            });
        securities.Setup(s => s.UnarchiveAsync(id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var audit = CapturingAudit(out var staged);
        var handler = SecuritiesHandler(securities, audit: audit);

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"unarchive\",\"securityId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object, McpToolExecutionMode.ApprovedExecution), CancellationToken.None));

        securities.Verify(s => s.UnarchiveAsync(id, owner, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        var security = doc.RootElement.GetProperty("security");
        Assert.True(security.GetProperty("isActive").GetBoolean());
        Assert.Equal(JsonValueKind.Null, security.GetProperty("archivedUtc").ValueKind);
        Assert.Equal("SecurityUnarchived", staged()!.EventType);
        Assert.Equal(McpAuditCategory.DataChange, staged()!.Category);
        Assert.Contains(staged()!.Changes!, c => c.Field == "IsActive" && c.OldValue == "false" && c.NewValue == "true");
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Unarchive_OnActive_ReturnsUnchanged_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewSecurity(id));
        var handler = SecuritiesHandler(securities);

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"unarchive\",\"securityId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.False(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal("unchanged", doc.RootElement.GetProperty("reason").GetString());
        Assert.True(doc.RootElement.GetProperty("security").GetProperty("isActive").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("security").GetProperty("archivedUtc").ValueKind);
        securities.Verify(s => s.UnarchiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Unarchive_ForeignSecurity_ReturnsNull()
    {
        var owner = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecurityDto?)null);
        var handler = SecuritiesHandler(securities);

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"unarchive\",\"securityId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("security").ValueKind);
        securities.Verify(s => s.UnarchiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Unarchive_ApprovedExecution_TargetGone()
    {
        var owner = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecurityDto?)null);
        var handler = SecuritiesHandler(securities);

        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"unarchive\",\"securityId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(owner).Object, McpToolExecutionMode.ApprovedExecution), CancellationToken.None),
            "Err_TargetGone");
        securities.Verify(s => s.UnarchiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Update_ExplicitNull_ClearsDescriptionAndCategory()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityDto
            {
                Id = id,
                Name = "sec",
                Identifier = "DE000A",
                CurrencyCode = "EUR",
                IsActive = true,
                CategoryId = Guid.NewGuid(),
                Description = "desc",
                Region = "EU",
                Sector = "tech"
            });
        securities.Setup(s => s.UpdateAsync(id, owner, "sec", "DE000A", null, null, "EUR", null,
                It.IsAny<CancellationToken>(), null, null))
            .ReturnsAsync(NewSecurity(id));
        var handler = SecuritiesHandler(securities);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"securityId\":\"{id}\",\"description\":null,\"alphaVantageCode\":null,\"categoryId\":null,\"region\":null,\"sector\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        securities.Verify(s => s.UpdateAsync(id, owner, "sec", "DE000A", null, null, "EUR", null,
            It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Securities_Result_WrapsIdentifier_InUntrustedData()
    {
        var owner = Guid.NewGuid();
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.CreateAsync(owner, "sec", "DE000A", null, null, "EUR", null,
                It.IsAny<CancellationToken>(), null, null))
            .ReturnsAsync(NewSecurity());
        var handler = SecuritiesHandler(securities);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"operation\":\"create\",\"name\":\"sec\",\"identifier\":\"DE000A\",\"currencyCode\":\"EUR\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var security = doc.RootElement.GetProperty("security");
        Assert.Equal("DE000A", security.GetProperty("untrusted_data").GetProperty("identifier").GetString());
        Assert.False(security.TryGetProperty("identifier", out _));
    }

    // ------------------------------------------------------------------
    // T26 fm_budgets_manage
    // ------------------------------------------------------------------

    private static FmBudgetsManageHandler BudgetsHandler(
        Mock<IBudgetPurposeService>? purposes = null, Mock<IBudgetRuleService>? rules = null,
        Mock<IBudgetOverrideService>? overrides = null, Mock<IBudgetCategoryService>? categories = null,
        Mock<IContactService>? contacts = null, Mock<IContactCategoryService>? contactCategories = null,
        Mock<ISavingsPlanService>? plans = null,
        Mock<IMcpScopeEvaluator>? evaluator = null, Mock<IMcpAuditWriter>? audit = null)
        => new((purposes ?? new Mock<IBudgetPurposeService>()).Object,
            (rules ?? new Mock<IBudgetRuleService>()).Object,
            (overrides ?? new Mock<IBudgetOverrideService>()).Object,
            (categories ?? new Mock<IBudgetCategoryService>()).Object,
            (contacts ?? new Mock<IContactService>()).Object,
            (contactCategories ?? new Mock<IContactCategoryService>()).Object,
            (plans ?? new Mock<ISavingsPlanService>()).Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Budgets_PurposeCreate_ResolvesSource_AndCallsService()
    {
        var owner = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.CreateAsync(owner, "purpose", BudgetSourceType.Contact, contactId, null, null,
                It.IsAny<CancellationToken>(), BudgetValuationType.TotalBudget))
            .ReturnsAsync(NewPurpose(sourceId: contactId));
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(contactId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewContact(contactId));
        var handler = BudgetsHandler(purposes: purposes, contacts: contacts);

        await handler.InvokeAsync(
            Args($"{{\"entity\":\"purpose\",\"operation\":\"create\",\"name\":\"purpose\",\"sourceType\":\"Contact\",\"sourceId\":\"{contactId}\",\"valuationType\":\"TotalBudget\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None);
        purposes.Verify(p => p.CreateAsync(owner, "purpose", BudgetSourceType.Contact, contactId, null, null,
            It.IsAny<CancellationToken>(), BudgetValuationType.TotalBudget), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Budgets_PurposeCreate_ForeignSource_RejectedBeforeEffect()
    {
        var owner = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactDto?)null);
        var handler = BudgetsHandler(purposes: purposes, contacts: contacts);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"entity\":\"purpose\",\"operation\":\"create\",\"name\":\"p\",\"sourceType\":\"Contact\",\"sourceId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        purposes.Verify(p => p.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<BudgetSourceType>(), It.IsAny<Guid>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<BudgetValuationType>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Budgets_RuleCreate_AndOverrideCreate_CallServices()
    {
        var owner = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.GetAsync(purposeId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewPurpose(purposeId));
        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.CreateAsync(owner, purposeId, 200m, BudgetIntervalType.Monthly, null,
                new DateOnly(2026, 1, 1), null, "pattern", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(purposeId: purposeId));
        var overrides = new Mock<IBudgetOverrideService>();
        overrides.Setup(o => o.CreateAsync(owner, purposeId, new BudgetPeriodKey(2026, 3), 150m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewOverride(purposeId: purposeId));
        var handler = BudgetsHandler(purposes: purposes, rules: rules, overrides: overrides);
        var call = NewCallContext(NewAccessContext(owner).Object);

        await handler.InvokeAsync(Args(
            $"{{\"entity\":\"rule\",\"operation\":\"create\",\"purposeId\":\"{purposeId}\",\"amount\":200,\"interval\":\"Monthly\",\"startDate\":\"2026-01-01\",\"purposePattern\":\"pattern\",\"useRegex\":true}}"),
            call, CancellationToken.None);
        rules.Verify(r => r.CreateAsync(owner, purposeId, 200m, BudgetIntervalType.Monthly, null,
            new DateOnly(2026, 1, 1), null, "pattern", true, It.IsAny<CancellationToken>()), Times.Once);

        await handler.InvokeAsync(Args(
            $"{{\"entity\":\"override\",\"operation\":\"create\",\"purposeId\":\"{purposeId}\",\"periodYear\":2026,\"periodMonth\":3,\"amount\":150}}"),
            call, CancellationToken.None);
        overrides.Verify(o => o.CreateAsync(owner, purposeId, new BudgetPeriodKey(2026, 3), 150m, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Budgets_RuleCreate_InvalidInterval_RejectedBeforeEffect()
    {
        var owner = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.GetAsync(purposeId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewPurpose(purposeId));
        var rules = new Mock<IBudgetRuleService>();
        var handler = BudgetsHandler(purposes: purposes, rules: rules);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"entity\":\"rule\",\"operation\":\"create\",\"purposeId\":\"{purposeId}\",\"amount\":100,\"interval\":\"CustomMonths\",\"startDate\":\"2026-01-01\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        rules.Verify(r => r.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<BudgetIntervalType>(),
            It.IsAny<int?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Budgets_CategoryCreate_AndDeletes_RequireApprovalWhereFinal()
    {
        var owner = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var categories = new Mock<IBudgetCategoryService>();
        categories.Setup(c => c.CreateAsync(owner, "bcat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewBudgetCategory(catId));
        categories.Setup(c => c.GetAsync(catId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewBudgetCategory(catId));
        var handler = BudgetsHandler(categories: categories);
        var call = NewCallContext(NewAccessContext(owner).Object);

        await handler.InvokeAsync(Args("{\"entity\":\"category\",\"operation\":\"create\",\"name\":\"bcat\"}"),
            call, CancellationToken.None);
        categories.Verify(c => c.CreateAsync(owner, "bcat", It.IsAny<CancellationToken>()), Times.Once);

        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"entity\":\"category\",\"operation\":\"delete\",\"categoryId\":\"{catId}\"}}"),
            call, CancellationToken.None));
        categories.Verify(c => c.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Budgets_PurposeDelete_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.GetAsync(id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewPurpose(id));
        var handler = BudgetsHandler(purposes: purposes);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"entity\":\"purpose\",\"operation\":\"delete\",\"purposeId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        purposes.Verify(p => p.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Budgets_RuleUpdate_ExplicitNull_ClearsEndDateAndPattern()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.GetAsync(id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BudgetRuleDto(id, owner, Guid.NewGuid(), null, 100m,
                BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "pat", false));
        rules.Setup(r => r.UpdateAsync(id, owner, 100m, BudgetIntervalType.Monthly, null,
                new DateOnly(2026, 1, 1), null, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(id));
        var handler = BudgetsHandler(rules: rules);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"entity\":\"rule\",\"operation\":\"update\",\"ruleId\":\"{id}\",\"endDate\":null,\"purposePattern\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        rules.Verify(r => r.UpdateAsync(id, owner, 100m, BudgetIntervalType.Monthly, null,
            new DateOnly(2026, 1, 1), null, null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: rule amounts are signed (negative = expected costs, positive = expected
    /// income — <see cref="FinanceManager.Domain.Budget.BudgetRule.Amount"/> contract). A negative
    /// amount must reach the service on both create and update.
    /// </summary>
    [Fact]
    public async Task Budgets_RuleCreateAndUpdate_NegativeAmount_IsApplied()
    {
        var owner = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.GetAsync(purposeId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewPurpose(purposeId));
        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.CreateAsync(owner, purposeId, -80m, BudgetIntervalType.Monthly, null,
                new DateOnly(2026, 1, 1), null, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(ruleId, purposeId));
        rules.Setup(r => r.GetAsync(ruleId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BudgetRuleDto(ruleId, owner, purposeId, null, -50m,
                BudgetIntervalType.Monthly, null, new DateOnly(2020, 1, 1), null, null, false));
        rules.Setup(r => r.UpdateAsync(ruleId, owner, -80m, BudgetIntervalType.Monthly, null,
                new DateOnly(2020, 1, 1), null, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(ruleId, purposeId));
        var handler = BudgetsHandler(purposes: purposes, rules: rules);
        var call = NewCallContext(NewAccessContext(owner).Object);

        using var doc1 = Serialize(await handler.InvokeAsync(Args(
            $"{{\"entity\":\"rule\",\"operation\":\"create\",\"purposeId\":\"{purposeId}\",\"amount\":-80,\"interval\":\"Monthly\",\"startDate\":\"2026-01-01\"}}"),
            call, CancellationToken.None));
        Assert.True(doc1.RootElement.GetProperty("applied").GetBoolean());
        rules.Verify(r => r.CreateAsync(owner, purposeId, -80m, BudgetIntervalType.Monthly, null,
            new DateOnly(2026, 1, 1), null, null, false, It.IsAny<CancellationToken>()), Times.Once);

        using var doc2 = Serialize(await handler.InvokeAsync(Args(
            $"{{\"entity\":\"rule\",\"operation\":\"update\",\"purposeId\":\"{purposeId}\",\"ruleId\":\"{ruleId}\",\"amount\":-80}}"),
            call, CancellationToken.None));
        Assert.True(doc2.RootElement.GetProperty("applied").GetBoolean());
        rules.Verify(r => r.UpdateAsync(ruleId, owner, -80m, BudgetIntervalType.Monthly, null,
            new DateOnly(2020, 1, 1), null, null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: zero is a legal domain value for a rule amount —
    /// <c>BudgetRule.SetAmount</c> carries no sign/zero check, so the tool must not block
    /// it either (the domain owns the value range, the tool only validates structure).
    /// </summary>
    [Fact]
    public async Task Budgets_RuleCreate_ZeroAmount_IsApplied()
    {
        var owner = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.GetAsync(purposeId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewPurpose(purposeId));
        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.CreateAsync(owner, purposeId, 0m, BudgetIntervalType.Monthly, null,
                new DateOnly(2026, 1, 1), null, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(purposeId: purposeId));
        var handler = BudgetsHandler(purposes: purposes, rules: rules);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"entity\":\"rule\",\"operation\":\"create\",\"purposeId\":\"{purposeId}\",\"amount\":0,\"interval\":\"Monthly\",\"startDate\":\"2026-01-01\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        rules.Verify(r => r.CreateAsync(owner, purposeId, 0m, BudgetIntervalType.Monthly, null,
            new DateOnly(2026, 1, 1), null, null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: override amounts are signed domain values like rule amounts — a
    /// negative monthly override must reach the service unchanged.
    /// </summary>
    [Fact]
    public async Task Budgets_OverrideCreate_NegativeAmount_IsApplied()
    {
        var owner = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.GetAsync(purposeId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewPurpose(purposeId));
        var overrides = new Mock<IBudgetOverrideService>();
        overrides.Setup(o => o.CreateAsync(owner, purposeId, new BudgetPeriodKey(2026, 3), -150m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewOverride(purposeId: purposeId));
        var handler = BudgetsHandler(purposes: purposes, overrides: overrides);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"entity\":\"override\",\"operation\":\"create\",\"purposeId\":\"{purposeId}\",\"periodYear\":2026,\"periodMonth\":3,\"amount\":-150}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        overrides.Verify(o => o.CreateAsync(owner, purposeId, new BudgetPeriodKey(2026, 3), -150m, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // T28 fm_attachments_manage
    // ------------------------------------------------------------------

    private static FmAttachmentsManageHandler AttachmentsHandler(
        Mock<IAttachmentService> attachments,
        Mock<IAttachmentCategoryService>? attachmentCategories = null,
        Mock<IContactService>? contacts = null,
        Mock<IContactCategoryService>? contactCategories = null,
        Mock<ISavingsPlanService>? plans = null,
        Mock<ISavingsPlanCategoryService>? planCategories = null,
        Mock<ISecurityService>? securities = null,
        Mock<ISecurityCategoryService>? securityCategories = null,
        Mock<IAccountService>? accounts = null,
        Mock<IStatementDraftService>? drafts = null,
        Mock<IMcpScopeEvaluator>? evaluator = null,
        Mock<IMcpAuditWriter>? audit = null)
        => new(attachments.Object,
            (attachmentCategories ?? new Mock<IAttachmentCategoryService>()).Object,
            (contacts ?? new Mock<IContactService>()).Object,
            (contactCategories ?? new Mock<IContactCategoryService>()).Object,
            (plans ?? new Mock<ISavingsPlanService>()).Object,
            (planCategories ?? new Mock<ISavingsPlanCategoryService>()).Object,
            (securities ?? new Mock<ISecurityService>()).Object,
            (securityCategories ?? new Mock<ISecurityCategoryService>()).Object,
            (accounts ?? new Mock<IAccountService>()).Object,
            (drafts ?? new Mock<IStatementDraftService>()).Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Attachments_Upload_DecodesBase64_StagesSha256_NeverContent()
    {
        var owner = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.UploadAsync(owner, AttachmentEntityKind.Contact, entityId,
                It.IsAny<Stream>(), "file.pdf", "application/pdf", null, AttachmentRole.Regular, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAttachment(kind: AttachmentEntityKind.Contact, entityId: entityId));
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(entityId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewContact(entityId));
        var audit = CapturingAudit(out var staged);
        var content = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var handler = AttachmentsHandler(attachments, contacts: contacts, audit: audit);

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"upload\",\"entityKind\":\"Contact\",\"entityId\":\"{entityId}\",\"fileName\":\"file.pdf\",\"contentType\":\"application/pdf\",\"contentBase64\":\"{content}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        attachments.Verify(a => a.UploadAsync(owner, AttachmentEntityKind.Contact, entityId,
            It.IsAny<Stream>(), "file.pdf", "application/pdf", null, AttachmentRole.Regular, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("AttachmentUploaded", staged()!.EventType);
        Assert.Contains("sha256", staged()!.PayloadJson);
        Assert.DoesNotContain(content, staged()!.PayloadJson);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Attachments_Upload_ForeignTarget_RejectedBeforeEffect()
    {
        var owner = Guid.NewGuid();
        var attachments = new Mock<IAttachmentService>();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactDto?)null);
        var handler = AttachmentsHandler(attachments, contacts: contacts);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"upload\",\"entityKind\":\"Contact\",\"entityId\":\"{Guid.NewGuid()}\",\"fileName\":\"f\",\"contentType\":\"application/pdf\",\"contentBase64\":\"{Convert.ToBase64String(new byte[] { 1 })}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        attachments.Verify(a => a.UploadAsync(It.IsAny<Guid>(), It.IsAny<AttachmentEntityKind>(), It.IsAny<Guid>(),
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<AttachmentRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Attachments_Reassign_ValidatesBothEntities()
    {
        var owner = Guid.NewGuid();
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.ListAsync(owner, AttachmentEntityKind.Contact, fromId, 0, 200, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttachmentDto> { NewAttachment(entityId: fromId) });
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(fromId, owner, It.IsAny<CancellationToken>())).ReturnsAsync(NewContact(fromId));
        contacts.Setup(c => c.GetAsync(toId, owner, It.IsAny<CancellationToken>())).ReturnsAsync((ContactDto?)null);
        var handler = AttachmentsHandler(attachments, contacts: contacts);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"reassign\",\"fromEntityKind\":\"Contact\",\"fromEntityId\":\"{fromId}\",\"toEntityKind\":\"Contact\",\"toEntityId\":\"{toId}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        attachments.Verify(a => a.ReassignAsync(It.IsAny<AttachmentEntityKind>(), It.IsAny<Guid>(),
            It.IsAny<AttachmentEntityKind>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Attachments_UpdateAndSetCategory_CallService()
    {
        var owner = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.GetAsync(owner, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAttachment(attachmentId));
        attachments.Setup(a => a.UpdateCoreAsync(owner, attachmentId, "renamed.pdf", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        attachments.Setup(a => a.UpdateCategoryAsync(owner, attachmentId, categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var categories = new Mock<IAttachmentCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttachmentCategoryDto> { NewAttachmentCategory(categoryId) });
        var handler = AttachmentsHandler(attachments, attachmentCategories: categories);
        var call = NewCallContext(NewAccessContext(owner).Object);

        await handler.InvokeAsync(Args($"{{\"operation\":\"update\",\"attachmentId\":\"{attachmentId}\",\"fileName\":\"renamed.pdf\"}}"),
            call, CancellationToken.None);
        attachments.Verify(a => a.UpdateCoreAsync(owner, attachmentId, "renamed.pdf", null, It.IsAny<CancellationToken>()), Times.Once);

        await handler.InvokeAsync(Args($"{{\"operation\":\"setCategory\",\"attachmentId\":\"{attachmentId}\",\"categoryId\":\"{categoryId}\"}}"),
            call, CancellationToken.None);
        attachments.Verify(a => a.UpdateCategoryAsync(owner, attachmentId, categoryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Attachments_SetCategory_ExplicitNull_ClearsCategory()
    {
        var owner = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.GetAsync(owner, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAttachment(attachmentId, categoryId: Guid.NewGuid()));
        attachments.Setup(a => a.UpdateCategoryAsync(owner, attachmentId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = AttachmentsHandler(attachments);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"setCategory\",\"attachmentId\":\"{attachmentId}\",\"categoryId\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("categoryId").ValueKind);
        attachments.Verify(a => a.UpdateCategoryAsync(owner, attachmentId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Attachments_Update_ExplicitNull_ClearsCategory()
    {
        var owner = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.GetAsync(owner, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAttachment(attachmentId, categoryId: Guid.NewGuid()));
        attachments.Setup(a => a.UpdateCoreAsync(owner, attachmentId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = AttachmentsHandler(attachments);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"operation\":\"update\",\"attachmentId\":\"{attachmentId}\",\"categoryId\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        attachments.Verify(a => a.UpdateCoreAsync(owner, attachmentId, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Attachments_Delete_RequiresApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var id = Guid.NewGuid();
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(a => a.GetAsync(owner, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAttachment(id));
        var handler = AttachmentsHandler(attachments);
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"operation\":\"delete\",\"attachmentId\":\"{id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        attachments.Verify(a => a.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // T29 fm_reports_configuration_manage
    // ------------------------------------------------------------------

    private static FmReportsConfigurationManageHandler ReportsConfigHandler(
        Mock<IReportFavoriteService>? favorites = null, Mock<IHomeKpiService>? kpis = null,
        Mock<IMcpScopeEvaluator>? evaluator = null, Mock<IMcpAuditWriter>? audit = null)
        => new((favorites ?? new Mock<IReportFavoriteService>()).Object,
            (kpis ?? new Mock<IHomeKpiService>()).Object,
            (evaluator ?? NewScopeEvaluator()).Object,
            (audit ?? NullAudit()).Object,
            NewFilter());

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReportsConfig_FavoriteCreate_PassesRequest()
    {
        var owner = Guid.NewGuid();
        var favorites = new Mock<IReportFavoriteService>();
        favorites.Setup(f => f.CreateAsync(owner, It.IsAny<ReportFavoriteCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewFavorite());
        var audit = CapturingAudit(out var staged);
        var handler = ReportsConfigHandler(favorites: favorites, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"entity\":\"reportFavorite\",\"operation\":\"create\",\"name\":\"fav\",\"postingKind\":\"Bank\",\"interval\":\"Month\",\"includeCategory\":true,\"take\":12,\"postingKinds\":[\"Bank\",\"Security\"],\"filters\":{\"securitySubTypes\":[0,1],\"includeDividendRelated\":true}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        favorites.Verify(f => f.CreateAsync(owner, It.Is<ReportFavoriteCreateRequest>(r =>
            r.Name == "fav" && r.PostingKind == PostingKind.Bank && r.Interval == ReportInterval.Month
            && r.IncludeCategory && r.Take == 12
            && r.PostingKinds!.Count == 2
            && r.Filters!.SecuritySubTypes!.Count == 2
            && r.Filters!.IncludeDividendRelated == true),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("ReportFavoriteCreated", staged()!.EventType);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReportsConfig_KpiCreate_ValidatesFavoriteReference()
    {
        var owner = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var favorites = new Mock<IReportFavoriteService>();
        favorites.Setup(f => f.GetAsync(favoriteId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewFavorite(favoriteId));
        var kpis = new Mock<IHomeKpiService>();
        kpis.Setup(k => k.CreateAsync(owner, It.IsAny<HomeKpiCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewKpi(favoriteId: favoriteId));
        var handler = ReportsConfigHandler(favorites: favorites, kpis: kpis);
        await handler.InvokeAsync(
            Args($"{{\"entity\":\"homeKpi\",\"operation\":\"create\",\"kind\":\"ReportFavorite\",\"reportFavoriteId\":\"{favoriteId}\",\"displayMode\":\"TotalOnly\",\"sortOrder\":2}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None);
        kpis.Verify(k => k.CreateAsync(owner, It.Is<HomeKpiCreateRequest>(r =>
            r.Kind == HomeKpiKind.ReportFavorite && r.ReportFavoriteId == favoriteId
            && r.DisplayMode == HomeKpiDisplayMode.TotalOnly && r.SortOrder == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReportsConfig_KpiCreate_ForeignFavorite_RejectedBeforeEffect()
    {
        var owner = Guid.NewGuid();
        var favorites = new Mock<IReportFavoriteService>();
        favorites.Setup(f => f.GetAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReportFavoriteDto?)null);
        var kpis = new Mock<IHomeKpiService>();
        var handler = ReportsConfigHandler(favorites: favorites, kpis: kpis);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args($"{{\"entity\":\"homeKpi\",\"operation\":\"create\",\"kind\":\"ReportFavorite\",\"reportFavoriteId\":\"{Guid.NewGuid()}\",\"displayMode\":\"TotalOnly\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        kpis.Verify(k => k.CreateAsync(It.IsAny<Guid>(), It.IsAny<HomeKpiCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReportsConfig_KpiCreate_PredefinedRequiresType()
    {
        var owner = Guid.NewGuid();
        var kpis = new Mock<IHomeKpiService>();
        var handler = ReportsConfigHandler(kpis: kpis);
        await AssertMcpErrorAsync(handler.InvokeAsync(
            Args("{\"entity\":\"homeKpi\",\"operation\":\"create\",\"kind\":\"Predefined\",\"displayMode\":\"TotalOnly\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None),
            "Err_Mcp_InvalidArguments");
        kpis.Verify(k => k.CreateAsync(It.IsAny<Guid>(), It.IsAny<HomeKpiCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReportsConfig_Deletes_RequireApproval_WithoutServiceEffect()
    {
        var owner = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var kpiId = Guid.NewGuid();
        var favorites = new Mock<IReportFavoriteService>();
        favorites.Setup(f => f.GetAsync(favoriteId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewFavorite(favoriteId));
        var kpis = new Mock<IHomeKpiService>();
        kpis.Setup(k => k.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HomeKpiDto> { NewKpi(kpiId) });
        var handler = ReportsConfigHandler(favorites: favorites, kpis: kpis);
        var call = NewCallContext(NewAccessContext(owner).Object);

        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"entity\":\"reportFavorite\",\"operation\":\"delete\",\"favoriteId\":\"{favoriteId}\"}}"), call, CancellationToken.None));
        await AssertApprovalRequiredAsync(handler.InvokeAsync(
            Args($"{{\"entity\":\"homeKpi\",\"operation\":\"delete\",\"kpiId\":\"{kpiId}\"}}"), call, CancellationToken.None));
        favorites.Verify(f => f.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        kpis.Verify(k => k.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReportsConfig_KpiUpdate_MergesFields()
    {
        var owner = Guid.NewGuid();
        var kpiId = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var kpis = new Mock<IHomeKpiService>();
        kpis.Setup(k => k.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HomeKpiDto> { NewKpi(kpiId, favoriteId) });
        kpis.Setup(k => k.UpdateAsync(kpiId, owner, It.IsAny<HomeKpiUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewKpi(kpiId, favoriteId));
        var favorites = new Mock<IReportFavoriteService>();
        favorites.Setup(f => f.GetAsync(favoriteId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewFavorite(favoriteId));
        var audit = CapturingAudit(out var staged);
        var handler = ReportsConfigHandler(favorites: favorites, kpis: kpis, audit: audit);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"entity\":\"homeKpi\",\"operation\":\"update\",\"kpiId\":\"{kpiId}\",\"sortOrder\":5,\"title\":\"t\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal("HomeKpiUpdated", staged()!.EventType);
        Assert.Contains(staged()!.Changes!, c => c.Field == "SortOrder" && c.NewValue == "5");
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ReportsConfig_FavoriteUpdate_ExplicitNull_ClearsFiltersAndKinds()
    {
        var owner = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var favorites = new Mock<IReportFavoriteService>();
        var existing = new ReportFavoriteDto(favoriteId, "fav", PostingKind.Bank, false,
            ReportInterval.Month, 24, false, false, false, false, false, DateTime.UtcNow, null,
            new List<PostingKind> { PostingKind.Bank, PostingKind.Security },
            new ReportFavoriteFiltersDto(new List<Guid> { Guid.NewGuid() }, null, null, null, null, null, null, null, null), false);
        favorites.Setup(f => f.GetAsync(favoriteId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        favorites.Setup(f => f.UpdateAsync(favoriteId, owner, It.IsAny<ReportFavoriteUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewFavorite(favoriteId));
        var handler = ReportsConfigHandler(favorites: favorites);
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"entity\":\"reportFavorite\",\"operation\":\"update\",\"favoriteId\":\"{favoriteId}\",\"filters\":null,\"postingKinds\":null}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        favorites.Verify(f => f.UpdateAsync(favoriteId, owner, It.Is<ReportFavoriteUpdateRequest>(r =>
            r.Filters == null && r.PostingKinds == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Cross-cutting: dispatcher-level write-stop (already generic) is not
    // re-tested here; approval ops never reach a service regardless.
    // ------------------------------------------------------------------
}
