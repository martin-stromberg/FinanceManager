using FinanceManager.Infrastructure;

namespace FinanceManager.Web.Services;

/// <summary>
/// Background service that periodically schedules monthly reminder jobs.
/// The scheduler creates a scope per run, resolves <see cref="MonthlyReminderJob"/> and executes it.
/// It runs on a rough hourly cadence to evaluate and schedule notifications when appropriate.
/// </summary>
public sealed class MonthlyReminderScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonthlyReminderScheduler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonthlyReminderScheduler"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a scoped service provider for each scheduled run.</param>
    /// <param name="logger">Logger used to record errors during execution.</param>
    public MonthlyReminderScheduler(IServiceScopeFactory scopeFactory, ILogger<MonthlyReminderScheduler> logger)
    {
        _scopeFactory = scopeFactory; _logger = logger;
    }

    /// <summary>
    /// Main execution loop for the background service. The method repeatedly creates a scope,
    /// resolves required services and executes the <see cref="MonthlyReminderJob"/>.
    /// </summary>
    /// <param name="stoppingToken">Token that signals service shutdown. When cancelled the loop exits.</param>
    /// <returns>A task that completes when the service is stopped.</returns>
    /// <exception cref="OperationCanceledException">May be thrown when the <paramref name="stoppingToken"/> is cancelled while awaiting the delay.</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var job = scope.ServiceProvider.GetRequiredService<MonthlyReminderJob>();
                await job.RunAsync(db, DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MonthlyReminderScheduler run failed");
            }

            // run every hour at minute 5 to reduce contention (rough schedule)
            var delay = TimeSpan.FromMinutes(65 - DateTime.UtcNow.Minute % 60);
            try { await Task.Delay(delay, stoppingToken); } catch { }
        }
    }
}
