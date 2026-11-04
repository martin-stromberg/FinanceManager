using FinanceManager.Application.Mcp;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services;

/// <summary>
/// Hosted expiry sweep of the approval queue (F-0904;
/// <see cref="MonthlyReminderScheduler"/>-Muster): every
/// <c>Mcp:Approval:ExpiryCheckIntervalMinutes</c> it marks pending and approved requests
/// whose decision deadline passed as <c>Expired</c> — with an <c>ApprovalEvent</c> audit
/// record per request (approval-konzept.md §3.3).
/// </summary>
public sealed class AiActionExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly ILogger<AiActionExpiryService> _logger;

    /// <summary>Creates the runner.</summary>
    /// <param name="scopeFactory">Scope factory — a scope per pass.</param>
    /// <param name="options">MCP options (sweep interval, kill-switch).</param>
    /// <param name="logger">Logger.</param>
    public AiActionExpiryService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<McpOptions> options,
        ILogger<AiActionExpiryService> logger)
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
            var interval = Math.Max(1, _options.CurrentValue.Approval.ExpiryCheckIntervalMinutes);
            try
            {
                if (_options.CurrentValue.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var engine = scope.ServiceProvider.GetRequiredService<IAiActionExecutionEngine>();
                    await engine.ExpireOverdueRequestsAsync(DateTime.UtcNow, 200, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AiActionExpiryService pass failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
