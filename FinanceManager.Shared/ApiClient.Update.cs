#pragma warning disable CS1591
using System.Net.Http.Json;
using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    public async Task<UpdateStatusDto> Updates_GetStatusAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/setup/update/status", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<UpdateStatusDto>(cancellationToken: ct))!;
    }

    public async Task<UpdateSettingsDto> Updates_GetSettingsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/setup/update/settings", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<UpdateSettingsDto>(cancellationToken: ct))!;
    }

    public async Task<UpdateSettingsDto> Updates_UpdateSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync("/api/setup/update/settings", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<UpdateSettingsDto>(cancellationToken: ct))!;
    }

    public async Task<UpdateCheckResultDto> Updates_CheckAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("/api/setup/update/check", content: null, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<UpdateCheckResultDto>(cancellationToken: ct))!;
    }

    public async Task<UpdateSettingsDto> Updates_ScheduleAsync(UpdateScheduleRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/setup/update/schedule", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return (await resp.Content.ReadFromJsonAsync<UpdateSettingsDto>(cancellationToken: ct))!;
    }

    public async Task<UpdateStatusDto?> Updates_StartInstallAsync(UpdateStartRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/setup/update/install/start", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return await resp.Content.ReadFromJsonAsync<UpdateStatusDto>(cancellationToken: ct);
    }

    public async Task<bool> Updates_ResetLockAsync(UpdateLockResetRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/setup/update/lock/reset", request, ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        return true;
    }
}
#pragma warning restore CS1591
