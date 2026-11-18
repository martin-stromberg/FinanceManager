using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the account posting-rule endpoints (Issue #541): rule CRUD via REST,
/// validation of the pattern and posting type, owner isolation between users and enqueueing the
/// posting-type re-evaluation background task.
/// </summary>
public class ApiClientAccountPostingRulesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>Initializes the test with the shared in-memory web application factory.</summary>
    /// <param name="factory">The shared in-memory test host used to spin up API clients.</param>
    public ApiClientAccountPostingRulesTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private static async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    private static Task<AccountDto> CreateGiroAccountAsync(FinanceManager.Shared.ApiClient api, string name)
    {
        return api.CreateAccountAsync(
            new AccountCreateRequest(name, AccountType.Giro, null, null, $"Bank {name}", null, SavingsPlanExpectation.None),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Full rule life cycle: the list starts empty, a created rule is returned ordered, an upsert
    /// with an existing id updates the rule in place, and a delete removes it again.
    /// </summary>
    [Fact]
    public async Task PostingRules_Create_Update_List_Remove()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var account = await CreateGiroAccountAsync(api, $"Konto_{Guid.NewGuid():N}");

        var rules = await api.GetPostingRulesAsync(account.Id, TestContext.Current.CancellationToken);
        rules.Should().NotBeNull().And.BeEmpty();

        await api.UpsertPostingRuleAsync(account.Id,
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Zins", BankPostingType.Interest, 0),
            TestContext.Current.CancellationToken);
        await api.UpsertPostingRuleAsync(account.Id,
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.BookingDescription, "Kapitalertragssteuer", BankPostingType.Tax, 1),
            TestContext.Current.CancellationToken);

        rules = await api.GetPostingRulesAsync(account.Id, TestContext.Current.CancellationToken);
        rules.Should().HaveCount(2);
        rules[0].Field.Should().Be(PostingRuleField.Subject);
        rules[0].Pattern.Should().Be("Zins");
        rules[0].PostingType.Should().Be(BankPostingType.Interest);
        rules[0].OrderIndex.Should().Be(0);
        rules[1].Field.Should().Be(PostingRuleField.BookingDescription);
        rules[1].PostingType.Should().Be(BankPostingType.Tax);

        // Upsert with an existing id updates the rule in place.
        var ruleId = rules[0].Id;
        await api.UpsertPostingRuleAsync(account.Id,
            new AccountPostingRuleUpsertRequest(ruleId, PostingRuleField.BookingDescription, "Zinsertrag", BankPostingType.Inflow, 5),
            TestContext.Current.CancellationToken);

        rules = await api.GetPostingRulesAsync(account.Id, TestContext.Current.CancellationToken);
        rules.Should().HaveCount(2);
        var updated = rules.Should().ContainSingle(r => r.Id == ruleId).Which;
        updated.Field.Should().Be(PostingRuleField.BookingDescription);
        updated.Pattern.Should().Be("Zinsertrag");
        updated.PostingType.Should().Be(BankPostingType.Inflow);
        updated.OrderIndex.Should().Be(5);

        await api.RemovePostingRuleAsync(account.Id, ruleId, TestContext.Current.CancellationToken);
        rules = await api.GetPostingRulesAsync(account.Id, TestContext.Current.CancellationToken);
        rules.Should().ContainSingle().Which.Pattern.Should().Be("Kapitalertragssteuer");
    }

    /// <summary>
    /// Invalid payloads are rejected: an empty pattern fails validation and
    /// <see cref="BankPostingType.Unspecified"/> is rejected by the service.
    /// </summary>
    [Fact]
    public async Task PostingRules_Upsert_RejectsInvalidPayloads()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var account = await CreateGiroAccountAsync(api, $"Konto_{Guid.NewGuid():N}");

        var whitespace = () => api.UpsertPostingRuleAsync(account.Id,
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "   ", BankPostingType.Interest, 0),
            TestContext.Current.CancellationToken);
        await whitespace.Should().ThrowAsync<HttpRequestException>();
        api.LastError.Should().NotBeNullOrEmpty();

        var unspecified = () => api.UpsertPostingRuleAsync(account.Id,
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Zins", BankPostingType.Unspecified, 0),
            TestContext.Current.CancellationToken);
        await unspecified.Should().ThrowAsync<HttpRequestException>();
        api.LastError.Should().NotBeNullOrEmpty();

        var rules = await api.GetPostingRulesAsync(account.Id, TestContext.Current.CancellationToken);
        rules.Should().BeEmpty();
    }

    /// <summary>
    /// Rules of another user's account are not accessible: upserting and removing both fail,
    /// so no rule data leaks across owners.
    /// </summary>
    [Fact]
    public async Task PostingRules_ForeignAccount_IsRejected()
    {
        var apiOwner = CreateClient();
        await EnsureAuthenticatedAsync(apiOwner);
        var account = await CreateGiroAccountAsync(apiOwner, $"Konto_{Guid.NewGuid():N}");
        await apiOwner.UpsertPostingRuleAsync(account.Id,
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Zins", BankPostingType.Interest, 0),
            TestContext.Current.CancellationToken);
        var ownerRule = (await apiOwner.GetPostingRulesAsync(account.Id, TestContext.Current.CancellationToken)).Single();

        var apiOther = CreateClient();
        await EnsureAuthenticatedAsync(apiOther);

        var upsert = () => apiOther.UpsertPostingRuleAsync(account.Id,
            new AccountPostingRuleUpsertRequest(null, PostingRuleField.Subject, "Fremd", BankPostingType.Tax, 0),
            TestContext.Current.CancellationToken);
        await upsert.Should().ThrowAsync<HttpRequestException>();
        apiOther.LastError.Should().NotBeNullOrEmpty();

        var remove = () => apiOther.RemovePostingRuleAsync(account.Id, ownerRule.Id, TestContext.Current.CancellationToken);
        await remove.Should().ThrowAsync<HttpRequestException>();
        apiOther.LastError.Should().NotBeNullOrEmpty();

        // The owner's rule must be untouched.
        var rules = await apiOwner.GetPostingRulesAsync(account.Id, TestContext.Current.CancellationToken);
        rules.Should().ContainSingle().Which.Pattern.Should().Be("Zins");
    }

    /// <summary>
    /// The reclassify endpoint enqueues a <see cref="BackgroundTaskType.ReevaluatePostingTypes"/>
    /// task for the account; foreign accounts are rejected.
    /// </summary>
    [Fact]
    public async Task PostingTypes_Reclassify_EnqueuesBackgroundTask()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var account = await CreateGiroAccountAsync(api, $"Konto_{Guid.NewGuid():N}");

        var info = await api.EnqueuePostingTypeReevaluationAsync(account.Id, TestContext.Current.CancellationToken);
        info.Should().NotBeNull();
        info.Type.Should().Be(BackgroundTaskType.ReevaluatePostingTypes);
        info.Id.Should().NotBeEmpty();

        var apiOther = CreateClient();
        await EnsureAuthenticatedAsync(apiOther);
        var foreign = () => apiOther.EnqueuePostingTypeReevaluationAsync(account.Id, TestContext.Current.CancellationToken);
        await foreign.Should().ThrowAsync<HttpRequestException>();
        apiOther.LastError.Should().NotBeNullOrEmpty();
    }
}
