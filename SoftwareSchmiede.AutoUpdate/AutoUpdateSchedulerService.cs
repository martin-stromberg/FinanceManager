using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Hosted service triggering installation of a ready update package at the configured
/// <see cref="AutoUpdateOptions.ScheduledInstallTime"/>, once per day.
/// </summary>
public sealed class AutoUpdateSchedulerService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IAutoUpdateCommandHandler _commandService;
    private readonly IAutoUpdateStatusProvider _statusProvider;
    private readonly AutoUpdateOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutoUpdateSchedulerService> _logger;
    private readonly ScheduledInstallEvaluator _evaluator;
    private DateOnly? _lastAttemptedDate;
    private TimeOnly? _lastAttemptedTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateSchedulerService"/> class.
    /// </summary>
    /// <param name="commandService">Used to trigger installation.</param>
    /// <param name="statusProvider">Used to read the current status snapshot.</param>
    /// <param name="options">The runtime-mutable auto-update options, read fresh on every iteration.</param>
    /// <param name="timeProvider">The time source used for scheduling and delays.</param>
    /// <param name="logger">Used to log failures.</param>
    /// <param name="evaluator">Used to determine whether a scheduled installation should be triggered.</param>
    public AutoUpdateSchedulerService(
        IAutoUpdateCommandHandler commandService,
        IAutoUpdateStatusProvider statusProvider,
        AutoUpdateOptions options,
        TimeProvider timeProvider,
        ILogger<AutoUpdateSchedulerService> logger,
        ScheduledInstallEvaluator? evaluator = null)
    {
        _commandService = commandService;
        _statusProvider = statusProvider;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
        _evaluator = evaluator ?? new ScheduledInstallEvaluator();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _timeProvider.GetLocalNow();
                if (_evaluator.ShouldInstall(_options.ScheduledInstallTime, _statusProvider.GetSnapshot(), now, _lastAttemptedDate, _lastAttemptedTime))
                {
                    _lastAttemptedDate = DateOnly.FromDateTime(now.DateTime);
                    _lastAttemptedTime = _options.ScheduledInstallTime;
                    await _commandService.InstallAsync(confirmDowntime: true, stoppingToken);
                }

                await Task.Delay(PollInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled update installation failed.");
                try
                {
                    await Task.Delay(PollInterval, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
