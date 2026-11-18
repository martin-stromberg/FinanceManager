using FinanceManager.Application;
using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Text.Json;

namespace FinanceManager.Web.Services;

/// <summary>
/// Background task executor that re-evaluates the posting type (<see cref="BankPostingType"/>) of all
/// <see cref="PostingKind.Bank"/> postings of a single account. The account id is provided via the
/// task payload (JSON <c>{"accountId":"…"}</c>); ownership is verified against
/// <see cref="BackgroundTaskContext.UserId"/> before any posting is touched.
/// </summary>
public sealed class PostingTypeReevaluationTaskExecutor : IBackgroundTaskExecutor
{
    /// <summary>
    /// The background task type handled by this executor.
    /// </summary>
    public BackgroundTaskType Type => BackgroundTaskType.ReevaluatePostingTypes;

    private const int PageSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostingTypeReevaluationTaskExecutor> _logger;
    private readonly IStringLocalizer<PostingTypeReevaluationTaskExecutor> _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostingTypeReevaluationTaskExecutor"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve scoped services for the execution run.</param>
    /// <param name="logger">Logger used to record errors and diagnostics.</param>
    /// <param name="localizer">Localizer used to provide localized progress messages.</param>
    public PostingTypeReevaluationTaskExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<PostingTypeReevaluationTaskExecutor> logger,
        IStringLocalizer<PostingTypeReevaluationTaskExecutor> localizer)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Executes the posting type re-evaluation for the account provided via the task payload.
    /// Reports start, progress and completion via <see cref="BackgroundTaskContext.ReportProgress"/>.
    /// </summary>
    /// <param name="context">Context describing the background task, including TaskId, UserId and Payload.</param>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when the re-evaluation has finished or is cancelled.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via <paramref name="ct"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the payload contains no valid account id or the account does not belong to the task owner.</exception>
    public async Task ExecuteAsync(BackgroundTaskContext context, CancellationToken ct)
    {
        var accountId = ParseAccountId(context.Payload);
        if (accountId == Guid.Empty)
        {
            context.ReportProgress(0, 1, _localizer["PTR_InvalidPayload"], 0, 1);
            throw new InvalidOperationException(_localizer["PTR_InvalidPayload"].Value);
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var accountExists = await db.Accounts.AsNoTracking()
            .AnyAsync(a => a.Id == accountId && a.OwnerUserId == context.UserId, ct);
        if (!accountExists)
        {
            _logger.LogWarning("Posting type re-evaluation aborted: account {AccountId} not found for user {UserId}", accountId, context.UserId);
            context.ReportProgress(0, 1, _localizer["PTR_AccountNotFound"], 0, 1);
            throw new InvalidOperationException(_localizer["PTR_AccountNotFound"].Value);
        }

        var rules = await db.AccountPostingRules.AsNoTracking()
            .Where(r => r.AccountId == accountId)
            .OrderBy(r => r.OrderIndex)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        var total = await db.Postings.AsNoTracking()
            .CountAsync(p => p.Kind == PostingKind.Bank && p.AccountId == accountId, ct);

        context.ReportProgress(0, total, _localizer["PTR_Start"], 0, 0);

        int processed = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await db.Postings
                .Where(p => p.Kind == PostingKind.Bank && p.AccountId == accountId)
                .OrderBy(p => p.Id)
                .Skip(processed)
                .Take(PageSize)
                .ToListAsync(ct);
            if (batch.Count == 0) { break; }

            foreach (var posting in batch)
            {
                posting.SetPostingType(
                    PostingTypeEvaluator.Evaluate(rules, posting.Subject, posting.Description, posting.Amount));
            }

            await db.SaveChangesAsync(ct);
            processed += batch.Count;
            context.ReportProgress(processed, total, _localizer["PTR_Progress", processed, total], 0, 0);
        }

        context.ReportProgress(processed, total, _localizer["PTR_Completed", processed], 0, 0);
    }

    /// <summary>
    /// Parses the account id from the task payload. The payload is expected to be a JSON string
    /// containing an <c>accountId</c> property. Returns <see cref="Guid.Empty"/> when missing or malformed.
    /// </summary>
    /// <param name="payload">Task payload provided when the background job was enqueued.</param>
    /// <returns>The parsed account id or <see cref="Guid.Empty"/>.</returns>
    private Guid ParseAccountId(object? payload)
    {
        if (payload is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                using var doc = JsonDocument.Parse(s);
                var root = doc.RootElement;
                foreach (var name in new[] { "accountId", "AccountId" })
                {
                    if (root.TryGetProperty(name, out var el))
                    {
                        if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Posting type re-evaluation payload is not valid JSON");
            }
        }
        return Guid.Empty;
    }
}
