using Microsoft.Extensions.Localization;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// View model responsible for managing backups in the setup area.
/// Provides operations to list, create, upload, delete and trigger restore of backups.
/// </summary>
public sealed class SetupBackupsViewModel : BaseViewModel
{
    /// <summary>
    /// Initializes a new instance of <see cref="SetupBackupsViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve dependencies (API client, localization, etc.).</param>
    public SetupBackupsViewModel(IServiceProvider sp) : base(sp)
    {
    }

    /// <summary>
    /// Represents a single backup item returned by the API and displayed in the UI.
    /// </summary>
    public sealed class BackupItem
    {
        /// <summary>
        /// Identifier of the backup.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// UTC timestamp when the backup was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// File name of the backup archive.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Size of the backup in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Source description of the backup (e.g. local, cloud provider).
        /// </summary>
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// Currently loaded list of backups; <c>null</c> when not loaded yet.
    /// </summary>
    public List<BackupItem>? Backups { get; private set; }

    /// <summary>
    /// Indicates whether an operation that may take a while (create/upload/delete) is in progress.
    /// </summary>
    public bool Busy { get; private set; }

    /// <summary>
    /// Indicates whether a restore operation is currently active in the system.
    /// </summary>
    public bool HasActiveRestore { get; private set; }

    /// <summary>
    /// Event used to request the UI to open a file picker for upload. Subscribers should
    /// open the file dialog and call <see cref="UploadAsync"/> with the chosen stream.
    /// </summary>
    public event EventHandler? UploadRequested;

    /// <summary>
    /// Loads the list of backups from the API and populates the <see cref="Backups"/> collection.
    /// Any errors are captured via <see cref="SetError(string?, string?)"/>.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
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

    /// <summary>
    /// Creates a new backup via the API and inserts it at the top of the <see cref="Backups"/> list when successful.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when the create operation has finished.</returns>
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

    /// <summary>
    /// Starts applying the specified backup. The server may return the current restore status which is
    /// reflected via <see cref="HasActiveRestore"/>.
    /// </summary>
    /// <param name="id">Backup identifier to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the request finishes.</returns>
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

    /// <summary>
    /// Deletes the backup with the specified id via the API and removes it from the local list on success.
    /// </summary>
    /// <param name="id">Identifier of the backup to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when deletion has finished.</returns>
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

    /// <summary>
    /// Uploads a backup file stream to the server and adds the returned backup to the local list.
    /// </summary>
    /// <param name="stream">Stream containing the backup file data. Must not be <c>null</c>.</param>
    /// <param name="fileName">Name of the uploaded file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the upload has finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <c>null</c>.</exception>
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

    /// <summary>
    /// Inserts a backup item at the top of the local list and notifies subscribers about the change.
    /// </summary>
    /// <param name="item">Backup item to add.</param>
    public void AddBackup(BackupItem item)
    {
        Backups ??= new List<BackupItem>();
        Backups.Insert(0, item);
        RaiseStateChanged();
    }

    /// <summary>
    /// Triggers the <see cref="UploadRequested"/> event to instruct the UI to open a file picker.
    /// </summary>
    public void TriggerUploadRequest()
    {
        UploadRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears any pending upload request. This method is kept for compatibility and is currently a no-op.
    /// </summary>
    public void ClearUploadRequest()
    {
        // kept for compatibility if something relied on clearing, no-op
    }

    /// <summary>
    /// Builds ribbon register definitions for backup related actions (create/upload).
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels.</param>
    /// <returns>Collection of ribbon register definitions or <c>null</c> when none are provided.</returns>
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
