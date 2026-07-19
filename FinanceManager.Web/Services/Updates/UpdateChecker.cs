#pragma warning disable CS1591
namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateChecker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UpdateChecker> _logger;

    public UpdateChecker(IServiceScopeFactory scopeFactory, ILogger<UpdateChecker> logger)
    {
        _scopeFactory = scopeFactory;
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
                if (settings.Enabled)
                {
                    await orchestrator.CheckAsync(stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, settings.CheckIntervalMinutes)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periodic update check failed.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
#pragma warning restore CS1591
