using Bunit;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Attachments;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web;
using FinanceManager.Web.Components.Pages;
using FinanceManager.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using msTools.Web.Blazor;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FinanceManager.Tests.Components
{
    /// <summary>
    /// Tests for the back-navigation handling of <see cref="FinanceManager.Web.Components.Pages.CardPage"/>:
    /// app-relative payload routes (<c>card/…</c>, <c>list/…</c>, <c>reports/…</c>) navigate directly instead of
    /// being mangled into the legacy list-route fallback, an explicit <c>?back=</c> query parameter takes
    /// precedence over the view-model payload, single-token subtype payloads keep the legacy typed-list
    /// behaviour, and the <c>OpenCategory</c> action propagates the current card as <c>?back=</c> target so the
    /// category card can return to its origin (issues #600/#602/#606/#607/#608).
    /// </summary>
    public sealed class CardPageBackNavigationTests : BunitContext
    {
        /// <summary>
        /// Fixes the thread culture to en-US and stubs the loading-bar JS interop calls the card page
        /// invokes during render.
        /// </summary>
        public CardPageBackNavigationTests()
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Services.AddScoped<LoadingBarService>();
            Services.AddSingleton<IConfirmationService>(Mock.Of<IConfirmationService>());
            Services.AddSingleton(new HttpClient(new EmptyChartHandler()) { BaseAddress = new Uri("http://localhost") });
            JSInterop.SetupVoid("financeManager.loadingBar.start").SetVoidResult();
            JSInterop.SetupVoid("financeManager.loadingBar.stop").SetVoidResult();
        }

        /// <summary>
        /// Renders the MCP audit card and clicks its ribbon Back action: the view-model sends the
        /// app-relative payload <c>card/setup</c>, which must navigate straight to that card route
        /// instead of producing the invalid legacy route <c>list/mcp-audit/card/setup</c> (issue #607).
        /// </summary>
        [Fact]
        public void McpAuditCard_Back_NavigatesToPayloadCardRoute()
        {
            var apiMock = CreateMcpAuditApiMock();
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "mcp-audit")
                .Add(p => p.Id, Guid.Empty));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Back"));
            cut.FindAll("button").First(b => b.Id == "Back").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith("card/setup", nav.Uri);
        }

        /// <summary>
        /// When the card URL carries an explicit <c>?back=</c> parameter it takes precedence over the
        /// view-model's back payload: the MCP audit card reached with <c>?back=card/mcp-accesses</c>
        /// must return to the accesses card, not to the default <c>card/setup</c> payload target.
        /// </summary>
        [Fact]
        public void McpAuditCard_Back_PrefersExplicitBackParameter()
        {
            var apiMock = CreateMcpAuditApiMock();
            RegisterServices(apiMock);

            Services.GetRequiredService<NavigationManager>()
                .NavigateTo("card/mcp-audit?back=" + Uri.EscapeDataString("card/mcp-accesses"));

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "mcp-audit")
                .Add(p => p.Id, Guid.Empty));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Back"));
            cut.FindAll("button").First(b => b.Id == "Back").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith("card/mcp-accesses", nav.Uri);
        }

        /// <summary>
        /// An existing budget rule sends <c>card/budget/purposes/{id}</c> as back payload; the click
        /// must navigate to the parent purpose card instead of the invalid fallback
        /// <c>list/budget/card/budget/purposes/{id}</c> (issues #600/#608).
        /// </summary>
        [Fact]
        public void BudgetRuleCard_Back_NavigatesToParentPurposeCard()
        {
            var apiMock = new Mock<IApiClient>();
            var ruleId = Guid.NewGuid();
            var purposeId = Guid.NewGuid();
            apiMock.Setup(a => a.Budgets_GetRuleAsync(ruleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BudgetRuleDto(ruleId, Guid.NewGuid(), purposeId, null, 25m,
                    BudgetIntervalType.Monthly, null, DateOnly.FromDateTime(DateTime.Today), null));
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "budget")
                .Add(p => p.SubKind, "rules")
                .Add(p => p.Id, ruleId));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Back"));
            cut.FindAll("button").First(b => b.Id == "Back").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith($"card/budget/purposes/{purposeId}", nav.Uri);
        }

        /// <summary>
        /// Regression guard: a single-token subtype payload such as <c>account/{guid}</c> from the
        /// postings card is not an app-relative route and must keep the legacy behaviour, navigating
        /// to the typed list route <c>list/postings/account/{guid}</c>.
        /// </summary>
        [Fact]
        public void PostingsCard_Back_KeepsLegacyTypedListRoute()
        {
            var apiMock = new Mock<IApiClient>();
            var postingId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            apiMock.Setup(a => a.Postings_GetByIdAsync(postingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PostingServiceDto(postingId, DateTime.Today, DateTime.Today, 12.5m,
                    PostingKind.Bank, accountId, null, null, null, Guid.NewGuid(),
                    "subject", "recipient", "description", null, null, Guid.NewGuid(),
                    null, null, null, null, null, null, null, null, false, false, null, null));
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "postings")
                .Add(p => p.Id, postingId));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Back"));
            cut.FindAll("button").First(b => b.Id == "Back").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith($"list/postings/account/{accountId}", nav.Uri);
        }

        /// <summary>
        /// The <c>OpenCategory</c> ribbon action of the savings-plan card must carry the current card
        /// URL as <c>?back=</c> target so the category card's Back action returns to the originating
        /// savings-plan card (issue #602).
        /// </summary>
        [Fact]
        public void SavingsPlanCard_OpenCategory_AppendsCurrentCardAsBackTarget()
        {
            var apiMock = new Mock<IApiClient>();
            var planId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            apiMock.Setup(a => a.SavingsPlans_GetAsync(planId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SavingsPlanDto(planId, "Plan A", SavingsPlanType.OneTime, null, null,
                    null, true, DateTime.UtcNow, null, categoryId));
            apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new SavingsPlanCategoryDto { Id = categoryId, Name = "Cat" } });
            apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(planId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("no analysis"));
            apiMock.Setup(a => a.SavingsPlans_GetLinkedAccountsAsync(planId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            apiMock.Setup(a => a.GetAccountsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<FinanceManager.Shared.Dtos.Accounts.AccountDto>());
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "savings-plans")
                .Add(p => p.Id, planId));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Category"));
            cut.FindAll("button").First(b => b.Id == "Category").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            var expectedBack = Uri.EscapeDataString($"card/savings-plans/{planId}");
            Assert.Contains($"card/savings-plans/categories/{categoryId}?back={expectedBack}", nav.Uri);
        }

        /// <summary>
        /// The budget purpose card's linked Category action must forward-navigate through the
        /// <c>OpenCategory</c> channel so the category card receives the purpose card as
        /// <c>?back=</c> target — the action must not be swallowed by the Back handler or broken
        /// by an existing <c>?back=</c> context.
        /// </summary>
        [Fact]
        public void BudgetPurposeCard_OpenCategory_AppendsPurposeCardAsBackTarget()
        {
            var apiMock = new Mock<IApiClient>();
            var purposeId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            apiMock.Setup(a => a.Budgets_GetPurposeAsync(purposeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BudgetPurposeDto(purposeId, Guid.NewGuid(), "Purpose", null,
                    BudgetSourceType.Contact, Guid.NewGuid(), categoryId));
            apiMock.Setup(a => a.Budgets_ListRulesByPurposeAsync(purposeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<BudgetRuleDto>());
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "budget")
                .Add(p => p.SubKind, "purposes")
                .Add(p => p.Id, purposeId));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Category"));
            cut.FindAll("button").First(b => b.Id == "Category").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            var expectedBack = Uri.EscapeDataString($"card/budget/purposes/{purposeId}");
            Assert.Contains($"card/budget/categories/{categoryId}?back={expectedBack}", nav.Uri);
        }

        /// <summary>
        /// A budget rule without purpose and category context (reachable only via direct URL) must
        /// fall back to the existing purposes list <c>list/budget/purposes</c> — the generic
        /// <c>list/budget/rules</c> route does not exist and would hang on loading.
        /// </summary>
        [Fact]
        public void BudgetRuleCard_Back_WithoutContext_NavigatesToPurposesList()
        {
            var apiMock = new Mock<IApiClient>();
            var ruleId = Guid.NewGuid();
            apiMock.Setup(a => a.Budgets_GetRuleAsync(ruleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BudgetRuleDto(ruleId, Guid.NewGuid(), null, null, 25m,
                    BudgetIntervalType.Monthly, null, DateOnly.FromDateTime(DateTime.Today), null));
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "budget")
                .Add(p => p.SubKind, "rules")
                .Add(p => p.Id, ruleId));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Back"));
            cut.FindAll("button").First(b => b.Id == "Back").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith("list/budget/purposes", nav.Uri);
        }

        /// <summary>
        /// For a new, unsaved savings plan (<c>Id == Guid.Empty</c>) there is no stable URL to return
        /// to, so <c>OpenCategory</c> must navigate to the category card without appending a
        /// <c>?back=</c> parameter pointing at the empty id.
        /// </summary>
        [Fact]
        public void SavingsPlanCard_OpenCategory_OnUnsavedCard_NavigatesWithoutBackTarget()
        {
            var apiMock = new Mock<IApiClient>();
            var categoryId = Guid.NewGuid();
            apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new SavingsPlanCategoryDto { Id = categoryId, Name = "Cat" } });
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "savings-plans")
                .Add(p => p.Id, Guid.Empty));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Category"));

            // select a category through the view model's lookup validation so the ribbon action
            // becomes enabled (the lookup commits to Model.CategoryId immediately)
            var vm = cut.FindComponent<FinanceManager.Web.Components.Shared.Ribbon<FinanceManager.Web.Components.Pages.CardPage.TabId>>()
                .Instance.Provider as FinanceManager.Web.ViewModels.SavingsPlans.SavingsPlanCardViewModel;
            Assert.NotNull(vm);
            Assert.NotNull(vm!.CardRecord);
            var categoryField = vm.CardRecord.Fields.First(f => f.LabelKey == "Card_Caption_SavingsPlan_Category");
            vm.ValidateLookupField(categoryField, new FinanceManager.Web.ViewModels.Common.BaseViewModel.LookupItem(categoryId, "Cat"));

            cut.WaitForState(() => !cut.FindAll("button").First(b => b.Id == "Category").HasAttribute("disabled"));
            cut.FindAll("button").First(b => b.Id == "Category").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith($"card/savings-plans/categories/{categoryId}", nav.Uri);
        }

        /// <summary>
        /// Regression guard: the comma-separated payload convention <c>card,{draftId}</c> from the
        /// statement-draft entry card must keep navigating to the typed card route
        /// <c>card/statement-drafts/{draftId}</c> (the parent draft card).
        /// </summary>
        [Fact]
        public void StatementDraftEntryCard_Back_NavigatesToParentDraftCard()
        {
            var apiMock = new Mock<IApiClient>();
            var draftId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var entry = new StatementDraftEntryDto(entryId, 1, DateTime.Today, null, 42m, "EUR",
                "subject", null, null, false, false, StatementDraftEntryStatus.Open,
                null, null, false, null, null, null, null, null, null);
            apiMock.Setup(a => a.StatementDrafts_GetEntryAsync(draftId, entryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StatementDraftEntryDetailDto(draftId, "file.pdf", entry, null, null, null, null, null, null));
            apiMock.Setup(a => a.StatementDrafts_GetAsync(draftId, true, null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((StatementDraftDetailDto?)null);
            RegisterServices(apiMock);

            Services.GetRequiredService<NavigationManager>()
                .NavigateTo($"card/statement-drafts/entries/{entryId}?draftId={draftId}");

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "statement-drafts")
                .Add(p => p.SubKind, "entries")
                .Add(p => p.Id, entryId));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Back"));
            cut.FindAll("button").First(b => b.Id == "Back").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith($"card/statement-drafts/{draftId}", nav.Uri);
        }

        /// <summary>
        /// Regression guard: a Back action without any payload (contact card) must keep the legacy
        /// behaviour of navigating to the generic list route <c>list/{kind}</c>.
        /// </summary>
        [Fact]
        public void ContactCard_Back_WithoutPayload_NavigatesToListRoute()
        {
            var apiMock = new Mock<IApiClient>();
            var id = Guid.NewGuid();
            apiMock.Setup(a => a.Contacts_GetAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactDto(id, "Name", ContactType.Person, null, "desc", false, null));
            apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ContactCategoryDto>());
            RegisterServices(apiMock);

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "contacts")
                .Add(p => p.Id, id));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Back"));
            cut.FindAll("button").First(b => b.Id == "Back").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            Assert.EndsWith("list/contacts", nav.Uri);
        }

        /// <summary>
        /// When the savings-plan card itself was reached via a <c>?back=</c> parameter, the
        /// <c>OpenCategory</c> action must nest that chain: the category card's <c>back</c> target is
        /// the plan card URL including its own <c>back</c> parameter, so the return chain keeps
        /// walking back to the original list.
        /// </summary>
        [Fact]
        public void SavingsPlanCard_OpenCategory_NestsExistingBackParameter()
        {
            var apiMock = new Mock<IApiClient>();
            var planId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            apiMock.Setup(a => a.SavingsPlans_GetAsync(planId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SavingsPlanDto(planId, "Plan A", SavingsPlanType.OneTime, null, null,
                    null, true, DateTime.UtcNow, null, categoryId));
            apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new SavingsPlanCategoryDto { Id = categoryId, Name = "Cat" } });
            apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(planId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("no analysis"));
            apiMock.Setup(a => a.SavingsPlans_GetLinkedAccountsAsync(planId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            apiMock.Setup(a => a.GetAccountsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<FinanceManager.Shared.Dtos.Accounts.AccountDto>());
            RegisterServices(apiMock);

            Services.GetRequiredService<NavigationManager>()
                .NavigateTo($"card/savings-plans/{planId}?back=" + Uri.EscapeDataString("list/savings-plans"));

            var cut = Render<FinanceManager.Web.Components.Pages.CardPage>(parameters => parameters
                .Add(p => p.Kind, "savings-plans")
                .Add(p => p.Id, planId));

            cut.WaitForState(() => cut.FindAll("button").Any(b => b.Id == "Category"));
            cut.FindAll("button").First(b => b.Id == "Category").Click();

            var nav = Services.GetRequiredService<NavigationManager>();
            var nestedCardUrl = $"card/savings-plans/{planId}?back={Uri.EscapeDataString("list/savings-plans")}";
            Assert.Contains($"card/savings-plans/categories/{categoryId}?back={Uri.EscapeDataString(nestedCardUrl)}", nav.Uri);
        }

        private static Mock<IApiClient> CreateMcpAuditApiMock()
        {
            var apiMock = new Mock<IApiClient>();
            apiMock.Setup(a => a.Mcp_GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new McpStatusDto(true));
            apiMock.Setup(a => a.Mcp_ListAccessesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<McpAccessListItemDto>());
            apiMock.Setup(a => a.Mcp_ListAuditEntriesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<short?>(),
                    It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<McpAuditEntryListItemDto>());
            return apiMock;
        }

        private void RegisterServices(Mock<IApiClient> apiMock)
        {
            Services.AddSingleton(apiMock.Object);
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
            Services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = false });
        }

        /// <summary>
        /// Fake <see cref="HttpMessageHandler"/> answering every request with an empty JSON array so
        /// embedded chart widgets (e.g. the savings-plan aggregates chart) resolve instantly.
        /// </summary>
        private sealed class EmptyChartHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }

        private sealed class TestCurrentUserService : ICurrentUserService
        {
            public Guid UserId { get; set; } = Guid.NewGuid();
            public string? PreferredLanguage { get; set; }
            public bool IsAuthenticated { get; set; }
            public bool IsAdmin { get; set; }
        }
    }
}
