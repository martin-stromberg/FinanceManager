using FinanceManager.Application.Accounts;
using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;
using IPostingsQueryService = FinanceManager.Application.Postings.IPostingsQueryService;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Unit tests of the EP-04 suggestion tools (T11 <c>fm_contacts_suggest</c>, T12
/// <c>fm_budget_rules_suggest</c>, T15 <c>fm_statement_draft_analyze</c>, T19
/// <c>fm_statement_draft_prepare_booking</c>): descriptor/scope contract, suggestion
/// output without persistence, scope denial, untrusted_data wrappers and the degraded
/// coverage when auxiliary read scopes are missing.
/// </summary>
public sealed class McpSuggestionToolsTests
{
    private static McpAccess NewAccess(Guid owner, McpAccessLevel level = McpAccessLevel.Assistent)
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
                ? McpLevelProfiles.GetDefaultScopes(McpAccessLevel.Assistent)
                    .Select(t => new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(), t.DataType, t.Right)).ToList()
                : scopes.ToList());
        return new McpRecordScopeFilter(evaluator.Object);
    }

    /// <summary>
    /// Scope evaluator granting every runtime check (conditional feature gates of the
    /// suggestion tools).
    /// </summary>
    private static IMcpScopeEvaluator AllowAllEvaluator()
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(),
                It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        evaluator.Setup(e => e.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpLevelProfiles.GetDefaultScopes(McpAccessLevel.Buchhalter)
                .Select(t => new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(), t.DataType, t.Right)).ToList());
        return evaluator.Object;
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonDocument Serialize(object result) => JsonDocument.Parse(JsonSerializer.Serialize(result));

    private static IMcpToolDescriptor[] SuggestionCatalog() => new IMcpToolDescriptor[]
    {
        new FmContactsSuggestDescriptor(),
        new FmBudgetRulesSuggestDescriptor(),
        new FmStatementDraftAnalyzeDescriptor(),
        new FmStatementDraftPrepareBookingDescriptor()
    };

    /// <summary>Test case.</summary>
    [Fact]
    public void Registry_AcceptsTheSuggestionCatalog()
    {
        var registry = new McpToolRegistry(SuggestionCatalog());
        Assert.Equal(4, registry.Tools.Count);
        Assert.All(registry.Tools, t =>
        {
            Assert.Equal(McpToolSecurityClass.Vorschlagend, t.SecurityClass);
            Assert.StartsWith("fm_", t.Name);
            Assert.True(t.RequiredScopes.Count > 0);
            Assert.Equal(JsonValueKind.Object, t.InputSchema.RootElement.ValueKind);
            // Every declared tuple must be a valid F3 matrix combination.
            Assert.All(t.RequiredScopes, s =>
                Assert.True(McpScopeMatrix.IsValidCombination(s.DataType, s.Right),
                    $"invalid scope tuple {s.DataType}/{s.Right} on {t.Name}"));
        });
    }

    /// <summary>Test case.</summary>
    [Fact]
    public void DeclaredScopes_MatchTheToolCatalog()
    {
        var expected = new Dictionary<string, (McpDataType, McpRightKind)[]>
        {
            [FmContactsSuggestDescriptor.ToolName] = new[]
            {
                (McpDataType.Contacts, McpRightKind.Suggest),
                (McpDataType.ContactCategories, McpRightKind.Suggest)
            },
            [FmBudgetRulesSuggestDescriptor.ToolName] = new[]
            {
                (McpDataType.BudgetRules, McpRightKind.Suggest)
            },
            [FmStatementDraftAnalyzeDescriptor.ToolName] = new[]
            {
                (McpDataType.StatementDrafts, McpRightKind.Suggest),
                (McpDataType.Contacts, McpRightKind.Read),
                (McpDataType.SavingsPlans, McpRightKind.Read),
                (McpDataType.Securities, McpRightKind.Read),
                (McpDataType.Accounts, McpRightKind.Read)
            },
            [FmStatementDraftPrepareBookingDescriptor.ToolName] = new[]
            {
                (McpDataType.StatementDrafts, McpRightKind.Suggest),
                (McpDataType.Postings, McpRightKind.Read)
            }
        };
        foreach (var (name, scopes) in expected)
        {
            var descriptor = SuggestionCatalog().Single(t => t.Name == name);
            Assert.Equal(scopes.Select(s => new McpToolScope(s.Item1, s.Item2)).ToList(), descriptor.RequiredScopes);
        }
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task Dispatch_SuggestionTool_DeniesAnalyseLevelAccess()
    {
        // Stufe Analyse carries no Suggest right — the dispatcher must reject before the
        // handler runs (suggestion tools stay reachable for Assistent and above).
        var owner = Guid.NewGuid();
        var access = new McpAccess(owner, "acc", "HINT01", "pbkdf2|1|AA|AA",
            McpAccessLevel.Analyse, McpApprovalMode.PerAction);
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.Access).Returns(access);
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);

        var descriptor = new FmStatementDraftAnalyzeDescriptor();
        var registry = new Mock<IMcpToolRegistry>();
        registry.SetupGet(r => r.CatalogVersion).Returns("katalog-v1");
        registry.Setup(r => r.Find(descriptor.Name)).Returns(descriptor);
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(access, McpDataType.StatementDrafts, McpRightKind.Suggest,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var audit = new Mock<IMcpAuditWriter>();
        var gate = new Mock<IMcpApprovalGate>();
        gate.Setup(g => g.EvaluateAsync(It.IsAny<McpAccess>(), It.IsAny<IMcpToolDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpApprovalDecision.Direct);
        var opts = new TestOptionsMonitor(new McpOptions());
        var dispatcher = new McpToolDispatcher(
            registry.Object, evaluator.Object, audit.Object,
            gate.Object, Mock.Of<IMcpApprovalService>(),
            new McpToolRateLimiter(opts),
            new McpAnomalyTracker(opts, Mock.Of<IServiceProvider>(), NullLogger<McpAnomalyTracker>.Instance),
            opts, NullLogger<McpToolDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync(ctx.Object, descriptor.Name,
            Args($"{{\"draftId\":\"{Guid.NewGuid()}\"}}"), Mock.Of<IServiceProvider>(), null, CancellationToken.None);
        Assert.Equal(McpDispatchResultKind.DefinedError, result.Kind);
        Assert.Equal("Err_Mcp_ScopeDenied", result.ErrorCode);
    }

    // ----------------------------------------------------------------- T11

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ContactsSuggest_MaintenanceMode_ProducesCategorizedSuggestions()
    {
        var owner = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var dupA = new ContactDto(Guid.NewGuid(), "ACME GmbH", ContactType.Organization, categoryId, null, false, null);
        var dupB = new ContactDto(Guid.NewGuid(), "acme gmbh", ContactType.Organization, null, null, false, null);
        var lonely = new ContactDto(Guid.NewGuid(), "Einzel <inject>", ContactType.Person, null, null, false, null);
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(owner, 0, 200, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { dupA, dupB, lonely });
        var aliasId = Guid.NewGuid();
        contacts.Setup(c => c.ListAliases(dupA.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AliasNameDto(aliasId, dupA.Id, "ACME GmbH") });
        contacts.Setup(c => c.ListAliases(dupB.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AliasNameDto(Guid.NewGuid(), dupB.Id, "ACME GmbH") });
        contacts.Setup(c => c.ListAliases(lonely.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AliasNameDto>());
        var categories = new Mock<IContactCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ContactCategoryDto(categoryId, "Lieferanten", null) });
        var handler = new FmContactsSuggestHandler(
            contacts.Object, categories.Object, Mock.Of<IKnownContactCatalog>(), NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal("maintenance", doc.RootElement.GetProperty("mode").GetString());
        var kinds = doc.RootElement.GetProperty("suggestions").EnumerateArray()
            .Select(s => s.GetProperty("kind").GetString()).ToList();
        Assert.Contains("missing_category", kinds);
        Assert.Contains("duplicate_candidate", kinds);
        Assert.Contains("alias_conflict", kinds);   // same pattern on two contacts
        Assert.Contains("redundant_alias", kinds);  // pattern equals the own name
        // No write path: suggestion tools never call contact mutations.
        contacts.Verify(c => c.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ContactType>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
        contacts.Verify(c => c.MergeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>(), It.IsAny<Shared.Dtos.Contacts.MergePreference>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ContactsSuggest_ResolutionMode_ResolvesViaAlias_AndWrapsUntrusted()
    {
        var owner = Guid.NewGuid();
        var contact = new ContactDto(Guid.NewGuid(), "Stadtwerke <inject>", ContactType.Organization, null, null, false, null);
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(owner, 0, 200, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { contact });
        contacts.Setup(c => c.ListAliases(contact.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AliasNameDto(Guid.NewGuid(), contact.Id, "stadtwerke*") });
        var handler = new FmContactsSuggestHandler(
            contacts.Object, Mock.Of<IContactCategoryService>(), Mock.Of<IKnownContactCatalog>(), NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"referenceText\":\"STADTWERKE MUSTERSTADT <inject>\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal("resolution", doc.RootElement.GetProperty("mode").GetString());
        var suggestion = doc.RootElement.GetProperty("suggestions")[0];
        Assert.Equal("resolve_contact", suggestion.GetProperty("kind").GetString());
        Assert.Equal(contact.Id.ToString(), suggestion.GetProperty("proposedContactId").GetString());
        Assert.Equal("aliasPattern", suggestion.GetProperty("matchBasis").GetString());
        Assert.Equal("Stadtwerke <inject>", suggestion.GetProperty("untrusted_data").GetProperty("contactName").GetString());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ContactsSuggest_ResolutionMode_KnownCatalogYieldsNewContactSuggestion()
    {
        var owner = Guid.NewGuid();
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(owner, 0, 200, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactDto>());
        var catalog = new Mock<IKnownContactCatalog>();
        catalog.Setup(k => k.FindMatchAsync(It.IsAny<IEnumerable<string?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnownContactMatch("Telekom <inject>", ContactType.Organization, new[] { "telekom*" }));
        var handler = new FmContactsSuggestHandler(
            contacts.Object, Mock.Of<IContactCategoryService>(), catalog.Object, NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"referenceText\":\"TELEKOM DEUTSCHLAND\"}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var suggestion = doc.RootElement.GetProperty("suggestions")[0];
        Assert.Equal("new_contact", suggestion.GetProperty("kind").GetString());
        Assert.Equal("Telekom <inject>", suggestion.GetProperty("untrusted_data").GetProperty("name").GetString());
        // The suggestion names a creation path — but the tool itself creates nothing.
        contacts.Verify(c => c.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ContactType>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ContactsSuggest_RecordFilter_UsesSuggestScopeRows()
    {
        // A filtered (Kontakte, Vorschlag) row must restrict the suggestion source:
        // the contact outside the whitelist takes no part in duplicate/maintenance
        // findings (Abweichung 1 — the filter must bind to the Suggest rows).
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var allowedCat = Guid.NewGuid();
        var deniedCat = Guid.NewGuid();
        var keptA = new ContactDto(Guid.NewGuid(), "Dup <inject>", ContactType.Organization, allowedCat, null, false, null);
        var keptB = new ContactDto(Guid.NewGuid(), "dup <inject>", ContactType.Organization, allowedCat, null, false, null);
        var dropped = new ContactDto(Guid.NewGuid(), "DUP <inject>", ContactType.Organization, deniedCat, null, false, null);
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(owner, 0, 200, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { keptA, keptB, dropped });
        contacts.Setup(c => c.ListAliases(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AliasNameDto>());
        var categories = new Mock<IContactCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ContactCategoryDto(allowedCat, "A", null),
                new ContactCategoryDto(deniedCat, "B", null)
            });
        var filter = NewFilter(
            new McpAccessScope(access.Id, owner, McpDataType.Contacts, McpRightKind.Suggest,
                contactCategoryIdsCsv: allowedCat.ToString()),
            new McpAccessScope(access.Id, owner, McpDataType.ContactCategories, McpRightKind.Suggest));
        var handler = new FmContactsSuggestHandler(
            contacts.Object, categories.Object, Mock.Of<IKnownContactCatalog>(), filter);

        using var doc = Serialize(await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        Assert.Equal(2, doc.RootElement.GetProperty("contactCount").GetInt32());
        var suggestions = doc.RootElement.GetProperty("suggestions");
        Assert.Contains(suggestions.EnumerateArray(),
            s => s.GetProperty("kind").GetString() == "duplicate_candidate");
        Assert.DoesNotContain(dropped.Id.ToString(), suggestions.GetRawText());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task ContactsSuggest_RecordFilter_IgnoresReadScopeRows()
    {
        // Regression of Abweichung 1: a filtered (Kontakte, Lesen) row must NOT restrict
        // the suggestion source — the Suggest rows decide (here unfiltered → visible).
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var contact = new ContactDto(Guid.NewGuid(), "Visible <inject>", ContactType.Person, null, null, false, null);
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(owner, 0, 200, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { contact });
        contacts.Setup(c => c.ListAliases(contact.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AliasNameDto>());
        var categories = new Mock<IContactCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactCategoryDto>());
        var filter = NewFilter(
            new McpAccessScope(access.Id, owner, McpDataType.Contacts, McpRightKind.Read,
                contactCategoryIdsCsv: Guid.NewGuid().ToString()),
            new McpAccessScope(access.Id, owner, McpDataType.Contacts, McpRightKind.Suggest),
            new McpAccessScope(access.Id, owner, McpDataType.ContactCategories, McpRightKind.Suggest));
        var handler = new FmContactsSuggestHandler(
            contacts.Object, categories.Object, Mock.Of<IKnownContactCatalog>(), filter);

        using var doc = Serialize(await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        Assert.Equal(1, doc.RootElement.GetProperty("contactCount").GetInt32());
        Assert.Contains(doc.RootElement.GetProperty("suggestions").EnumerateArray(),
            s => s.GetProperty("kind").GetString() == "missing_category");
    }

    // ----------------------------------------------------------------- T12

    /// <summary>Test case.</summary>
    [Fact]
    public async Task BudgetRulesSuggest_WithoutBudgetsRead_DegradesToEmpty()
    {
        var owner = Guid.NewGuid();
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), McpDataType.Budgets, McpRightKind.Read,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(),
                It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var purposes = new Mock<IBudgetPurposeService>();
        var handler = new FmBudgetRulesSuggestHandler(
            purposes.Object, Mock.Of<IBudgetRuleService>(), Mock.Of<IBudgetOverrideService>(),
            Mock.Of<IBudgetCategoryService>(), Mock.Of<IPostingsQueryService>(),
            Mock.Of<IContactService>(), Mock.Of<Application.Savings.ISavingsPlanService>(),
            evaluator.Object, NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal(0, doc.RootElement.GetProperty("suggestions").GetArrayLength());
        Assert.Contains("budgets", doc.RootElement.GetProperty("degraded").EnumerateArray()
            .Select(x => x.GetString()));
        // No purpose was enumerated — nothing leaks through the suggestion.
        purposes.Verify(p => p.ListOverviewAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<BudgetSourceType?>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<BudgetReportDateBasis>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task BudgetRulesSuggest_PurposeWithoutRules_SuggestsRuleWithDerivedAmount()
    {
        var owner = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purpose = new BudgetPurposeOverviewDto(
            Guid.NewGuid(), owner, "Miete <inject>", null, BudgetSourceType.Contact, contactId,
            0, 0m, 1200m, 1200m, "Vermieter", null, null, null);
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.ListOverviewAsync(owner, 0, 50, null, null,
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), null, It.IsAny<CancellationToken>(),
                It.IsAny<BudgetReportDateBasis>()))
            .ReturnsAsync(new[] { purpose });
        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.ListByPurposeAsync(owner, purpose.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetRuleDto>());
        var overrides = new Mock<IBudgetOverrideService>();
        overrides.Setup(o => o.ListByPurposeAsync(owner, purpose.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetOverrideDto>());
        var postings = new Mock<IPostingsQueryService>();
        postings.Setup(p => p.GetContactPostingsAsync(contactId, 0, 200, null,
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Posting(-400m, "MIETE WOHNUNG <inject>"),
                Posting(-400m, "MIETE WOHNUNG <inject>"),
                Posting(-400m, "MIETE WOHNUNG <inject>")
            });
        var categories = new Mock<IBudgetCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetCategoryDto>());
        var handler = new FmBudgetRulesSuggestHandler(
            purposes.Object, rules.Object, overrides.Object, categories.Object,
            postings.Object, Mock.Of<IContactService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(), AllowAllEvaluator(), NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var suggestion = doc.RootElement.GetProperty("suggestions")
            .EnumerateArray().Single(s => s.GetProperty("kind").GetString() == "missing_rule");
        Assert.Equal("Rules", suggestion.GetProperty("field").GetString());
        Assert.Equal(400m, suggestion.GetProperty("proposedValue").GetProperty("amount").GetDecimal());
        Assert.Equal("Miete <inject>", suggestion.GetProperty("untrusted_data").GetProperty("purposeName").GetString());
        // No write path on the budget services.
        rules.Verify(r => r.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
            It.IsAny<BudgetIntervalType>(), It.IsAny<int?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task BudgetRulesSuggest_RecordFilter_HidesOutOfScopePurpose()
    {
        // A filtered (Budgetregeln, Vorschlag) row restricts the primary data: the
        // purpose whose actuals source is a contact outside the whitelist produces
        // no suggestions (Abweichung 2 — previously no record filter ran at all).
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var allowedCat = Guid.NewGuid();
        var deniedCat = Guid.NewGuid();
        var contactIn = Guid.NewGuid();
        var contactOut = Guid.NewGuid();
        var purposeIn = new BudgetPurposeOverviewDto(
            Guid.NewGuid(), owner, "In", null, BudgetSourceType.Contact, contactIn,
            0, 0m, 100m, 100m, null, null, null, null);
        var purposeOut = new BudgetPurposeOverviewDto(
            Guid.NewGuid(), owner, "Out", null, BudgetSourceType.Contact, contactOut,
            0, 0m, 200m, 200m, null, null, null, null);
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.ListOverviewAsync(owner, 0, 50, null, null,
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), null, It.IsAny<CancellationToken>(),
                It.IsAny<BudgetReportDateBasis>()))
            .ReturnsAsync(new[] { purposeIn, purposeOut });
        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.ListByPurposeAsync(owner, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetRuleDto>());
        var overrides = new Mock<IBudgetOverrideService>();
        overrides.Setup(o => o.ListByPurposeAsync(owner, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetOverrideDto>());
        var postings = new Mock<IPostingsQueryService>();
        postings.Setup(p => p.GetContactPostingsAsync(It.IsAny<Guid>(), 0, 200, null,
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Shared.Dtos.Postings.PostingServiceDto>());
        var categories = new Mock<IBudgetCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetCategoryDto>());
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(contactIn, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(contactIn, "In", ContactType.Organization, allowedCat, null, false, null));
        contacts.Setup(c => c.GetAsync(contactOut, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(contactOut, "Out", ContactType.Organization, deniedCat, null, false, null));
        var filter = NewFilter(
            new McpAccessScope(access.Id, owner, McpDataType.BudgetRules, McpRightKind.Suggest,
                contactCategoryIdsCsv: allowedCat.ToString()));
        var handler = new FmBudgetRulesSuggestHandler(
            purposes.Object, rules.Object, overrides.Object, categories.Object,
            postings.Object, contacts.Object,
            Mock.Of<Application.Savings.ISavingsPlanService>(), AllowAllEvaluator(), filter);

        using var doc = Serialize(await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        var suggestions = doc.RootElement.GetProperty("suggestions");
        Assert.NotEmpty(suggestions.EnumerateArray());
        Assert.All(suggestions.EnumerateArray(), s =>
            Assert.Equal(purposeIn.Id.ToString(), s.GetProperty("targetBudgetPurposeId").GetString()));
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task BudgetRulesSuggest_RecordFilter_AppliesToPostingHistory()
    {
        // The posting history inherits the (Buchungen, Lesen) record filter (like T15):
        // a scope row whitelisting another contact category drops the loaded postings —
        // the missing_rule proposal then degrades to "amount not derivable".
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var allowedCat = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purpose = new BudgetPurposeOverviewDto(
            Guid.NewGuid(), owner, "Miete", null, BudgetSourceType.Contact, contactId,
            0, 0m, 1200m, 1200m, "Vermieter", null, null, null);
        var purposes = new Mock<IBudgetPurposeService>();
        purposes.Setup(p => p.ListOverviewAsync(owner, 0, 50, null, null,
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), null, It.IsAny<CancellationToken>(),
                It.IsAny<BudgetReportDateBasis>()))
            .ReturnsAsync(new[] { purpose });
        var rules = new Mock<IBudgetRuleService>();
        rules.Setup(r => r.ListByPurposeAsync(owner, purpose.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetRuleDto>());
        var overrides = new Mock<IBudgetOverrideService>();
        overrides.Setup(o => o.ListByPurposeAsync(owner, purpose.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetOverrideDto>());
        var postings = new Mock<IPostingsQueryService>();
        postings.Setup(p => p.GetContactPostingsAsync(contactId, 0, 200, null,
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Posting(-400m, "MIETE"), Posting(-400m, "MIETE"), Posting(-400m, "MIETE")
            });
        var categories = new Mock<IBudgetCategoryService>();
        categories.Setup(c => c.ListAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetCategoryDto>());
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.GetAsync(contactId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(contactId, "Vermieter", ContactType.Organization,
                allowedCat, null, false, null));
        var filter = NewFilter(
            new McpAccessScope(access.Id, owner, McpDataType.BudgetRules, McpRightKind.Suggest,
                contactCategoryIdsCsv: allowedCat.ToString()),
            new McpAccessScope(access.Id, owner, McpDataType.Postings, McpRightKind.Read,
                contactCategoryIdsCsv: Guid.NewGuid().ToString()));
        var handler = new FmBudgetRulesSuggestHandler(
            purposes.Object, rules.Object, overrides.Object, categories.Object,
            postings.Object, contacts.Object,
            Mock.Of<Application.Savings.ISavingsPlanService>(), AllowAllEvaluator(), filter);

        using var doc = Serialize(await handler.InvokeAsync(Args("{}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        var suggestion = doc.RootElement.GetProperty("suggestions")
            .EnumerateArray().Single(s => s.GetProperty("kind").GetString() == "missing_rule");
        Assert.Equal(JsonValueKind.Null, suggestion.GetProperty("proposedValue").ValueKind);
        Assert.Equal("low", suggestion.GetProperty("confidence").GetString());
        // The history was loaded but record-filtered away — not skipped up front.
        postings.Verify(p => p.GetContactPostingsAsync(contactId, 0, 200, null,
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), owner, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Shared.Dtos.Postings.PostingServiceDto Posting(decimal amount, string subject)
        => new(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1), amount,
            Shared.Dtos.Postings.PostingKind.Contact, null, Guid.NewGuid(), null, null, Guid.NewGuid(),
            subject, "recipient", "description", null, null, Guid.NewGuid(), null, null, null, null, null,
            null, null, null, false, false, null, null);

    // ----------------------------------------------------------------- T15

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftAnalyze_SuggestsContactViaAlias_WithoutPersisting()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var entry = new StatementDraftEntryDto(Guid.NewGuid(), 1, DateTime.UtcNow, null, -19.99m, "EUR",
            "KARTENZAHLUNG", "ACME STORE <inject>", "POS", false, false,
            StatementDraftEntryStatus.Open, null, null, false, null, null, null, null, null, null);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Header(draftId, accountId));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        var contacts = new Mock<IContactService>();
        var self = new ContactDto(Guid.NewGuid(), "Self", ContactType.Self, null, null, false, null);
        var target = new ContactDto(contactId, "ACME", ContactType.Organization, null, null, false, null);
        contacts.Setup(c => c.ListAsync(owner, 0, 200, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { self, target });
        contacts.Setup(c => c.ListAliases(contactId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AliasNameDto(Guid.NewGuid(), contactId, "acme*") });
        contacts.Setup(c => c.ListAliases(self.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AliasNameDto>());
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.ListAsync(owner, 0, 200, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AccountDto(accountId, "Giro", AccountType.Giro, "DE00", 0m, Guid.NewGuid(), null,
                    SavingsPlanExpectation.None, false)
            });
        var savings = new Mock<Application.Savings.ISavingsPlanService>();
        savings.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Shared.Dtos.SavingsPlans.SavingsPlanDto>());
        var securities = new Mock<Application.Securities.ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Shared.Dtos.Securities.SecurityDto>());
        // No Postings/StatementImports read scope → heuristics marked not-checked.
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(),
                It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new FmStatementDraftAnalyzeHandler(
            drafts.Object, contacts.Object, savings.Object, securities.Object, accounts.Object,
            Mock.Of<IPostingsQueryService>(), Mock.Of<IStatementImportQueryService>(),
            Mock.Of<IKnownContactCatalog>(), evaluator.Object, NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var analyzed = doc.RootElement.GetProperty("analysis").GetProperty("entries")[0];
        var suggestion = analyzed.GetProperty("suggestions")[0];
        Assert.Equal("contact", suggestion.GetProperty("kind").GetString());
        Assert.Equal(contactId.ToString(), suggestion.GetProperty("proposedContactId").GetString());
        Assert.False(analyzed.GetProperty("duplicate").GetProperty("pipelineFlagged").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("analysis").GetProperty("heuristicScope")
            .GetProperty("bankPostings").GetBoolean()); // conditional source not granted
        Assert.Equal("ACME STORE <inject>", analyzed.GetProperty("untrusted_data").GetProperty("recipientName").GetString());
        // EP-04 core invariant: no classification or mutation path was invoked.
        drafts.Verify(d => d.ClassifyAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        drafts.Verify(d => d.SetEntryContactAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        drafts.Verify(d => d.BookAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid>(),
            It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftAnalyze_AlreadyBookedEntry_ReportsFlag_WithoutSuggestions()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var entry = new StatementDraftEntryDto(Guid.NewGuid(), 1, DateTime.UtcNow, null, -5m, "EUR",
            "dup", "x", null, false, false, StatementDraftEntryStatus.AlreadyBooked,
            null, null, false, null, null, null, null, null, null);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Header(draftId, null));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.IsAllowedAsync(It.IsAny<McpAccess>(), It.IsAny<McpDataType>(),
                It.IsAny<McpRightKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = NewAnalyzeHandler(drafts.Object, evaluator.Object);

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var analyzed = doc.RootElement.GetProperty("analysis").GetProperty("entries")[0];
        Assert.True(analyzed.GetProperty("duplicate").GetProperty("pipelineFlagged").GetBoolean());
        Assert.Equal("alreadyBooked", analyzed.GetProperty("classificationSkipped").GetString());
        Assert.Equal(0, analyzed.GetProperty("suggestions").GetArrayLength());
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task DraftAnalyze_RecordFilteredDraft_IsInvisible()
    {
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        // Suggest scope row restricted to AccountType=1 (Savings) — a Giro draft is invisible.
        var scope = new McpAccessScope(access.Id, owner, McpDataType.StatementDrafts,
            McpRightKind.Suggest, accountTypesCsv: "1");
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Header(draftId, accountId));
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDto(accountId, "g", AccountType.Giro, null, 0m, Guid.Empty, null,
                SavingsPlanExpectation.None, false));
        var handler = new FmStatementDraftAnalyzeHandler(
            drafts.Object, Mock.Of<IContactService>(), Mock.Of<Application.Savings.ISavingsPlanService>(),
            Mock.Of<Application.Securities.ISecurityService>(), accounts.Object,
            Mock.Of<IPostingsQueryService>(), Mock.Of<IStatementImportQueryService>(),
            Mock.Of<IKnownContactCatalog>(), Mock.Of<IMcpScopeEvaluator>(), NewFilter(scope));

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("analysis").ValueKind);
    }

    private static StatementDraftDto Header(Guid draftId, Guid? accountId)
        => new(draftId, "f.csv", null, accountId, null, null, StatementDraftStatus.Draft,
            0m, false, null, null, null, null, Array.Empty<StatementDraftEntryDto>());

    private static FmStatementDraftAnalyzeHandler NewAnalyzeHandler(
        IStatementDraftService drafts, IMcpScopeEvaluator evaluator)
    {
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<ContactType?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactDto>());
        var savings = new Mock<Application.Savings.ISavingsPlanService>();
        savings.Setup(s => s.ListAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Shared.Dtos.SavingsPlans.SavingsPlanDto>());
        var securities = new Mock<Application.Securities.ISecurityService>();
        securities.Setup(s => s.ListAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Shared.Dtos.Securities.SecurityDto>());
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.ListAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AccountDto>());
        return new FmStatementDraftAnalyzeHandler(
            drafts, contacts.Object, savings.Object, securities.Object, accounts.Object,
            Mock.Of<IPostingsQueryService>(), Mock.Of<IStatementImportQueryService>(),
            Mock.Of<IKnownContactCatalog>(), evaluator, NewFilter());
    }

    // ----------------------------------------------------------------- T19

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PrepareBooking_SplitsBookableAndBlocked_AndRecommendsForceWarnings()
    {
        var owner = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var selfId = Guid.NewGuid();
        var ok = new StatementDraftEntryDto(Guid.NewGuid(), 1, DateTime.UtcNow, null, -10m, "EUR",
            "s1", "r1", null, false, false, StatementDraftEntryStatus.Open,
            contactId, null, false, null, null, null, null, null, null);
        var dup = new StatementDraftEntryDto(Guid.NewGuid(), 2, DateTime.UtcNow, null, -5m, "EUR",
            "s2", "r2", null, false, false, StatementDraftEntryStatus.AlreadyBooked,
            contactId, null, false, null, null, null, null, null, null);
        var noContact = new StatementDraftEntryDto(Guid.NewGuid(), 3, DateTime.UtcNow, null, -1m, "EUR",
            "s3", "r3", null, false, false, StatementDraftEntryStatus.Open,
            null, null, false, null, null, null, null, null, null);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Header(draftId, accountId));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ok, dup, noContact });
        drafts.Setup(d => d.ValidateAsync(draftId, null, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DraftValidationResultDto(draftId, true,
                new[] { new DraftValidationMessageDto("Warn_X", "Warning", "check <inject>", draftId, ok.Id) }));
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(owner, 0, 200, ContactType.Self, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ContactDto(selfId, "Self", ContactType.Self, null, null, false, null) });
        contacts.Setup(c => c.GetAsync(contactId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(contactId, "C", ContactType.Organization, null, null, false, null));
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDto(accountId, "g", AccountType.Giro, null, 100m, Guid.Empty, null,
                SavingsPlanExpectation.None, false));
        var handler = new FmStatementDraftPrepareBookingHandler(
            drafts.Object, contacts.Object, accounts.Object,
            Mock.Of<Application.Savings.ISavingsPlanService>(),
            Mock.Of<Application.Securities.ISecurityService>(),
            AllowAllEvaluator(), NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var plan = doc.RootElement.GetProperty("plan");
        Assert.True(plan.GetProperty("canBook").GetBoolean());
        Assert.True(plan.GetProperty("forceWarningsRecommended").GetBoolean());
        var bookable = plan.GetProperty("bookableEntries");
        Assert.Single(bookable.EnumerateArray());
        var kinds = plan.GetProperty("expectedEffect").GetProperty("postingKinds");
        Assert.Equal(1, kinds.GetProperty("bank").GetInt32());
        Assert.Equal(1, kinds.GetProperty("contact").GetInt32());
        Assert.Equal(-10m, plan.GetProperty("expectedEffect").GetProperty("bankAmountDelta").GetDecimal());
        Assert.Equal(90m, plan.GetProperty("expectedEffect").GetProperty("expectedBalance").GetDecimal());
        var blocked = plan.GetProperty("blockedEntries").EnumerateArray().ToList();
        Assert.Equal(2, blocked.Count);
        var blockedCodes = blocked.SelectMany(b => b.GetProperty("blocks").EnumerateArray()
            .Select(x => x.GetProperty("code").GetString())).ToList();
        Assert.Contains("alreadyBooked", blockedCodes);
        Assert.Contains("missingContact", blockedCodes);
        // Validation message text stays inside the untrusted wrapper.
        var msg = plan.GetProperty("validation").GetProperty("messages")[0];
        Assert.Equal("check <inject>", msg.GetProperty("untrusted_data").GetProperty("message").GetString());
        // The plan never books.
        drafts.Verify(d => d.BookAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid>(),
            It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        drafts.Verify(d => d.CommitAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<ImportFormat>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PrepareBooking_ForeignDraft_ReturnsNullPlan()
    {
        var owner = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(It.IsAny<Guid>(), owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StatementDraftDto?)null);
        var handler = new FmStatementDraftPrepareBookingHandler(
            drafts.Object, Mock.Of<IContactService>(), Mock.Of<IAccountService>(),
            Mock.Of<Application.Savings.ISavingsPlanService>(),
            Mock.Of<Application.Securities.ISecurityService>(),
            AllowAllEvaluator(), NewFilter());

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{Guid.NewGuid()}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("plan").ValueKind);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PrepareBooking_ScopeFilteredDraft_ReturnsNullPlan()
    {
        // Same draft-level gate as T15: a (Kontoauszugsentwürfe, Vorschlag) row filtered
        // to Savings accounts makes a Giro draft invisible — no plan is emitted.
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Header(draftId, accountId));
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDto(accountId, "g", AccountType.Giro, null, 0m, Guid.Empty, null,
                SavingsPlanExpectation.None, false));
        var scope = new McpAccessScope(access.Id, owner, McpDataType.StatementDrafts,
            McpRightKind.Suggest, accountTypesCsv: "1");
        var handler = new FmStatementDraftPrepareBookingHandler(
            drafts.Object, Mock.Of<IContactService>(), accounts.Object,
            Mock.Of<Application.Savings.ISavingsPlanService>(),
            Mock.Of<Application.Securities.ISecurityService>(),
            Mock.Of<IMcpScopeEvaluator>(), NewFilter(scope));

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("plan").ValueKind);
    }

    /// <summary>Test case.</summary>
    [Fact]
    public async Task PrepareBooking_RecordFilter_EvaluatesEntriesIndividually()
    {
        // Abweichung 3: with a filtered scope row the per-entry filter resolves the
        // linked-record categories of each entry — the savings-plan lookup proves the
        // individual evaluation ran (no such call exists outside the filter).
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plain = new StatementDraftEntryDto(Guid.NewGuid(), 1, DateTime.UtcNow, null, -10m, "EUR",
            "s1", "r1", null, false, false, StatementDraftEntryStatus.Open,
            contactId, null, false, null, null, null, null, null, null);
        var linked = new StatementDraftEntryDto(Guid.NewGuid(), 2, DateTime.UtcNow, null, -20m, "EUR",
            "s2", "r2", null, false, false, StatementDraftEntryStatus.Open,
            contactId, planId, false, null, null, null, null, null, null);
        var drafts = new Mock<IStatementDraftService>();
        drafts.Setup(d => d.GetDraftHeaderAsync(draftId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Header(draftId, accountId));
        drafts.Setup(d => d.GetDraftEntriesAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plain, linked });
        drafts.Setup(d => d.ValidateAsync(draftId, null, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DraftValidationResultDto(draftId, true,
                Array.Empty<DraftValidationMessageDto>()));
        var contacts = new Mock<IContactService>();
        contacts.Setup(c => c.ListAsync(owner, 0, 200, ContactType.Self, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactDto>());
        contacts.Setup(c => c.GetAsync(contactId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(contactId, "C", ContactType.Organization, null, null, false, null));
        var savings = new Mock<Application.Savings.ISavingsPlanService>();
        savings.Setup(s => s.GetAsync(planId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shared.Dtos.SavingsPlans.SavingsPlanDto { Id = planId });
        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAsync(accountId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDto(accountId, "g", AccountType.Giro, null, 100m, Guid.Empty, null,
                SavingsPlanExpectation.None, false));
        var scope = new McpAccessScope(access.Id, owner, McpDataType.StatementDrafts,
            McpRightKind.Suggest, accountTypesCsv: "0");
        var handler = new FmStatementDraftPrepareBookingHandler(
            drafts.Object, contacts.Object, accounts.Object, savings.Object,
            Mock.Of<Application.Securities.ISecurityService>(),
            AllowAllEvaluator(), NewFilter(scope));

        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"draftId\":\"{draftId}\"}}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        var plan = doc.RootElement.GetProperty("plan");
        Assert.Equal(2, plan.GetProperty("bookableEntries").GetArrayLength());
        // The per-entry record filter classified the linked savings plan (Kategorie
        // über das Aggregate) — only the filter performs this lookup.
        savings.Verify(s => s.GetAsync(planId, owner, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<McpOptions>
    {
        private readonly McpOptions _value;

        public TestOptionsMonitor(McpOptions value)
        {
            _value = value;
        }

        public McpOptions CurrentValue => _value;
        public McpOptions Get(string? name) => _value;
        public IDisposable? OnChange(Action<McpOptions, string?> listener) => null;
    }
}
