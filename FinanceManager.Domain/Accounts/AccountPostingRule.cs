namespace FinanceManager.Domain.Accounts;

/// <summary>
/// Represents a posting rule (Kontierungsregel) associated with a bank account.
/// A rule assigns a posting type when a configured text field contains a configured pattern.
/// </summary>
public sealed class AccountPostingRule : Entity
{
    /// <summary>
    /// Creates a new posting rule for the specified account.
    /// </summary>
    /// <param name="accountId">Identifier of the owning account.</param>
    /// <param name="field">The text field the rule matches against.</param>
    /// <param name="pattern">The text that must be contained in the selected field.</param>
    /// <param name="postingType">The posting type assigned when the rule matches.</param>
    /// <param name="orderIndex">Evaluation order; the lowest matching rule wins.</param>
    public AccountPostingRule(Guid accountId, PostingRuleField field, string pattern, BankPostingType postingType, int orderIndex)
    {
        AccountId = Guards.NotEmpty(accountId, nameof(accountId));
        Field = field;
        Pattern = Guards.NotNullOrWhiteSpace(pattern, nameof(pattern)).Trim();
        PostingType = postingType;
        OrderIndex = orderIndex;
    }

    /// <summary>
    /// Identifier of the account that owns this posting rule.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// The text field the rule matches against (subject or booking description).
    /// </summary>
    public PostingRuleField Field { get; private set; }

    /// <summary>
    /// The text that must be contained in the selected field for the rule to match.
    /// </summary>
    public string Pattern { get; private set; } = null!;

    /// <summary>
    /// The posting type assigned when the rule matches.
    /// </summary>
    public BankPostingType PostingType { get; private set; }

    /// <summary>
    /// Evaluation order; the matching rule with the lowest index wins.
    /// </summary>
    public int OrderIndex { get; private set; }

    /// <summary>
    /// Navigation property back to the owning account.
    /// </summary>
    public Account? Account { get; private set; }

    /// <summary>
    /// Updates the rule definition.
    /// </summary>
    /// <param name="field">The text field the rule matches against.</param>
    /// <param name="pattern">The text that must be contained in the selected field.</param>
    /// <param name="postingType">The posting type assigned when the rule matches.</param>
    /// <param name="orderIndex">Evaluation order; the lowest matching rule wins.</param>
    public void Update(PostingRuleField field, string pattern, BankPostingType postingType, int orderIndex)
    {
        Field = field;
        Pattern = Guards.NotNullOrWhiteSpace(pattern, nameof(pattern)).Trim();
        PostingType = postingType;
        OrderIndex = orderIndex;
        Touch();
    }

    // Backup DTO
    /// <summary>
    /// Backup DTO for <see cref="AccountPostingRule"/>.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="AccountId">Identifier of the owning account.</param>
    /// <param name="Field">The text field the rule matches against.</param>
    /// <param name="Pattern">The text that must be contained in the selected field.</param>
    /// <param name="PostingType">The posting type assigned when the rule matches.</param>
    /// <param name="OrderIndex">Evaluation order; the lowest matching rule wins.</param>
    /// <returns>The result.</returns>
    public sealed record AccountPostingRuleBackupDto(Guid Id, Guid AccountId, PostingRuleField Field, string Pattern, BankPostingType PostingType, int OrderIndex);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this posting rule.
    /// </summary>
    /// <returns>An <see cref="AccountPostingRuleBackupDto"/> containing values required for backup/restore.</returns>
    public AccountPostingRuleBackupDto ToBackupDto()
        => new AccountPostingRuleBackupDto(Id, AccountId, Field, Pattern, PostingType, OrderIndex);

    /// <summary>
    /// Assigns values from a backup DTO to this entity instance.
    /// </summary>
    /// <param name="dto">The <see cref="AccountPostingRuleBackupDto"/> containing values to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the pattern of <paramref name="dto"/> is <c>null</c> or empty.</exception>
    public void AssignBackupDto(AccountPostingRuleBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        AccountId = dto.AccountId;
        Field = dto.Field;
        Pattern = dto.Pattern?.Trim() ?? throw new ArgumentException("Backup DTO must provide a pattern.", nameof(dto));
        PostingType = dto.PostingType;
        OrderIndex = dto.OrderIndex;
    }
}
