using Microsoft.Extensions.Localization;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Setup;

public sealed class SetupBackupsViewModel : BaseViewModel
{

    public SetupBackupsViewModel(IServiceProvider sp) : base(sp)
    {
    }

    public sealed class BackupItem
    {
        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public List<BackupItem>? Backups { get; private set; }
    public bool Busy { get; private set; }
    public bool HasActiveRestore { get; private set; }

    // Event used to request the UI to open file picker
    public event EventHandler? UploadRequested;

    public async Task LoadBackupsAsync(CancellationToken ct = default)
    {
        try
        {
            SetError(null, null);
            var list = await ApiClient.Backups_ListAsync(ct);
            Backups = list?.Select(b => new BackupItem { Id = b.Id, CreatedUtc = b.CreatedUtc, FileName = b.FileName, SizeBytes = b.SizeBytes, Source = b.Source }).ToList() ?? new List<BackupItem>();
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
            Backups = new List<BackupItem>();
        }
        finally { RaiseStateChanged(); }
    }

    public async Task CreateAsync(CancellationToken ct = default)
    {
        Busy = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var created = await ApiClient.Backups_CreateAsync(ct);
            if (created is not null)
            {
                Backups ??= new List<BackupItem>();
                Backups.Insert(0, new BackupItem { Id = created.Id, CreatedUtc = created.CreatedUtc, FileName = created.FileName, SizeBytes = created.SizeBytes, Source = created.Source });
            }
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    public async Task StartApplyAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) { return; }
        try
        {
            var status = await ApiClient.Backups_StartApplyAsync(id, ct);
            HasActiveRestore = status.Running;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { RaiseStateChanged(); }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Busy = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await ApiClient.Backups_DeleteAsync(id, ct);
            if (ok)
            {
                if (Backups is not null)
                {
                    Backups.RemoveAll(x => x.Id == id);
                }
            }
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    public async Task UploadAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Busy = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var created = await ApiClient.Backups_UploadAsync(stream, fileName, ct);
            if (created is not null)
            {
                AddBackup(new BackupItem { Id = created.Id, CreatedUtc = created.CreatedUtc, FileName = created.FileName, SizeBytes = created.SizeBytes, Source = created.Source });
            }
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    public void AddBackup(BackupItem item)
    {
        Backups ??= new List<BackupItem>();
        Backups.Insert(0, item);
        RaiseStateChanged();
    }

    public void TriggerUploadRequest()
    {
        UploadRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ClearUploadRequest()
    {
        // kept for compatibility if something relied on clearing, no-op
    }

    // Provide ribbon actions for Backup create/upload
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "CreateBackup",
                localizer["Ribbon_CreateBackup"].Value,
                "<svg><use href='/icons/sprite.svg#save'/></svg>",
                UiRibbonItemSize.Large,
                false,
                localizer["Hint_CreateBackup"].Value ?? string.Empty,
                "CreateBackup",
                new Func<Task>(async () =>
                {
                    try { await CreateAsync(); } catch { }
                })),

            new UiRibbonAction(
                "UploadBackup",
                localizer["Ribbon_UploadBackup"].Value,
                "<svg><use href='/icons/sprite.svg#upload'/></svg>",
                UiRibbonItemSize.Large,
                false,
                localizer["Hint_UploadBackup"].Value ?? string.Empty,
                "UploadBackup",
                new Func<Task>(() =>
                {
                    try { TriggerUploadRequest(); } catch { }
                    return Task.CompletedTask;
                }))
        };

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Actions"].Value, actions, int.MaxValue)
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}
