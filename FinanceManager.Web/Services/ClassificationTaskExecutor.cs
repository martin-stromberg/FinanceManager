using FinanceManager.Application;
using FinanceManager.Application.Statements;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.Services
{
    /// <summary>
    /// Background task executor that classifies all open statement drafts for a user.
    /// The executor iterates over open drafts and invokes the classification logic on each draft,
    /// reporting progress via the provided <see cref="BackgroundTaskContext"/>.
    /// </summary>
    public sealed class ClassificationTaskExecutor : IBackgroundTaskExecutor
    {
        /// <summary>
        /// The background task type handled by this executor.
        /// </summary>
        public BackgroundTaskType Type => BackgroundTaskType.ClassifyAllDrafts;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ClassificationTaskExecutor> _logger;
        private readonly IStringLocalizer _localizer;

        /// <summary>
        /// Initializes a new instance of <see cref="ClassificationTaskExecutor"/>.
        /// </summary>
        /// <param name="scopeFactory">Factory used to create an <see cref="IServiceScope"/> for resolving scoped services during execution.</param>
        /// <param name="logger">Logger for reporting warnings and errors.</param>
        /// <param name="localizer">Localizer used to produce localized progress messages.</param>
        public ClassificationTaskExecutor(IServiceScopeFactory scopeFactory, ILogger<ClassificationTaskExecutor> logger, IStringLocalizer<Pages> localizer)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _localizer = localizer;
        }

        /// <summary>
        /// Executes the classification task for all open drafts belonging to the user specified in <paramref name="context"/>.
        /// Progress is reported using <see cref="BackgroundTaskContext.ReportProgress(int,int,string,int,int)"/>.
        /// </summary>
        /// <param name="context">Background task execution context containing TaskId, UserId and optional Payload.</param>
        /// <param name="ct">Cancellation token that may be used to cancel execution. When cancellation is requested an <see cref="OperationCanceledException"/> is thrown.</param>
        /// <returns>A task that completes when all drafts have been processed or the operation is cancelled.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the provided <paramref name="ct"/> requests cancellation.</exception>
        public async Task ExecuteAsync(BackgroundTaskContext context, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var draftService = scope.ServiceProvider.GetRequiredService<IStatementDraftService>();
            var drafts = await draftService.GetOpenDraftsAsync(context.UserId, ct);
            int total = drafts.Count;
            int processed = 0;
            context.ReportProgress(processed, total, _localizer["CL_Start"], 0, 0);
            foreach (var draft in drafts)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await draftService.ClassifyAsync(draft.DraftId, null, context.UserId, ct);
                    processed++;
                    context.ReportProgress(processed, total, _localizer["CL_Progress", processed, total], 0, 0);
                }
                catch (Exception ex)
                {
                    // Log and continue with next draft; errors are reported to the background task progress
                    _logger.LogWarning(ex, "Classification failed for draft {DraftId}", draft.DraftId);
                    context.ReportProgress(processed, total, _localizer["CL_Error", ex.Message], 1, 1);
                }
            }
            context.ReportProgress(processed, total, _localizer["CL_Completed"], 0, 0);
        }
    }
}
