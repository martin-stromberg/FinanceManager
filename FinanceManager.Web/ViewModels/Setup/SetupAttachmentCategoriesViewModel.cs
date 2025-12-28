namespace FinanceManager.Web.ViewModels.Setup;

using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Attachments;

public sealed class SetupAttachmentCategoriesViewModel : BaseViewModel
{

    public SetupAttachmentCategoriesViewModel(IServiceProvider sp) : base(sp)
    {
    }

    public List<AttachmentCategoryDto> Items { get; } = new();

    public bool Busy { get; private set; }
    public bool ActionOk { get; private set; }

    public string NewName { get; set; } = string.Empty;
    public bool CanAdd => !string.IsNullOrWhiteSpace(NewName) && NewName.Trim().Length >= 2;

    public Guid EditId { get; private set; }
    public string EditName { get; set; } = string.Empty;
    public bool CanSaveEdit => !string.IsNullOrWhiteSpace(EditName) && EditName.Trim().Length >= 2;

    public void OnChanged()
    {
        ActionOk = false; SetError(null,null); RaiseStateChanged();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Loading = true; SetError(null, null); ActionOk = false; EditId = Guid.Empty; EditName = string.Empty; RaiseStateChanged();
        try
        {
            var list = await ApiClient.Attachments_ListCategoriesAsync(ct);
            Items.Clear();
            if (list is not null)
            {
                Items.AddRange(list.OrderBy(x => x.Name));
            }
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    public async Task AddAsync(CancellationToken ct = default)
    {
        var name = NewName?.Trim() ?? string.Empty;
        if (name.Length < 2) { return; }
        Busy = true; SetError(null,null); ActionOk = false; RaiseStateChanged();
        try
        {
            var dto = await ApiClient.Attachments_CreateCategoryAsync(name, ct);
            if (dto is not null)
            {
                Items.Add(dto);
                Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
                NewName = string.Empty;
                ActionOk = true;
            }
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    public void BeginEdit(Guid id, string currentName)
    {
        if (Busy) { return; }
        EditId = id; EditName = currentName; SetError(null,null); ActionOk = false; RaiseStateChanged();
    }

    public void CancelEdit()
    {
        EditId = Guid.Empty; EditName = string.Empty; SetError(null,null); RaiseStateChanged();
    }

    public async Task SaveEditAsync(CancellationToken ct = default)
    {
        if (EditId == Guid.Empty) { return; }
        var name = EditName?.Trim() ?? string.Empty;
        if (name.Length < 2) { return; }
        Busy = true; SetError(null,null); ActionOk = false; RaiseStateChanged();
        try
        {
            var dto = await ApiClient.Attachments_UpdateCategoryNameAsync(EditId, name, ct);
            if (dto is not null)
            {
                var idx = Items.FindIndex(x => x.Id == dto.Id);
                if (idx >= 0) { Items[idx] = dto; }
                else { Items.Add(dto); }
                Items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
                ActionOk = true;
                CancelEdit();
            }
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Busy = true; SetError(null,null); ActionOk = false; RaiseStateChanged();
        try
        {
            var ok = await ApiClient.Attachments_DeleteCategoryAsync(id, ct);
            if (ok)
            {
                var idx = Items.FindIndex(x => x.Id == id);
                if (idx >= 0) { Items.RemoveAt(idx); }
                ActionOk = true;
            }
            else
            {
                SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Delete failed");
            }
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Delete failed");
        }
        finally { Busy = false; RaiseStateChanged(); }
    }
}
