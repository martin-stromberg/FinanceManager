using FinanceManager.Shared; // IApiClient
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Web.ViewModels.Setup;

public sealed class UsersViewModel : ViewModelBase
{
    private readonly IApiClient _api;

    public UsersViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    // State
    public bool Loaded { get; private set; }
    public string? Error { get; set; }
    public bool BusyCreate { get; private set; }
    public bool BusyRow { get; private set; }

    public List<UserVm> Users { get; } = new();

    // Create form model
    public CreateVm Create { get; private set; } = new();

    // Edit buffer
    public UserVm? Edit { get; private set; }
    public string EditUsername { get; set; } = string.Empty;
    public bool EditIsAdmin { get; set; }
    public bool EditActive { get; set; }

    // Last reset info
    public Guid LastResetUserId { get; private set; }
    public string? LastResetPassword { get; private set; }

    public override async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        await LoadAsync(ct);
        Loaded = true;
        RaiseStateChanged();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            Error = null;
            var data = await _api.Admin_ListUsersAsync(ct);
            Users.Clear();
            if (data != null)
            {
                Users.AddRange(data.Select(d => new UserVm
                {
                    Id = d.Id,
                    Username = d.Username,
                    IsAdmin = d.IsAdmin,
                    Active = d.Active,
                    LockoutEnd = d.LockoutEnd,
                    LastLoginUtc = d.LastLoginUtc,
                    PreferredLanguage = d.PreferredLanguage
                }));
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        RaiseStateChanged();
    }

    public void BeginEdit(UserVm u)
    {
        Edit = u;
        EditUsername = u.Username;
        EditIsAdmin = u.IsAdmin;
        EditActive = u.Active;
        RaiseStateChanged();
    }

    public void CancelEdit()
    {
        Edit = null;
        RaiseStateChanged();
    }

    public void SetEditUsername(string value) { EditUsername = value; }
    public void SetEditIsAdmin(bool value) { EditIsAdmin = value; }
    public void SetEditActive(bool value) { EditActive = value; }

    public async Task SaveEditAsync(Guid id, CancellationToken ct = default)
    {
        if (Edit == null) { return; }
        BusyRow = true; Error = null; RaiseStateChanged();
        try
        {
            var req = new UpdateUserRequest(EditUsername, EditIsAdmin, EditActive, null);
            var updated = await _api.Admin_UpdateUserAsync(id, req, ct);
            if (updated != null)
            {
                var idx = Users.FindIndex(x => x.Id == id);
                if (idx >= 0)
                {
                    Users[idx] = new UserVm
                    {
                        Id = updated.Id,
                        Username = updated.Username,
                        IsAdmin = updated.IsAdmin,
                        Active = updated.Active,
                        LockoutEnd = updated.LockoutEnd,
                        LastLoginUtc = updated.LastLoginUtc,
                        PreferredLanguage = updated.PreferredLanguage
                    };
                }
                Edit = null;
            }
            else
            {
                Error = "NotFound";
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            BusyRow = false; RaiseStateChanged();
        }
    }

    public async Task CreateAsync(CancellationToken ct = default)
    {
        BusyCreate = true; Error = null; RaiseStateChanged();
        try
        {
            var req = new CreateUserRequest(Create.Username.Trim(), Create.Password, Create.IsAdmin);
            var created = await _api.Admin_CreateUserAsync(req, ct);
            if (created != null)
            {
                Users.Add(new UserVm
                {
                    Id = created.Id,
                    Username = created.Username,
                    IsAdmin = created.IsAdmin,
                    Active = created.Active,
                    LockoutEnd = created.LockoutEnd,
                    LastLoginUtc = created.LastLoginUtc,
                    PreferredLanguage = created.PreferredLanguage
                });
                Create = new();
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            BusyCreate = false; RaiseStateChanged();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        BusyRow = true; RaiseStateChanged();
        try
        {
            var ok = await _api.Admin_DeleteUserAsync(id, ct);
            if (ok)
            {
                Users.RemoveAll(u => u.Id == id);
            }
            else { Error = "NotFound"; }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { BusyRow = false; RaiseStateChanged(); }
    }

    public async Task ResetPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var newPw = Guid.NewGuid().ToString("N")[..12];
        BusyRow = true; RaiseStateChanged();
        try
        {
            var ok = await _api.Admin_ResetPasswordAsync(id, new ResetPasswordRequest(newPw), ct);
            if (ok) { LastResetUserId = id; LastResetPassword = newPw; }
            else { Error = "NotFound"; }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { BusyRow = false; RaiseStateChanged(); }
    }

    public async Task UnlockAsync(Guid id, CancellationToken ct = default)
    {
        BusyRow = true; RaiseStateChanged();
        try
        {
            var ok = await _api.Admin_UnlockUserAsync(id, ct);
            if (ok)
            {
                var found = Users.FirstOrDefault(x => x.Id == id);
                if (found != null) { found.LockoutEnd = null; }
            }
            else { Error = "NotFound"; }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { BusyRow = false; RaiseStateChanged(); }
    }

    public void ClearLastPassword()
    {
        LastResetUserId = Guid.Empty; LastResetPassword = null; RaiseStateChanged();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>();

        // Navigation group as a tab
        var navItems = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "back",
                localizer["Ribbon_Back"],
                "<svg><use href='/icons/sprite.svg#back'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                "Back",
                new Func<Task>(() => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
            )
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Navigation"], navItems));

        // Actions group as a tab
        var actionItems = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "reload",
                localizer["Ribbon_Reload"],
                "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                "Reload",
                new Func<Task>(() => { RaiseUiActionRequested("Reload"); return Task.CompletedTask; })
            )
        };

        if (Edit != null)
        {
            actionItems.Add(new UiRibbonAction(
                "cancelEdit",
                localizer["Ribbon_CancelEdit"],
                "<svg><use href='/icons/sprite.svg#clear'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                "CancelEdit",
                new Func<Task>(() => { RaiseUiActionRequested("CancelEdit"); return Task.CompletedTask; })
            ));
        }

        if (LastResetUserId != Guid.Empty && !string.IsNullOrEmpty(LastResetPassword))
        {
            actionItems.Add(new UiRibbonAction(
                "hidePassword",
                localizer["Ribbon_HidePassword"],
                "<svg><use href='/icons/sprite.svg#clear'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                "HidePassword",
                new Func<Task>(() => { RaiseUiActionRequested("HidePassword"); return Task.CompletedTask; })
            ));
        }

        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Actions"], actionItems));

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs)
        };
    }

    // DTOs
    public sealed class CreateVm
    {
        [Required, MinLength(3)] public string Username { get; set; } = string.Empty;
        [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
    public sealed class UserVm { public Guid Id { get; set; } public string Username { get; set; } = string.Empty; public bool IsAdmin { get; set; } public bool Active { get; set; } public DateTime? LockoutEnd { get; set; } public DateTime LastLoginUtc { get; set; } public string? PreferredLanguage { get; set; } }
}
