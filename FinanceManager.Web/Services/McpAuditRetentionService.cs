using FinanceManager.Application.Mcp;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services;

/// <summary>
/// Hosted retention runner of the MCP integration (F-1104;
/// <see cref="AiActionExpiryService"/>-Muster): every
/// <c>Mcp:Retention:IntervalMinutes</c> it delegates one pass to the scoped
/// <see cref="IMcpAuditRetention"/> executor — the only deletion path on the audit table.
/// Inert while <c>Mcp:Enabled</c> is off.
/// </summary>
public sealed class McpAuditRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<McpOptions> _options;
    private readonly ILogger<McpAuditRetentionService> _logger;

    /// <summary>Creates the runner.</summary>
    /// <param name="scopeFactory">Scope factory — a scope per pass.</param>
    /// <param name="options">MCP options (retention interval, kill-switch).</param>
    /// <param name="logger">Logger.</param>
    public McpAuditRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<McpOptions> options,
        ILogger<McpAuditRetentionService> logger)
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
            var interval = Math.Max(1, _options.CurrentValue.Retention.IntervalMinutes);
            try
            {
                if (_options.CurrentValue.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var retention = scope.ServiceProvider.GetRequiredService<IMcpAuditRetention>();
                    var result = await retention.RunAsync(DateTime.UtcNow, stoppingToken);
                    if (result.EntriesRemoved > 0 || result.RequestsRemoved > 0)
                    {
                        _logger.LogInformation(
                            "MCP retention pass: {Entries} audit entries, {Requests} requests removed ({Markers} cut markers, {Chains} chains)",
                            result.EntriesRemoved, result.RequestsRemoved,
                            result.CutMarkersWritten, result.ChainsProcessed);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "McpAuditRetentionService pass failed");
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
