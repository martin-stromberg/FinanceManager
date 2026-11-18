using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Postings;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Tests.TestHelpers;

namespace FinanceManager.Tests.Postings;

/// <summary>
/// Tests for <see cref="PostingTypeEvaluator"/>: rule matching, normalization, ordering and sign fallback.
/// </summary>
public sealed class PostingTypeEvaluatorTests
{
    private static AccountPostingRule Rule(PostingRuleField field, string pattern, BankPostingType type, int orderIndex, Guid? id = null)
    {
        var rule = new AccountPostingRule(Guid.NewGuid(), field, pattern, type, orderIndex);
        if (id.HasValue) { TestEntityHelper.SetEntityId(rule, id.Value); }
        return rule;
    }

    /// <summary>Without rules a negative amount classifies as outflow.</summary>
    [Fact]
    public void Evaluate_NoRules_NegativeAmount_ReturnsOutflow()
    {
        var result = PostingTypeEvaluator.Evaluate(new List<AccountPostingRule>(), "Anything", null, -10m);
        Assert.Equal(BankPostingType.Outflow, result);
    }

    /// <summary>Without rules a zero amount classifies as inflow (zero counts as positive).</summary>
    [Fact]
    public void Evaluate_NoRules_ZeroAmount_ReturnsInflow()
    {
        var result = PostingTypeEvaluator.Evaluate(new List<AccountPostingRule>(), "Anything", null, 0m);
        Assert.Equal(BankPostingType.Inflow, result);
    }

    /// <summary>Without rules a positive amount classifies as inflow.</summary>
    [Fact]
    public void Evaluate_NoRules_PositiveAmount_ReturnsInflow()
    {
        var result = PostingTypeEvaluator.Evaluate(new List<AccountPostingRule>(), "Anything", null, 42.5m);
        Assert.Equal(BankPostingType.Inflow, result);
    }

    /// <summary>A subject rule matches text contained in the subject.</summary>
    [Fact]
    public void Evaluate_SubjectRule_MatchesSubjectOnly()
    {
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.Subject, "Zins", BankPostingType.Interest, 1)
        };

        var result = PostingTypeEvaluator.Evaluate(rules, "Zinsgutschrift 2025", "Gutschrift", 5m);
        Assert.Equal(BankPostingType.Interest, result);
    }

    /// <summary>A booking-description rule matches the description field.</summary>
    [Fact]
    public void Evaluate_BookingDescriptionRule_MatchesDescriptionNotSubject()
    {
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.BookingDescription, "Steuer", BankPostingType.Tax, 1)
        };

        var result = PostingTypeEvaluator.Evaluate(rules, "Zinsgutschrift", "Kapitalertragssteuer", -3m);
        Assert.Equal(BankPostingType.Tax, result);
    }

    /// <summary>A subject rule does not match text that only appears in the booking description.</summary>
    [Fact]
    public void Evaluate_SubjectRule_DoesNotMatchDescription()
    {
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.Subject, "Kapitalertragssteuer", BankPostingType.Tax, 1)
        };

        // Pattern only present in the booking description -> subject rule must not match -> sign fallback.
        var result = PostingTypeEvaluator.Evaluate(rules, "Zinsgutschrift", "Kapitalertragssteuer", -3m);
        Assert.Equal(BankPostingType.Outflow, result);
    }

    /// <summary>Matching is insensitive to case, whitespace and umlaut spellings on both sides.</summary>
    [Fact]
    public void Evaluate_Normalization_IgnoresCaseWhitespaceAndUmlauts()
    {
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.Subject, "Jahresüberschuss", BankPostingType.Interest, 1)
        };

        // Subject contains umlaut written as "ue" and additional whitespace/casing differences.
        var result = PostingTypeEvaluator.Evaluate(rules, "  JAHRESUEBERSCHUSS  2025 ", null, 12m);
        Assert.Equal(BankPostingType.Interest, result);
    }

    /// <summary>An ASCII pattern still matches when the subject contains the umlaut.</summary>
    [Fact]
    public void Evaluate_Normalization_MatchesUmlautInSubjectAgainstAsciiPattern()
    {
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.Subject, "fuer", BankPostingType.Tax, 1)
        };

        var result = PostingTypeEvaluator.Evaluate(rules, "Abzug für Steuer", null, -1m);
        Assert.Equal(BankPostingType.Tax, result);
    }

    /// <summary>When several rules match, the one with the lowest order index wins.</summary>
    [Fact]
    public void Evaluate_LowerOrderIndexWins()
    {
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.Subject, "Zins", BankPostingType.Tax, 5),
            Rule(PostingRuleField.Subject, "Zins", BankPostingType.Interest, 1)
        };

        var result = PostingTypeEvaluator.Evaluate(rules, "Zinsgutschrift", null, 5m);
        Assert.Equal(BankPostingType.Interest, result);
    }

    /// <summary>Rules sharing an order index are tie-broken by entity id.</summary>
    [Fact]
    public void Evaluate_SameOrderIndex_LowerIdWins()
    {
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.Subject, "Zins", BankPostingType.Tax, 1, highId),
            Rule(PostingRuleField.Subject, "Zins", BankPostingType.Interest, 1, lowId)
        };

        var result = PostingTypeEvaluator.Evaluate(rules, "Zinsgutschrift", null, 5m);
        Assert.Equal(BankPostingType.Interest, result);
    }

    /// <summary>Rules whose pattern normalizes to an empty string are skipped.</summary>
    [Fact]
    public void Evaluate_EmptyNormalizedPattern_IsIgnored()
    {
        var rule = Rule(PostingRuleField.Subject, "x", BankPostingType.Tax, 1);
        // The domain guard prevents whitespace-only patterns on the ctor; a legacy row could still
        // carry one, so simulate it directly to cover the evaluator's defensive skip.
        typeof(AccountPostingRule).GetProperty(nameof(AccountPostingRule.Pattern))!.SetValue(rule, "   ");
        var rules = new List<AccountPostingRule> { rule };

        var result = PostingTypeEvaluator.Evaluate(rules, "Zinsgutschrift", null, -5m);
        Assert.Equal(BankPostingType.Outflow, result);
    }

    /// <summary>Missing texts cannot match rules, so the sign fallback applies.</summary>
    [Fact]
    public void Evaluate_NullSubjectAndDescription_FallsBackToSign()
    {
        var rules = new List<AccountPostingRule>
        {
            Rule(PostingRuleField.Subject, "Zins", BankPostingType.Interest, 1)
        };

        var result = PostingTypeEvaluator.Evaluate(rules, null, null, -5m);
        Assert.Equal(BankPostingType.Outflow, result);
    }

    /// <summary>Umlaut normalization replaces lower and upper case umlauts and handles null.</summary>
    [Fact]
    public void NormalizeUmlauts_ReplacesGermanCharacters()
    {
        Assert.Equal("aeoeuess", PostingTypeEvaluator.NormalizeUmlauts("äöüß"));
        Assert.Equal("aeoeue", PostingTypeEvaluator.NormalizeUmlauts("ÄÖÜ"));
        Assert.Equal(string.Empty, PostingTypeEvaluator.NormalizeUmlauts(null!));
    }

    /// <summary>The sign helper classifies negative amounts as outflow, zero/positive as inflow.</summary>
    /// <param name="amount">The amount to classify.</param>
    /// <param name="expected">The expected posting type.</param>
    [Theory]
    [InlineData(-0.01, BankPostingType.Outflow)]
    [InlineData(-100, BankPostingType.Outflow)]
    [InlineData(0, BankPostingType.Inflow)]
    [InlineData(42.5, BankPostingType.Inflow)]
    public void EvaluateByAmountSign_ClassifiesBySign(decimal amount, BankPostingType expected)
    {
        Assert.Equal(expected, PostingTypeEvaluator.EvaluateByAmountSign(amount));
    }

    /// <summary><see cref="PostingTypeEvaluator.Resolve"/> keeps specified values and falls back by sign otherwise.</summary>
    [Fact]
    public void Resolve_KeepsSpecifiedType_AndFallsBackOnUnspecified()
    {
        Assert.Equal(BankPostingType.Tax, PostingTypeEvaluator.Resolve(BankPostingType.Tax, -5m));
        Assert.Equal(BankPostingType.Interest, PostingTypeEvaluator.Resolve(BankPostingType.Interest, 5m));
        Assert.Equal(BankPostingType.Outflow, PostingTypeEvaluator.Resolve(BankPostingType.Unspecified, -5m));
        Assert.Equal(BankPostingType.Inflow, PostingTypeEvaluator.Resolve(BankPostingType.Unspecified, 5m));
    }
}
