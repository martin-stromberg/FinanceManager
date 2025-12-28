using FinanceManager.Shared.Dtos.Users;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using static FinanceManager.Web.ViewModels.Setup.UserListViewModel;

namespace FinanceManager.Web.ViewModels.Setup;

public record UserListItem(Guid Id, string Username, bool IsAdmin, bool Active, DateTime? LockoutEnd, DateTime? LastLoginUtc) : IListItemNavigation
{
    public string GetNavigateUrl() => $"/card/users/{Id}";
}
public sealed class UserListViewModel : BaseListViewModel<UserListItem>
{

    public UserListViewModel(IServiceProvider sp)
        : base(sp)
    {
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated) return;
        try
        {
            var list = await ApiClient.Admin_ListUsersAsync(ct);
            Items.Clear();
            if (list != null)
            {
                Items.AddRange(list.Select(u => new UserListItem(u.Id, u.Username, u.IsAdmin, u.Active, u.LockoutEnd, u.LastLoginUtc)));
            }
        }
        catch
        {
            Items.Clear();
        }
        BuildRecords();
        RaiseStateChanged();
    }

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        // The generic list provider expects paging support; for users we load full list and disable paging
        await LoadAsync(CancellationToken.None);
        CanLoadMore = false;
    }

    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("username", L["UserList_Th_Username"].Value ?? "Username", "", ListColumnAlign.Left),
            new ListColumn("admin", L["UserList_Th_Admin"].Value ?? "Admin", "80px", ListColumnAlign.Left),
            new ListColumn("active", L["UserList_Th_Active"].Value ?? "Active", "80px", ListColumnAlign.Left),
            new ListColumn("lockedUntil", L["UserList_Th_LockedUntil"].Value ?? "LockedUntil", "160px", ListColumnAlign.Left),
            new ListColumn("lastLogin", L["UserList_Th_LastLogin"].Value ?? "LastLogin", "160px", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: i.Username),
            new ListCell(ListCellKind.Text, Text: i.IsAdmin ? (L["Value_Yes"].Value ?? "Yes") : (L["Value_No"].Value ?? "No")),
            new ListCell(ListCellKind.Text, Text: i.Active ? (L["Value_Yes"].Value ?? "Yes") : (L["Value_No"].Value ?? "No")),
            new ListCell(ListCellKind.Text, Text: i.LockoutEnd?.ToString("u") ?? "-"),
            new ListCell(ListCellKind.Text, Text: i.LastLoginUtc?.ToString("u") ?? "-")
        }, i)).ToList();
    }

    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "New",
                localizer["Ribbon_New"].Value,
                "<svg><use href='/icons/sprite.svg#plus'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                "New",
                new Func<Task>(() => { RaiseUiActionRequested("New"); return Task.CompletedTask; })
            )
        };

        var tabs = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, actions) };
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}
