using FinanceManager.Domain.Accounts;

namespace FinanceManager.Domain.Savings;

/// <summary>
/// Represents an explicit many-to-many link between a <see cref="SavingsPlan"/> and an
/// <see cref="Account"/>. A savings plan can be linked to several accounts and an account can
/// serve several savings plans (e.g. a collection account). The pair
/// (<see cref="SavingsPlanId"/>, <see cref="AccountId"/>) is unique and links are only allowed
/// between a plan and an account of the same owner.
/// </summary>
public sealed class SavingsPlanAccountLink : Entity
{
    /// <summary>
    /// Creates a new link between a savings plan and an account owned by the same user.
    /// </summary>
    /// <param name="savingsPlanId">Identifier of the linked savings plan.</param>
    /// <param name="accountId">Identifier of the linked account.</param>
    /// <param name="ownerUserId">Identifier of the user owning both the plan and the account.</param>
    /// <exception cref="ArgumentException">Thrown when any identifier is <see cref="Guid.Empty"/>.</exception>
    public SavingsPlanAccountLink(Guid savingsPlanId, Guid accountId, Guid ownerUserId)
    {
        SavingsPlanId = Guards.NotEmpty(savingsPlanId, nameof(savingsPlanId));
        AccountId = Guards.NotEmpty(accountId, nameof(accountId));
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
    }

    /// <summary>
    /// Identifier of the linked savings plan.
    /// </summary>
    public Guid SavingsPlanId { get; private set; }

    /// <summary>
    /// Identifier of the linked account.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// Identifier of the user owning both the savings plan and the account. Stored redundantly to
    /// simplify owner checks, indices and backup collection.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Navigation property to the linked savings plan.
    /// </summary>
    public SavingsPlan? SavingsPlan { get; private set; }

    /// <summary>
    /// Navigation property to the linked account.
    /// </summary>
    public Account? Account { get; private set; }

    /// <summary>
    /// Backup DTO for <see cref="SavingsPlanAccountLink"/>.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="SavingsPlanId">Identifier of the linked savings plan.</param>
    /// <param name="AccountId">Identifier of the linked account.</param>
    /// <param name="OwnerUserId">Identifier of the owning user.</param>
    /// <param name="CreatedUtc">Entity creation timestamp UTC.</param>
    /// <param name="ModifiedUtc">Entity last modified timestamp UTC, if any.</param>
    /// <returns>The result.</returns>
    public sealed record SavingsPlanAccountLinkBackupDto(Guid Id, Guid SavingsPlanId, Guid AccountId, Guid OwnerUserId, DateTime CreatedUtc, DateTime? ModifiedUtc);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this link.
    /// </summary>
    /// <returns>A <see cref="SavingsPlanAccountLinkBackupDto"/> containing values needed to restore the link.</returns>
    public SavingsPlanAccountLinkBackupDto ToBackupDto()
        => new SavingsPlanAccountLinkBackupDto(Id, SavingsPlanId, AccountId, OwnerUserId, CreatedUtc, ModifiedUtc);

    /// <summary>
    /// Applies values from a backup DTO to this link instance.
    /// </summary>
    /// <param name="dto">The backup DTO to apply to this entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(SavingsPlanAccountLinkBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        SavingsPlanId = dto.SavingsPlanId;
        AccountId = dto.AccountId;
        OwnerUserId = dto.OwnerUserId;
        SetDates(dto.CreatedUtc, dto.ModifiedUtc);
    }
}
