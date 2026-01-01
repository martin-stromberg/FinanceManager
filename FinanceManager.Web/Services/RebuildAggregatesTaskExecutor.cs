using FinanceManager.Application;
using FinanceManager.Application.Aggregates;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.Services
{
    /// <summary>
    /// Background task executor that rebuilds posting aggregates for a user.
    /// This executor reports progress via the provided <see cref="BackgroundTaskContext"/> and updates the task manager with intermediate progress.
    /// </summary>
    public sealed class RebuildAggregatesTaskExecutor : IBackgroundTaskExecutor
    {
        /// <summary>
        /// Gets the background task type handled by this executor.
        /// </summary>
        public BackgroundTaskType Type => BackgroundTaskType.RebuildAggregates;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RebuildAggregatesTaskExecutor> _logger;
        private readonly IStringLocalizer _localizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RebuildAggregatesTaskExecutor"/> class.
        /// </summary>
        /// <param name="scopeFactory">Factory used to create a scoped service provider for the execution run.</param>
        /// <param name="logger">Logger used to record errors and diagnostics.</param>
        /// <param name="localizer">Localizer used to provide localized progress messages.</param>
        public RebuildAggregatesTaskExecutor(IServiceScopeFactory scopeFactory, ILogger<RebuildAggregatesTaskExecutor> logger, IStringLocalizer<Pages> localizer)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _localizer = localizer;
        }

        /// <summary>
        /// Executes the rebuild aggregates task for the user defined in the <paramref name="context"/>.
        /// Reports start, progress and completion via <see cref="BackgroundTaskContext.ReportProgress"/> and updates the task manager with intermediate values.
        /// </summary>
        /// <param name="context">Context describing the background task, including TaskId and UserId. Must not be <c>null</c>.</param>
        /// <param name="ct">Cancellation token used to cancel the operation. When cancelled an <see cref="OperationCanceledException"/> will be thrown.</param>
        /// <returns>A task that completes when the rebuild operation has finished or is cancelled.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the provided <paramref name="ct"/> is cancelled.</exception>
        /// <exception cref="Exception">Propagates unexpected exceptions that occur during the rebuild; the exception is logged and rethrown by the caller.</exception>
        public async Task ExecuteAsync(BackgroundTaskContext context, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var agg = scope.ServiceProvider.GetRequiredService<IPostingAggregateService>();
            var taskMgr = scope.ServiceProvider.GetRequiredService<IBackgroundTaskManager>();

            // report start
            context.ReportProgress(0, null, _localizer["RB_Start"], 0, 0);
            try
            {
                await agg.RebuildForUserAsync(context.UserId, (done, total) =>
                {
                    var prev = taskMgr.Get(context.TaskId);
                    if (prev != null)
                    {
                        var updated = prev with { Processed = done, Total = total, Message = _localizer["RB_Progress", done, total] };
                        taskMgr.UpdateTaskInfo(updated);
                    }
                }, ct);
                context.ReportProgress(1, 1, _localizer["RB_Completed"], 0, 0);
            }
            catch (OperationCanceledException)
            {
                context.ReportProgress(0, 1, _localizer["RB_Canceled"], 0, 0);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rebuild aggregates failed");
                context.ReportProgress(0, 1, ex.Message, 0, 1);
                throw;
            }
        }
    }
}
