using FinanceManager.Application;
using FinanceManager.Shared;

namespace FinanceManager.Web.ViewModels.Setup;

public sealed class SetupSecurityViewModel : BaseViewModel
{
    private readonly ICurrentUserService _current;

    public SetupSecurityViewModel(IServiceProvider sp) : base(sp)
    {
        _current = sp.GetRequiredService<ICurrentUserService>();
    }

    public sealed class IpBlockItem
    {
        public Guid Id { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public bool IsBlocked { get; set; }
        public DateTime? BlockedAtUtc { get; set; }
        public string? BlockReason { get; set; }
        public int UnknownUserFailedAttempts { get; set; }
        public DateTime? UnknownUserLastFailedUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? ModifiedUtc { get; set; }
    }

    public List<IpBlockItem> Items { get; private set; } = new();
    public bool Busy { get; private set; }
    public string Ip { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool BlockOnCreate { get; set; } = true;
    public bool IsAdmin => _current.IsAdmin;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        try
        {
            CheckAuthentication();
            CheckAdmin();
            SetError(null, null);
            var list = await ApiClient.Admin_ListIpBlocksAsync(null, ct);
            Items = (list ?? Array.Empty<IpBlockDto>())
                .Select(d => new IpBlockItem
                {
                    Id = d.Id,
                    IpAddress = d.IpAddress,
                    IsBlocked = d.IsBlocked,
                    BlockedAtUtc = d.BlockedAtUtc,
                    BlockReason = d.BlockReason,
                    UnknownUserFailedAttempts = d.UnknownUserFailedAttempts,
                    UnknownUserLastFailedUtc = d.UnknownUserLastFailedUtc,
                    CreatedUtc = d.CreatedUtc,
                    ModifiedUtc = d.ModifiedUtc
                })
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
        }
        finally { RaiseStateChanged(); }
    }

    private void CheckAdmin()
    {
        if (!IsAdmin) throw new UnauthorizedAccessException("Error_AdminAccessRequired");
    }

    public async Task CreateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Ip)) { SetError("Error_IpRequired", ""); RaiseStateChanged(); return; }
        Busy = true; RaiseStateChanged();
        try
        {
            var req = new IpBlockCreateRequest(Ip.Trim(), Reason, BlockOnCreate);
            var _ = await ApiClient.Admin_CreateIpBlockAsync(req, ct);
            Ip = string.Empty; Reason = null; BlockOnCreate = true;
            await ReloadAsync(ct);
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    public async Task BlockAsync(Guid id, CancellationToken ct = default)
    {
        try { await ApiClient.Admin_BlockIpAsync(id, null, ct); }
        catch { }
        await ReloadAsync(ct);
    }
    public async Task UnblockAsync(Guid id, CancellationToken ct = default)
    {
        try { await ApiClient.Admin_UnblockIpAsync(id, ct); }
        catch { }
        await ReloadAsync(ct);
    }
    public async Task ResetCountersAsync(Guid id, CancellationToken ct = default)
    {
        try { await ApiClient.Admin_ResetCountersAsync(id, ct); }
        catch { }
        await ReloadAsync(ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try { await ApiClient.Admin_DeleteIpBlockAsync(id, ct); }
        catch { }
        await ReloadAsync(ct);
    }
}
