using FinanceManager.Application;
using FinanceManager.Application.Budget;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.Services;

/// <summary>
/// Background task executor that refreshes marked budget report cache entries.
/// </summary>
public sealed class ReportCacheRefreshTaskExecutor : IBackgroundTaskExecutor
{
    /// <summary>
    /// Gets the background task type handled by this executor.
    /// </summary>
    public BackgroundTaskType Type => BackgroundTaskType.RefreshBudgetReportCache;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportCacheRefreshTaskExecutor> _logger;
    private readonly IStringLocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportCacheRefreshTaskExecutor"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a scoped service provider for the execution run.</param>
    /// <param name="logger">Logger used to record errors and diagnostics.</param>
    /// <param name="localizer">Localizer used to provide localized progress messages.</param>
    public ReportCacheRefreshTaskExecutor(IServiceScopeFactory scopeFactory, ILogger<ReportCacheRefreshTaskExecutor> logger, IStringLocalizer<Pages> localizer)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Executes the cache refresh task for the user defined in the <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Context describing the background task, including TaskId and UserId. Must not be <c>null</c>.</param>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    public async Task ExecuteAsync(BackgroundTaskContext context, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var reportCache = scope.ServiceProvider.GetRequiredService<IReportCacheService>();
        var reports = scope.ServiceProvider.GetRequiredService<IBudgetReportService>();
        var taskMgr = scope.ServiceProvider.GetRequiredService<IBackgroundTaskManager>();

        context.ReportProgress(0, null, _localizer["RC_Start"], 0, 0);

        int processed = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var next = await reportCache.GetNextBudgetReportCacheToUpdateAsync(ct);
                if (next == null)
                {
                    break;
                }

                processed++;
                context.ReportProgress(processed, null, _localizer["RC_Progress", processed], 0, 0);

                await reports.GetRawDataAsync(context.UserId, next.From, next.To, next.DateBasis, ct, ignoreCache: true);

                var prev = taskMgr.Get(context.TaskId);
                if (prev != null)
                {
                    taskMgr.UpdateTaskInfo(prev with { Processed = processed, Message = _localizer["RC_Progress", processed] });
                }
            }

            context.ReportProgress(processed, processed, _localizer["RC_Completed"], 0, 0);
        }
        catch (OperationCanceledException)
        {
            context.ReportProgress(processed, processed, _localizer["RC_Canceled"], 0, 0);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report cache refresh failed");
            context.ReportProgress(processed, processed, ex.Message, 0, 1);
            throw;
        }
    }
}
