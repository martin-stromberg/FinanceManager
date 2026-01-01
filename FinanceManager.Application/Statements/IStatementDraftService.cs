using FinanceManager.Domain.Statements;

namespace FinanceManager.Application.Statements;

/// <summary>
/// Service to create and manage statement import drafts and their entries.
/// Provides operations to create drafts from uploaded files, query and modify drafts and entries,
/// validate and book drafts into the system.
/// </summary>
public interface IStatementDraftService
{
    /// <summary>
    /// Creates a new statement draft from the supplied file bytes and yields progress via an async stream
    /// of created <see cref="StatementDraftDto"/> instances. Implementations may yield intermediate
    /// drafts for preview or when processing multiple contained statements.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner user for whom the draft is created.</param>
    /// <param name="originalFileName">Original file name of the uploaded statement (used for logging and display).</param>
    /// <param name="fileBytes">Raw file bytes to import. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous stream of <see cref="StatementDraftDto"/> representing created/parsed drafts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileBytes"/> is <c>null</c>.</exception>
    IAsyncEnumerable<StatementDraftDto> CreateDraftAsync(Guid ownerUserId, string originalFileName, byte[] fileBytes, CancellationToken ct);

    /// <summary>
    /// Gets a draft by id or <c>null</c> when not found.
    /// </summary>
    /// <param name="draftId">Identifier of the draft to retrieve.</param>
    /// <param name="ownerUserId">Owner user identifier used to scope the draft lookup.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="StatementDraftDto"/> when found; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> GetDraftAsync(Guid draftId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Gets draft header information (metadata only) by draft id or <c>null</c> when not found.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="ownerUserId">Owner user identifier used to scope the draft lookup.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The draft header DTO when found; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> GetDraftHeaderAsync(Guid draftId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Finds a draft header that contains the specified entry id.
    /// </summary>
    /// <param name="entryId">Identifier of the draft entry used to locate its parent draft.</param>
    /// <param name="ownerUserId">Owner user identifier used to scope the lookup.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The draft header DTO when found; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> FindDraftHeaderAsync(Guid entryId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Returns entries for the specified draft.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of <see cref="StatementDraftEntryDto"/> for the draft (may be empty).</returns>
    Task<IEnumerable<StatementDraftEntryDto>> GetDraftEntriesAsync(Guid draftId, CancellationToken ct);

    /// <summary>
    /// Gets a specific draft entry by draft and entry id.
    /// </summary>
    /// <param name="draftId">Identifier of the draft containing the entry.</param>
    /// <param name="entryId">Identifier of the entry to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entry DTO when found; otherwise <c>null</c>.</returns>
    Task<StatementDraftEntryDto?> GetDraftEntryAsync(Guid draftId, Guid entryId, CancellationToken ct);

    /// <summary>
    /// Returns a list of open drafts for the owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of open draft DTOs.</returns>
    Task<IReadOnlyList<StatementDraftDto>> GetOpenDraftsAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Returns a paged list of open drafts with skip/take for pagination.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of open draft DTOs for the requested page.</returns>
    Task<IReadOnlyList<StatementDraftDto>> GetOpenDraftsAsync(Guid ownerUserId, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Returns the count of open drafts for a user.
    /// </summary>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The number of open drafts for the user.</returns>
    Task<int> GetOpenDraftsCountAsync(Guid userId, CancellationToken token);

    /// <summary>
    /// Adds a draft entry with minimal required data and returns the updated draft DTO when successful.
    /// </summary>
    /// <param name="draftId">Identifier of the draft to add the entry to.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="bookingDate">Booking date of the new entry.</param>
    /// <param name="amount">Amount of the new entry.</param>
    /// <param name="subject">Subject/description of the new entry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when the entry was added; otherwise <c>null</c> when the draft was not found.</returns>
    Task<StatementDraftDto?> AddEntryAsync(Guid draftId, Guid ownerUserId, DateTime bookingDate, decimal amount, string subject, CancellationToken ct);

    /// <summary>
    /// Commits a draft into the system for a specific account using the provided import format.
    /// </summary>
    /// <param name="draftId">Identifier of the draft to commit.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="accountId">Target account identifier for the commit.</param>
    /// <param name="format">Import format to use for booking.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CommitResult"/> describing the outcome when successful; otherwise <c>null</c> if commit could not be prepared.</returns>
    Task<CommitResult?> CommitAsync(Guid draftId, Guid ownerUserId, Guid accountId, ImportFormat format, CancellationToken ct);

    /// <summary>
    /// Cancels an existing draft and removes any associated temporary data.
    /// </summary>
    /// <param name="draftId">Identifier of the draft to cancel.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the draft was found and cancelled; otherwise <c>false</c>.</returns>
    Task<bool> CancelAsync(Guid draftId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Classifies a draft or a single entry using configured matching rules and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Optional draft id to limit classification to a specific draft.</param>
    /// <param name="entryId">Optional entry id to limit classification to a single entry.</param>
    /// <param name="ownerUserId">Owner user identifier used to scope lookups.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when classification changed state; otherwise <c>null</c> when not found.</returns>
    Task<StatementDraftDto?> ClassifyAsync(Guid? draftId, Guid? entryId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Sets the account for a draft and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="accountId">Account identifier to set on the draft.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> SetAccountAsync(Guid draftId, Guid ownerUserId, Guid accountId, CancellationToken ct);

    /// <summary>
    /// Sets a human description for a draft and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="description">Optional human readable description to attach to the draft.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> SetDescriptionAsync(Guid draftId, Guid ownerUserId, string? description, CancellationToken ct);

    /// <summary>
    /// Creates an empty draft (without file) for the owner and returns the created draft DTO.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="originalFileName">Optional original file name used as label for the draft.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created draft DTO when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> CreateEmptyDraftAsync(Guid ownerUserId, string originalFileName, CancellationToken ct);

    /// <summary>
    /// Sets the contact for a specific draft entry and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft containing the entry.</param>
    /// <param name="entryId">Identifier of the entry to update.</param>
    /// <param name="contactId">Contact identifier to assign to the entry (or <c>null</c> to clear).</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> SetEntryContactAsync(Guid draftId, Guid entryId, Guid? contactId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Sets whether an entry is cost-neutral and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="entryId">Identifier of the entry to update.</param>
    /// <param name="isCostNeutral">Flag indicating whether the entry is cost-neutral; <c>null</c> means unchanged.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> SetEntryCostNeutralAsync(Guid draftId, Guid entryId, bool? isCostNeutral, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Assigns a savings plan to a draft entry and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft containing the entry.</param>
    /// <param name="entryId">Identifier of the entry to update.</param>
    /// <param name="savingsPlanId">Savings plan identifier to assign, or <c>null</c> to clear.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO reflecting the assigned savings plan.</returns>
    Task<StatementDraftDto> AssignSavingsPlanAsync(Guid draftId, Guid entryId, Guid? savingsPlanId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Sets the split draft id for an entry (used for splitting amounts across groups) and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <param name="splitDraftId">Identifier of the split draft to assign, or <c>null</c> to clear.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> SetEntrySplitDraftAsync(Guid draftId, Guid entryId, Guid? splitDraftId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Updates core fields for an entry and returns the updated entry DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft containing the entry.</param>
    /// <param name="entryId">Identifier of the entry to update.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="bookingDate">New booking date for the entry.</param>
    /// <param name="valutaDate">Optional valuta date for the entry.</param>
    /// <param name="amount">New amount.</param>
    /// <param name="subject">New subject/description.</param>
    /// <param name="recipientName">Optional recipient name.</param>
    /// <param name="currencyCode">Optional currency code.</param>
    /// <param name="bookingDescription">Optional booking description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="StatementDraftEntryDto"/> when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftEntryDto?> UpdateEntryCoreAsync(Guid draftId, Guid entryId, Guid ownerUserId, DateTime bookingDate, DateTime? valutaDate, decimal amount, string subject, string? recipientName, string? currencyCode, string? bookingDescription, CancellationToken ct);

    /// <summary>
    /// Sets a flag whether the linked savings plan should be archived on booking and returns the updated draft DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <param name="archive">Flag indicating whether to archive the linked savings plan when the entry is booked.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated draft DTO when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftDto?> SetEntryArchiveSavingsPlanOnBookingAsync(Guid draftId, Guid entryId, bool archive, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Sets security related fields for an entry (security id, transaction type, quantities/prices) and returns the draft entity.
    /// Note: this operation returns the domain draft when successful to allow further domain-level operations by the caller.
    /// </summary>
    /// <param name="draftId">Identifier of the draft containing the entry.</param>
    /// <param name="entryId">Identifier of the entry to update.</param>
    /// <param name="securityId">Security identifier to assign, or <c>null</c> to clear.</param>
    /// <param name="txType">Optional security transaction type (buy/sell/etc.).</param>
    /// <param name="quantity">Optional security quantity.</param>
    /// <param name="price">Optional security price (per unit).</param>
    /// <param name="fee">Optional fee amount for the security transaction.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The domain <see cref="StatementDraft"/> when the operation succeeded; otherwise <c>null</c>.</returns>
    Task<StatementDraft?> SetEntrySecurityAsync(Guid draftId, Guid entryId, Guid? securityId, SecurityTransactionType? txType, decimal? quantity, decimal? price, decimal? fee, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Validates the draft (or a single entry) and returns a <see cref="DraftValidationResultDto"/> describing warnings and errors.
    /// </summary>
    /// <param name="draftId">Identifier of the draft to validate.</param>
    /// <param name="entryId">Optional entry id to validate a single entry.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="DraftValidationResultDto"/> containing validation messages and state.</returns>
    Task<DraftValidationResultDto> ValidateAsync(Guid draftId, Guid? entryId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Books a draft or single entry into posted statement entries. Returns booking result which includes created postings and any warnings/errors.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="entryId">Optional entry id to book a single entry.</param>
    /// <param name="ownerUserId">Owner user identifier performing the booking.</param>
    /// <param name="forceWarnings">When <c>true</c> warnings are ignored and booking proceeds if possible.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BookingResult"/> describing success, created postings and any issues encountered.</returns>
    Task<BookingResult> BookAsync(Guid draftId, Guid? entryId, Guid ownerUserId, bool forceWarnings, CancellationToken ct);

    /// <summary>
    /// Saves all entry properties including optional linking to savings plans and securities and returns the updated entry DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft.</param>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="splitDraftId">Optional split draft id to assign.</param>
    /// <param name="isCostNeutral">Optional cost-neutral flag.</param>
    /// <param name="contactId">Optional contact id to assign.</param>
    /// <param name="archiveSavingsPlanOnBooking">Optional flag whether to archive linked savings plan on booking.</param>
    /// <param name="savingsPlanId">Optional savings plan id to assign.</param>
    /// <param name="txType">Optional security transaction type.</param>
    /// <param name="quantity">Optional quantity for securities.</param>
    /// <param name="price">Optional price for securities.</param>
    /// <param name="fee">Optional fee for securities.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="StatementDraftEntryDto"/> when saved; otherwise <c>null</c>.</returns>
    Task<StatementDraftEntryDto?> SaveEntryAllAsync(Guid draftId, Guid entryId, Guid ownerUserId, Guid? splitDraftId, bool? isCostNeutral, Guid? contactId, bool? archiveSavingsPlanOnBooking, Guid? savingsPlanId, SecurityTransactionType? txType, decimal? quantity, decimal? price, decimal? fee, CancellationToken ct);

    /// <summary>
    /// Adds additional statement details such as original file attachment to the draft.
    /// </summary>
    /// <param name="id">Identifier of the draft to update.</param>
    /// <param name="fileName">Original file name of the attachment.</param>
    /// <param name="fileData">Raw bytes of the file to attach.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the details have been persisted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileData"/> is <c>null</c>.</exception>
    Task AddStatementDetailsAsync(Guid id, string fileName, byte[] fileData, CancellationToken ct);

    /// <summary>
    /// Deletes a single draft entry.
    /// </summary>
    /// <param name="draftId">Identifier of the draft containing the entry.</param>
    /// <param name="entryId">Identifier of the entry to delete.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the entry was found and deleted; otherwise <c>false</c>.</returns>
    Task<bool> DeleteEntryAsync(Guid draftId, Guid entryId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Deletes all drafts for the owner and returns the number of deleted drafts.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of drafts that were deleted.</returns>
    Task<int> DeleteAllAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Returns the sum of amounts for a split group identified by <paramref name="splitDraftId"/>.
    /// </summary>
    /// <param name="splitDraftId">Identifier of the split draft group.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sum of group amounts or <c>null</c> when the group has no entries.</returns>
    Task<decimal?> GetSplitGroupSumAsync(Guid splitDraftId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Returns neighbor ids for the upload group (previous, next) to navigate uploads within the same upload group.
    /// </summary>
    /// <param name="draftId">Identifier of the draft to locate neighbors for.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing previous and next draft ids (each may be <c>null</c>).</returns>
    Task<(Guid? prevId, Guid? nextId)> GetUploadGroupNeighborsAsync(Guid draftId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Resets duplicate detection state for an entry and returns the updated entry DTO.
    /// </summary>
    /// <param name="draftId">Identifier of the draft containing the entry.</param>
    /// <param name="entryId">Identifier of the entry to reset.</param>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="StatementDraftEntryDto"/> when successful; otherwise <c>null</c>.</returns>
    Task<StatementDraftEntryDto?> ResetDuplicateEntryAsync(Guid draftId, Guid entryId, Guid ownerUserId, CancellationToken ct);
}



