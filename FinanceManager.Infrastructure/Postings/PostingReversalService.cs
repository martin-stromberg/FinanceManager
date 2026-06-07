using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Postings;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Statements;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace FinanceManager.Infrastructure.Postings;

/// <summary>
/// Implementation of posting reversal service that handles creation of reversal postings,
/// validation, and transaction management.
/// </summary>
public sealed class PostingReversalService : IPostingReversalService
{
    private readonly AppDbContext _context;
    private readonly IPostingAggregateService _aggregateService;
    private readonly ILogger<PostingReversalService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostingReversalService"/> class.
    /// </summary>
    /// <param name="context">Database context.</param>
    /// <param name="aggregateService">Posting aggregate service for updating aggregates.</param>
    /// <param name="logger">Logger instance.</param>
    public PostingReversalService(
        AppDbContext context,
        IPostingAggregateService aggregateService,
        ILogger<PostingReversalService> logger)
    {
        _context = context;
        _aggregateService = aggregateService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ReversalResultDto> ReversePostingAsync(Guid postingId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} initiating reversal for posting {PostingId}", userId, postingId);

        // Pre-transaction validation
        var validation = await CanReverseAsync(postingId, userId, ct);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors);
            _logger.LogWarning("Reversal validation failed for posting {PostingId}: {Errors}", postingId, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        // Begin transaction with ReadCommitted isolation level
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // Load original posting with lock (within transaction)
            var original = await _context.Postings
                .FirstOrDefaultAsync(p => p.Id == postingId, ct);

            if (original == null || original.ReversedByPostingId.HasValue)
            {
                throw new InvalidOperationException($"Posting {postingId} not found or already reversed");
            }

            // Load all related postings by GroupId
            var relatedPostings = await GetRelatedPostingsAsync(postingId, ct);

            // Check for partially reversed group
            var allPostings = relatedPostings.Prepend(original).ToList();
            var anyAlreadyReversed = allPostings.Any(p => p.ReversedByPostingId.HasValue);
            if (anyAlreadyReversed)
            {
                throw new InvalidOperationException("Cannot reverse posting: group is partially reversed. All-or-nothing policy enforced.");
            }

            // Create reversal postings
            var reversals = new List<Posting>();
            var newGroupId = Guid.NewGuid(); // All reversals share new GroupId

            foreach (var posting in allPostings)
            {
                var reversal = CreateReversalPosting(posting, newGroupId);
                _context.Postings.Add(reversal);
                reversals.Add(reversal);

                // Mark original as reversed
                posting.SetReversedBy(reversal, userId);
            }

            await _context.SaveChangesAsync(ct);

            // Create StatementDraft with the original posting for reconciliation
            var statementDraft = await CreateReversalStatementDraftAsync(original, allPostings, userId, ct);

            // Update aggregates (only for reversal postings)
            foreach (var reversal in reversals)
            {
                await _aggregateService.UpsertForPostingAsync(reversal, ct);
            }

            await _context.SaveChangesAsync(ct);

            // Commit transaction
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Reversal completed: {ReversedCount} postings reversed, {CreatedCount} reversals created by user {UserId}",
                allPostings.Count,
                reversals.Count,
                userId);

            return new ReversalResultDto(
                allPostings.Select(p => p.Id).ToList(),
                reversals.Select(r => r.Id).ToList(),
                statementDraft.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reversal transaction failed for posting {PostingId}, rolling back", postingId);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ReversalValidationDto> CanReverseAsync(Guid postingId, Guid userId, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var posting = await _context.Postings.FirstOrDefaultAsync(p => p.Id == postingId, ct);

        if (posting == null)
        {
            errors.Add($"Posting {postingId} not found");
            return new ReversalValidationDto(false, errors);
        }

        // Check ownership
        var ownerUserId = await GetPostingOwnerUserIdAsync(posting, ct);
        if (ownerUserId != userId)
        {
            errors.Add($"User {userId} is not authorized to reverse posting {postingId}");
            return new ReversalValidationDto(false, errors);
        }

        // Check if already reversed
        if (posting.ReversedByPostingId.HasValue)
        {
            errors.Add($"Posting {postingId} has already been reversed by posting {posting.ReversedByPostingId.Value}");
            return new ReversalValidationDto(false, errors);
        }

        // Check if this is itself a reversal
        if (posting.ReversalForPostingId.HasValue)
        {
            errors.Add($"Posting {postingId} is itself a reversal and cannot be reversed");
            return new ReversalValidationDto(false, errors);
        }

        // Check for partially reversed group
        var relatedPostings = await GetRelatedPostingsAsync(postingId, ct);
        var allPostings = relatedPostings.Prepend(posting).ToList();
        var anyAlreadyReversed = allPostings.Any(p => p.ReversedByPostingId.HasValue);
        if (anyAlreadyReversed)
        {
            errors.Add($"Cannot reverse posting {postingId}: group is partially reversed. All-or-nothing policy enforced.");
            return new ReversalValidationDto(false, errors);
        }

        return new ReversalValidationDto(true, Array.Empty<string>());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Posting>> GetRelatedPostingsAsync(Guid postingId, CancellationToken ct = default)
    {
        var posting = await _context.Postings.FirstOrDefaultAsync(p => p.Id == postingId, ct);
        if (posting == null)
        {
            return Array.Empty<Posting>();
        }

        // A posting with GroupId == Guid.Empty is ungrouped — no related postings exist
        if (posting.GroupId == Guid.Empty)
        {
            return Array.Empty<Posting>();
        }

        // Get all postings with the same GroupId (excluding the original posting itself)
        var relatedPostings = await _context.Postings
            .Where(p => p.GroupId == posting.GroupId && p.Id != postingId)
            .ToListAsync(ct);

        return relatedPostings;
    }

    /// <summary>
    /// Creates a reversal posting with negated amount and same dates/references.
    /// </summary>
    /// <param name="original">The original posting to reverse.</param>
    /// <param name="newGroupId">The new group ID for the reversal posting.</param>
    /// <returns>A new reversal posting.</returns>
    private Posting CreateReversalPosting(Posting original, Guid newGroupId)
    {
        var reversalSourceId = Guid.NewGuid(); // New source for reversal

        var reversal = new Posting(
            sourceId: reversalSourceId,
            kind: original.Kind,
            accountId: original.AccountId,
            contactId: original.ContactId,
            savingsPlanId: original.SavingsPlanId,
            securityId: original.SecurityId,
            bookingDate: original.BookingDate,
            valutaDate: original.ValutaDate,
            amount: -original.Amount, // Negated amount
            subject: original.Subject != null ? $"REVERSAL: {original.Subject}" : "REVERSAL",
            recipientName: original.RecipientName,
            description: original.Description,
            securitySubType: original.SecuritySubType,
            quantity: original.Quantity.HasValue ? -original.Quantity.Value : null // Negated quantity
        );

        reversal.SetGroup(newGroupId);
        reversal.SetReversalFor(original);

        return reversal;
    }

    /// <summary>
    /// Creates a statement draft mirroring the original posting for reconciliation purposes.
    /// The draft entry reflects the original booking (same amount, same subject) and carries
    /// all related assignments (contact, savings plan, security) derived from the posting group.
    /// </summary>
    /// <param name="original">The bank posting being reversed (must have an AccountId).</param>
    /// <param name="allPostings">All postings in the reversal group (including contact, savings plan, security postings).</param>
    /// <param name="userId">The user initiating the reversal (becomes draft owner).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created statement draft with a fully assigned entry.</returns>
    private async Task<StatementDraft> CreateReversalStatementDraftAsync(Posting original, IReadOnlyList<Posting> allPostings, Guid userId, CancellationToken ct)
    {
        if (!original.AccountId.HasValue)
        {
            throw new InvalidOperationException("Cannot create statement draft for posting without account");
        }

        var draft = new StatementDraft(
            ownerUserId: userId,
            originalFileName: $"REVERSAL_{original.Id}",
            accountNumber: null,
            description: original.Subject ?? $"Stornierung {original.Id}");

        draft.SetDetectedAccount(original.AccountId.Value);

        // Add entry mirroring the original bank posting (not the counter-booking)
        var entry = draft.AddEntry(
            bookingDate: original.BookingDate,
            amount: original.Amount,
            subject: original.Subject ?? string.Empty,
            recipientName: original.RecipientName,
            valutaDate: original.ValutaDate,
            currencyCode: null,
            bookingDescription: original.Description,
            isAnnounced: false,
            isCostNeutral: false);

        // Derive contact assignment from related contact postings
        var contactPosting = allPostings.FirstOrDefault(p => p.Kind == PostingKind.Contact && p.ContactId.HasValue);
        if (contactPosting != null)
        {
            entry.AssignContactWithoutAccounting(contactPosting.ContactId!.Value);
        }

        // Derive savings plan assignment from related savings plan postings
        var savingsPlanPosting = allPostings.FirstOrDefault(p => p.Kind == PostingKind.SavingsPlan && p.SavingsPlanId.HasValue);
        if (savingsPlanPosting != null)
        {
            entry.AssignSavingsPlan(savingsPlanPosting.SavingsPlanId!.Value);
        }

        // Derive security assignment from related security postings
        var securityPosting = allPostings.FirstOrDefault(p =>
            p.Kind == PostingKind.Security
            && p.SecurityId.HasValue
            && p.SecuritySubType is SecurityPostingSubType.Buy or SecurityPostingSubType.Sell or SecurityPostingSubType.Dividend);

        if (securityPosting != null)
        {
            var txType = securityPosting.SecuritySubType switch
            {
                SecurityPostingSubType.Buy => SecurityTransactionType.Buy,
                SecurityPostingSubType.Sell => SecurityTransactionType.Sell,
                SecurityPostingSubType.Dividend => SecurityTransactionType.Dividend,
                _ => (SecurityTransactionType?)null
            };

            var feeAmount = allPostings
                .Where(p => p.Kind == PostingKind.Security && p.SecuritySubType == SecurityPostingSubType.Fee)
                .Sum(p => Math.Abs(p.Amount));

            var taxAmount = allPostings
                .Where(p => p.Kind == PostingKind.Security && p.SecuritySubType == SecurityPostingSubType.Tax)
                .Sum(p => Math.Abs(p.Amount));

            entry.SetSecurity(
                securityId: securityPosting.SecurityId!.Value,
                txType: txType,
                quantity: securityPosting.Quantity,
                fee: feeAmount == 0 ? null : feeAmount,
                tax: taxAmount == 0 ? null : taxAmount);
        }

        _context.StatementDrafts.Add(draft);
        await _context.SaveChangesAsync(ct);

        return draft;
    }

    /// <summary>
    /// Gets the owner user ID for a posting by looking up the account owner.
    /// </summary>
    /// <param name="posting">The posting to get the owner for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The owner user ID.</returns>
    private async Task<Guid> GetPostingOwnerUserIdAsync(Posting posting, CancellationToken ct)
    {
        if (posting.AccountId.HasValue)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == posting.AccountId.Value, ct);
            if (account != null)
            {
                return account.OwnerUserId;
            }
        }

        // Fallback: try to find owner via contact (no direct UserId property, need to query differently)
        // For now, we require AccountId as the primary authorization source
        
        throw new InvalidOperationException($"Cannot determine owner for posting {posting.Id}: no account reference found");
    }
}
