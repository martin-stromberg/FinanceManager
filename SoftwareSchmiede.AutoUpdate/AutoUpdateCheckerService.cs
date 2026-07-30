using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Hosted service periodically running the update workflow against the configured source, honoring the
/// configured interval and time windows. Calls <see cref="IAutoUpdateOrchestrator.RunUpdateAsync"/>, which checks
/// for a newer version and then downloads and installs it depending on <see cref="AutoUpdateOptions.EnableAutomaticDownload"/>
/// and <see cref="AutoUpdateOptions.EnableAutomaticInstallation"/>.
/// </summary>
public sealed class AutoUpdateCheckerService : BackgroundService
{
    private const int MinimumIntervalMinutes = 1;
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(5);

    private readonly IAutoUpdateOrchestrator _orchestrator;
    private readonly AutoUpdateOptions _options;
    private readonly SourceCheckWindowEvaluator _windowEvaluator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutoUpdateCheckerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateCheckerService"/> class.
    /// </summary>
    /// <param name="orchestrator">Used to run the update workflow.</param>
    /// <param name="options">The runtime-mutable auto-update options, read fresh on every iteration.</param>
    /// <param name="windowEvaluator">Used to determine whether checks are currently allowed.</param>
    /// <param name="timeProvider">The time source used for scheduling and delays.</param>
    /// <param name="logger">Used to log failures.</param>
    public AutoUpdateCheckerService(
        IAutoUpdateOrchestrator orchestrator,
        AutoUpdateOptions options,
        SourceCheckWindowEvaluator windowEvaluator,
        TimeProvider timeProvider,
        ILogger<AutoUpdateCheckerService> logger)
    {
        _orchestrator = orchestrator;
        _options = options;
        _windowEvaluator = windowEvaluator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled && _windowEvaluator.IsWithinWindow(_options.SourceCheck.TimeRanges, _timeProvider.GetLocalNow()))
                {
                    await _orchestrator.RunUpdateAsync(stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(MinimumIntervalMinutes, _options.SourceCheck.Interval)), _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periodic update run failed.");
                try
                {
                    await Task.Delay(ErrorBackoff, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
