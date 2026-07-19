#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateScheduler> _logger;
    private DateOnly? _lastAttemptedDate;
    private TimeOnly? _lastAttemptedTime;

    public UpdateScheduler(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<UpdateScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IUpdateOrchestrator>();
                var settings = await orchestrator.GetSettingsAsync(stoppingToken);
                var status = await orchestrator.GetStatusAsync(stoppingToken);
                var now = _timeProvider.GetLocalNow().DateTime;
                if (ShouldInstall(settings.ScheduledInstallTime, status, now))
                {
                    _lastAttemptedDate = DateOnly.FromDateTime(now);
                    _lastAttemptedTime = settings.ScheduledInstallTime;
                    await orchestrator.StartInstallAsync(confirmDowntime: true, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled update installation failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public bool ShouldInstall(TimeOnly? scheduledTime, UpdateStatusDto status, DateTime now)
    {
        if (!scheduledTime.HasValue || status.Status != UpdateStatusKind.Ready || status.IsLocked)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(now);
        if (_lastAttemptedDate == today && _lastAttemptedTime == scheduledTime.Value)
        {
            return false;
        }

        return TimeOnly.FromDateTime(now) >= scheduledTime.Value;
    }
}
#pragma warning restore CS1591
