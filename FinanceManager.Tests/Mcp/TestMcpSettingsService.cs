using FinanceManager.Application.Mcp;
using FinanceManager.Shared.Dtos.Mcp;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// In-memory <see cref="IMcpSettingsService"/> stub for unit tests: returns the configured
/// switch states without touching a database.
/// </summary>
internal sealed class TestMcpSettingsService : IMcpSettingsService
{
    public TestMcpSettingsService(bool enabled = true, bool writeAccessEnabled = true)
    {
        Enabled = enabled;
        WriteAccessEnabled = writeAccessEnabled;
    }

    public bool Enabled { get; set; }
    public bool WriteAccessEnabled { get; set; }

    public Task<McpSettingsDto> GetAsync(CancellationToken ct)
        => Task.FromResult(new McpSettingsDto(Enabled, WriteAccessEnabled));

    public Task UpdateAsync(McpSettingsUpdateRequest request, CancellationToken ct)
    {
        Enabled = request.Enabled;
        WriteAccessEnabled = request.WriteAccessEnabled;
        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync(CancellationToken ct) => Task.FromResult(Enabled);

    public Task<bool> IsWriteAccessEnabledAsync(CancellationToken ct) => Task.FromResult(WriteAccessEnabled);
}
