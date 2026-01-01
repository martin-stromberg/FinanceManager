using FinanceManager.Application;
using FinanceManager.Application.Backups;
using Microsoft.Extensions.Localization;
using System.Text.Json;

namespace FinanceManager.Web.Services
{
    /// <summary>
    /// Background task executor that applies a previously created backup to the database.
    /// The executor reads a JSON payload containing a <c>BackupId</c> and an optional <c>ReplaceExisting</c> flag
    /// and uses <see cref="IBackupService"/> to apply the backup. Progress is reported via the provided <see cref="BackgroundTaskContext"/>.
    /// </summary>
    public sealed class BackupRestoreTaskExecutor : IBackgroundTaskExecutor
    {
        /// <summary>
        /// The <see cref="BackgroundTaskType"/> handled by this executor.
        /// </summary>
        public BackgroundTaskType Type => BackgroundTaskType.BackupRestore;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackupRestoreTaskExecutor> _logger;
        private readonly IStringLocalizer _localizer;

        /// <summary>
        /// Payload shape expected by the executor when enqueued: JSON object with <c>BackupId</c> (GUID) and optional <c>ReplaceExisting</c> (bool).
        /// </summary>
        /// <param name="BackupId">Identifier of the backup to restore.</param>
        /// <param name="ReplaceExisting">When <c>true</c> existing data will be replaced; when <c>false</c> it may be merged according to service rules.</param>
        private sealed record RestorePayload(Guid BackupId, bool ReplaceExisting);

        /// <summary>
        /// Initializes a new instance of <see cref="BackupRestoreTaskExecutor"/>.
        /// </summary>
        /// <param name="scopeFactory">Factory to create a service scope used to resolve scoped services during execution.</param>
        /// <param name="logger">Logger instance for the executor.</param>
        /// <param name="localizer">Localizer used to produce localized progress messages.</param>
        public BackupRestoreTaskExecutor(IServiceScopeFactory scopeFactory, ILogger<BackupRestoreTaskExecutor> logger, IStringLocalizer<Pages> localizer)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _localizer = localizer;
        }

        /// <summary>
        /// Executes the backup restore task. The task <paramref name="context"/> must contain a JSON payload with a <c>BackupId</c> (GUID)
        /// and optional <c>ReplaceExisting</c> boolean. Progress updates are reported through the <paramref name="context"/>.
        /// </summary>
        /// <param name="context">Context object that contains task metadata, payload and progress reporting helpers.</param>
        /// <param name="ct">Cancellation token that may be used to cancel the operation.</param>
        /// <returns>A task that completes when the restore has finished.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the task payload is missing or invalid, or when the backup apply operation fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the provided <paramref name="ct"/>.</exception>
        /// <exception cref="System.Exception">Propagates exceptions from underlying services (logged and reported) when unexpected errors occur.</exception>
        public async Task ExecuteAsync(BackgroundTaskContext context, CancellationToken ct)
        {
            if (context.Payload is not string raw || string.IsNullOrWhiteSpace(raw))
            {
                context.ReportProgress(0, 1, _localizer["BR_NoBackupId"], 0, 1);
                throw new InvalidOperationException("BackupId payload required");
            }
            Guid backupId;
            var replaceExisting = true; // current behaviour always replace
            try
            {
                using var doc = JsonDocument.Parse(raw);
                backupId = doc.RootElement.GetProperty("BackupId").GetGuid();
                if (doc.RootElement.TryGetProperty("ReplaceExisting", out var repEl))
                {
                    replaceExisting = repEl.GetBoolean();
                }
            }
            catch (Exception ex)
            {
                context.ReportProgress(0, 1, _localizer["BR_InvalidPayload"], 0, 1);
                throw new InvalidOperationException("Invalid backup restore payload", ex);
            }

            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var taskManager = scope.ServiceProvider.GetRequiredService<IBackgroundTaskManager>();

            context.ReportProgress(0, 0, _localizer["BR_Start"], 0, 0);
            try
            {
                var ok = await backupService.ApplyAsync(context.UserId, backupId, (desc, step, stepTotal, subStep, subTotal) =>
                {
                    var info = taskManager.Get(context.TaskId);
                    if (info != null)
                    {
                        var updated = info with
                        {
                            Processed = step + 1,
                            Total = stepTotal + 1,
                            Message = string.IsNullOrWhiteSpace(desc) ? info.Message : desc,
                            Processed2 = subStep,
                            Total2 = subTotal,
                            Message2 = null
                        };
                        taskManager.UpdateTaskInfo(updated);
                    }
                }, ct);
                if (!ok)
                {
                    context.ReportProgress(0, 0, _localizer["BR_ApplyFailed"], 0, 1);
                    throw new InvalidOperationException("Backup apply failed");
                }
                var finalInfo = taskManager.Get(context.TaskId);
                var finalProcessed = finalInfo?.Processed ?? 1;
                var finalTotal = finalInfo?.Total ?? finalProcessed;
                context.ReportProgress(finalProcessed, finalTotal, _localizer["BR_Completed"], 0, 0);
            }
            catch (OperationCanceledException)
            {
                context.ReportProgress(0, 0, _localizer["BR_Canceled"], 0, 0);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup restore failed for backup {BackupId}", backupId);
                context.ReportProgress(0, 0, ex.Message, 0, 1);
                throw;
            }
        }
    }
}
