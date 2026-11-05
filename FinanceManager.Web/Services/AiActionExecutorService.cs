using FinanceManager.Application.Mcp;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services;

/// <summary>
/// Hosted runner of the persistent approval queue (F-0903;
/// <c>BackgroundTaskRunner</c>/<see cref="MonthlyReminderScheduler"/>-Muster): polls the
/// <see cref="FinanceManager.Domain.Mcp.AiActionRequest"/> table in
/// <c>Mcp:Approval:ExecutorPollSeconds</c> intervals and delegates each pass to the scoped
/// <see cref="IAiActionExecutionEngine"/>. The table itself is the queue — approved
/// requests survive process restarts (approval-konzept.md §5.1).
/// </summary>
public sealed class AiActionExecutorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly ILogger<AiActionExecutorService> _logger;

    /// <summary>Creates the runner.</summary>
    /// <param name="scopeFactory">Scope factory — a scope per pass.</param>
    /// <param name="options">MCP options (poll interval).</param>
    /// <param name="logger">Logger.</param>
    public AiActionExecutorService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<McpOptions> options,
        ILogger<AiActionExecutorService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = Math.Max(1, _options.CurrentValue.Approval.ExecutorPollSeconds);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<IMcpSettingsService>();
                if (await settings.IsEnabledAsync(stoppingToken))
                {
                    var engine = scope.ServiceProvider.GetRequiredService<IAiActionExecutionEngine>();
                    await engine.ExecuteDueRequestsAsync(
                        _options.CurrentValue.Approval.MaxExecutionBatchSize, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AiActionExecutorService pass failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
