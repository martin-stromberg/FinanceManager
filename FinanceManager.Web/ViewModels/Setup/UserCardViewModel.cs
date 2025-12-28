using FinanceManager.Domain.Attachments;
using FinanceManager.Shared.Dtos.Users;
using Microsoft.Extensions.Localization;
using System.Reflection;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.Components.Shared;

namespace FinanceManager.Web.ViewModels.Setup;

[FinanceManager.Web.ViewModels.Common.CardRoute("users")]
public sealed class UserCardViewModel : BaseCardViewModel<(string Key, string Value)>
{
    private readonly Shared.IApiClient _api;

    public UserCardViewModel(IServiceProvider sp, Shared.IApiClient apiClient) : base(sp)
    {
        _api = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public Guid UserId { get; private set; }
    public UserAdminDto? User { get; private set; }

    public override string Title => User?.Username ?? Localizer?["Users_Title"] ?? "User";

    public override async Task InitializeAsync(Guid id)
    {
        await LoadAsync(id);
    }

    // existing loader that accepts CancellationToken
    public async Task LoadAsync(Guid id, CancellationToken ct = default)
    {
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            UserId = id;
            if (id == Guid.Empty)
            {
                // New user: initialize defaults and apply any prefill
                var name = !string.IsNullOrWhiteSpace(InitPrefill) ? InitPrefill : string.Empty;

                // Create UserAdminDto via reflection to avoid depending on exact ctor signature
                var udtType = typeof(UserAdminDto);
                var ctor = udtType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
                if (ctor != null)
                {
                    var parms = ctor.GetParameters();
                    var args = new object[parms.Length];
                    for (int i = 0; i < parms.Length; i++)
                    {
                        var p = parms[i];
                        if (p.ParameterType == typeof(Guid) || p.ParameterType == typeof(Guid?)) args[i] = Guid.Empty;
                        else if (p.ParameterType == typeof(string)) args[i] = name;
                        else if (p.ParameterType == typeof(bool) || p.ParameterType == typeof(bool?)) args[i] = false;
                        else if (p.ParameterType == typeof(DateTime) || p.ParameterType == typeof(DateTime?)) args[i] = (object?)null ?? default(DateTime);
                        else args[i] = null;
                    }
                    try { User = (UserAdminDto?)ctor.Invoke(args); } catch { User = null; }
                }

                // Build card record for new user (username editable)
                CardRecord = await BuildCardRecordAsync(User);

                Loading = false;
                RaiseStateChanged();
                return;
            }

            var u = await _api.Admin_GetUserAsync(id, ct);
            if (u != null) User = u; else SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");

            CardRecord = await BuildCardRecordAsync(User);
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    private string GetFieldText(string key, string? fallback = null)
    {
        if (CardRecord?.Fields != null)
        {
            var f = CardRecord.Fields.FirstOrDefault(x => string.Equals(x.LabelKey, key, StringComparison.OrdinalIgnoreCase));
            if (f != null && !string.IsNullOrWhiteSpace(f.Text)) return f.Text;
        }
        return fallback ?? string.Empty;
    }
    private bool GetFieldBoolean(string key, bool? fallback = null)
    {
        if (CardRecord?.Fields != null)
        {
            var f = CardRecord.Fields.FirstOrDefault(x => string.Equals(x.LabelKey, key, StringComparison.OrdinalIgnoreCase));
            if (f != null && f.BoolValue.HasValue) return f.BoolValue.Value;
        }
        return fallback ?? false;
    }

    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        if (User == null) return false;
        try
        {
            var preferredLang = User.PreferredLanguage ?? string.Empty;
            var createUser = UserId == Guid.Empty;

            // Pick up edited username from CardRecord (apply pending UI changes)
            var usernameToUse = GetFieldText("Card_Caption_User_Username", User.Username);
            // Pick up admin flag from CardRecord (editable during creation)
            var isAdminToUse = GetFieldBoolean("Card_Caption_User_IsAdmin", User.IsAdmin);

            if (createUser)
            {
                // Create user - password is generated here (will be shown/hinted elsewhere if needed)
                var created = await _api.Admin_CreateUserAsync(new CreateUserRequest(usernameToUse, Guid.NewGuid().ToString(), isAdminToUse), ct);
                if (created == null)
                    return false;
                User = created;
                CardRecord = await BuildCardRecordAsync(User);
            }

            // Update (or update after create)
            var updated = await _api.Admin_UpdateUserAsync(User.Id, new UpdateUserRequest(usernameToUse, isAdminToUse, User.Active, preferredLang), ct);
            if (updated == null)
                return false;
            User = updated;
            CardRecord = await BuildCardRecordAsync(User);
            if (createUser)
                RaiseUiActionRequested("Saved", User.Id.ToString());
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        return false;
    }

    public async Task<bool> DeleteAsync(CancellationToken ct = default)
    {
        if (User == null) return false;
        try
        {
            var ok = await _api.Admin_DeleteUserAsync(User.Id, ct);
            if (ok) RaiseUiActionRequested("Deleted");
            return ok;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    public async Task<bool> EnableAsync(CancellationToken ct = default)
    {
        if (User == null) return false;
        try
        {
            var preferredLang = User.PreferredLanguage ?? string.Empty;
            var updated = await _api.Admin_UpdateUserAsync(User.Id, new UpdateUserRequest(User.Username, User.IsAdmin, true, preferredLang), ct);
            if (updated != null) { User = updated; CardRecord = await BuildCardRecordAsync(User); RaiseStateChanged(); return true; }
            return false;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    public async Task<bool> DisableAsync(CancellationToken ct = default)
    {
        if (User == null) return false;
        try
        {
            var preferredLang = User.PreferredLanguage ?? string.Empty;
            var updated = await _api.Admin_UpdateUserAsync(User.Id, new UpdateUserRequest(User.Username, User.IsAdmin, false, preferredLang), ct);
            if (updated != null) { User = updated; CardRecord = await BuildCardRecordAsync(User); RaiseStateChanged(); return true; }
            return false;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    public async Task<bool> UnblockAsync(CancellationToken ct = default)
    {
        if (User == null) return false;
        try
        {
            var ok = await _api.Admin_UnlockUserAsync(User.Id, ct);
            if (ok) await LoadAsync(User.Id, ct);
            return ok;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    // BaseCardViewModel abstract implementations
    public override async Task LoadAsync(Guid id)
    {
        await LoadAsync(id, CancellationToken.None);
    }

    protected override bool IsSymbolUploadAllowed()
    {
        // Users do not support symbol attachments in current model
        return false;
    }

    protected override (AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent()
    {
        return (AttachmentEntityKind.None, Guid.Empty);
    }

    protected override async Task AssignNewSymbolAsync(Guid? attachmentId)
    {
        // No-op for users; swallow exceptions
        await Task.CompletedTask;
    }

    private static string FormatDate(object? val)
    {
        if (val is DateTime dt)
            if (dt == DateTime.MinValue)
                return "";
            else 
                return dt.ToString("u");
        return "";
    }

    private async Task<CardRecord> BuildCardRecordAsync(UserAdminDto? u)
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        var fields = new List<CardField>
        {
            // Username: editable only when creating (UserId == Guid.Empty)
            new CardField("Card_Caption_User_Username", CardFieldKind.Text, text: u?.Username ?? string.Empty, editable: (UserId == Guid.Empty)),
            // Admin flag: editable when creating, read-only for existing users
            new CardField("Card_Caption_User_IsAdmin", CardFieldKind.Boolean, boolValue: u?.IsAdmin, text: u != null && u.IsAdmin ? "✓" : "", editable: (UserId == Guid.Empty)),
            // Active flag: not editable
            new CardField("Card_Caption_User_Active", CardFieldKind.Boolean, boolValue: u?.Active, text: u != null && u.Active ? "✓" : "", editable: false),
            // Locked until / blocked info
            new CardField("Card_Caption_User_LockedUntil", CardFieldKind.Text, text: FormatDate(u == null ? null : (object?) (u.LockoutEnd)), editable: false),
            // Last login
            new CardField("Card_Caption_User_LastLogin", CardFieldKind.Text, text: FormatDate(u == null ? null : (object?) (u.LastLoginUtc)), editable: false)
        };

        var record = new CardRecord(fields, u);
        return ApplyPendingValues(record);
    }

    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
        };

        var manage = new List<UiRibbonAction>
        {
            new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>", UiRibbonItemSize.Large, User != null && User.Id != Guid.Empty, null, "Save", async () => { await SaveAsync(); }),
            new UiRibbonAction("Delete", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#delete'/></svg>", UiRibbonItemSize.Small, !(UserId != Guid.Empty && User != null), null, "Delete", async () => { await DeleteAsync(); })
        };

        // Activate/Deactivate
        if (User != null)
        {
            if (User.IsAdmin == false) // don't allow toggling admin flag; we toggle active state
            {
                if (User.Active)
                {
                    manage.Add(new UiRibbonAction("Deactivate", localizer["Ribbon_Deactivate"].Value, "<svg><use href='/icons/sprite.svg#archive'/></svg>", UiRibbonItemSize.Small, !(UserId != Guid.Empty && User != null), null, "Deactivate", async () => { await DisableAsync(); }));
                }
                else
                {
                    manage.Add(new UiRibbonAction("Activate", localizer["Ribbon_Activate"].Value, "<svg><use href='/icons/sprite.svg#check'/></svg>", UiRibbonItemSize.Small, !(UserId != Guid.Empty && User != null), null, "Activate", async () => { await EnableAsync(); }));
                }
            }

            // Unblock if locked
            if (User.LockoutEnd != null && User.LockoutEnd > DateTime.UtcNow)
            {
                manage.Add(new UiRibbonAction("Unblock", localizer["Ribbon_Unblock"].Value, "<svg><use href='/icons/sprite.svg#unlock'/></svg>", UiRibbonItemSize.Small, !(UserId != Guid.Empty && User != null), null, "Unblock", async () => { await UnblockAsync(); }));
            }

            // Set password action for existing users
            if (UserId != Guid.Empty && User != null)
            {
                var specParams = new Dictionary<string, object?>
                {
                    ["UserId"] = User.Id,
                    ["OverlayTitle"] = localizer["Users_SetPassword_Title"].Value
                };
                manage.Add(new UiRibbonAction("SetPassword", localizer["Ribbon_SetPassword"].Value, "<svg><use href='/icons/sprite.svg#key'/></svg>", UiRibbonItemSize.Small, false, null, "SetPassword", () => { RaiseUiActionRequested("SetPassword", new BaseViewModel.UiOverlaySpec(typeof(SetPasswordOverlay), specParams)); return Task.CompletedTask; }));
            }
        }

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, actions),
            new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, manage)
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}
