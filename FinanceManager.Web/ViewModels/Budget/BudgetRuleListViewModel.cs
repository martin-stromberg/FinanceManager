using System.Globalization;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Embedded list view model for budget rules belonging to a specific budget purpose.
/// </summary>
public sealed class BudgetRuleListViewModel : BaseListViewModel<BudgetRuleListItem>
{
    private readonly IApiClient _api;

    /// <summary>
    /// Budget purpose id for which rules are listed.
    /// </summary>
    public Guid? BudgetPurposeId { get; private set; }

    /// <summary>
    /// Budget category id for which rules are listed.
    /// </summary>
    public Guid? BudgetCategoryId { get; private set; }

    /// <summary>
    /// Whether a usable parent context (purpose or category) exists. Without one the list stays
    /// empty and the New action is disabled — a rule cannot be created without its parent.
    /// </summary>
    /// <returns><c>true</c> when a purpose or category id is set; otherwise <c>false</c>.</returns>
    private bool HasContext
        => (BudgetPurposeId ?? Guid.Empty) != Guid.Empty
            || (BudgetCategoryId ?? Guid.Empty) != Guid.Empty;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetRuleListViewModel(IServiceProvider services) : base(services)
    {
        _api = services.GetRequiredService<IApiClient>();
        AllowRangeFiltering = false;
        AllowSearchFiltering = false;
    }

    /// <summary>
    /// Sets the budget purpose id and triggers loading.
    /// </summary>
    /// <param name="budgetPurposeId">The budget purpose id.</param>
    public async Task InitializeForPurposeAsync(Guid budgetPurposeId)
    {
        BudgetPurposeId = budgetPurposeId;
        BudgetCategoryId = null;
        await base.InitializeAsync();
    }

    /// <summary>
    /// Sets the budget category id and triggers loading.
    /// </summary>
    /// <param name="budgetCategoryId">The budget category id.</param>
    public async Task InitializeForCategoryAsync(Guid budgetCategoryId)
    {
        BudgetCategoryId = budgetCategoryId;
        BudgetPurposeId = null;
        await base.InitializeAsync();
    }

    /// <summary>
    /// Loads the list content.
    /// </summary>
    /// <param name="resetPaging">Ignored for this embedded list (no paging).</param>
    protected override async Task LoadPageAsync(bool resetPaging)
    {
        // No paging for embedded list right now.
        CanLoadMore = false;

        if (!HasContext)
        {
            Items.Clear();
            return;
        }

        try
        {
            IReadOnlyList<BudgetRuleDto>? list;

            if (BudgetPurposeId.HasValue && BudgetPurposeId.Value != Guid.Empty)
            {
                list = await _api.Budgets_ListRulesByPurposeAsync(BudgetPurposeId.Value);
            }
            else
            {
                list = await _api.Budgets_ListRulesByCategoryAsync(BudgetCategoryId.GetValueOrDefault());
            }

            Items.Clear();

            foreach (var r in list ?? Array.Empty<BudgetRuleDto>())
            {
                Items.Add(new BudgetRuleListItem(
                    r.Id,
                    r.Interval.ToString(),
                    r.Amount.ToString("C", CultureInfo.CurrentUICulture),
                    r.StartDate.ToString("d", CultureInfo.CurrentUICulture),
                    r.EndDate?.ToString("d", CultureInfo.CurrentUICulture) ?? string.Empty,
                    r.PurposePattern));
            }
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
        }
        finally
        {
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Builds column definitions and list records.
    /// </summary>
    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();

        Columns = new List<ListColumn>
        {
            new ListColumn("interval", L["List_Th_Interval"], "20%", ListColumnAlign.Left),
            new ListColumn("amount", L["List_Th_Amount"], "20%", ListColumnAlign.Right),
            new ListColumn("purpose", L["List_Th_BudgetRule_Purpose"], "30%", ListColumnAlign.Left),
            new ListColumn("start", L["List_Th_Start"], "15%", ListColumnAlign.Left),
            new ListColumn("end", L["List_Th_End"], "15%", ListColumnAlign.Left),
        };

        if (!HasContext)
        {
            // muted hint row so the reason for the disabled New action is visible on every
            // device — a tooltip on a disabled button is unreachable via touch and keyboard.
            Records = new List<ListRecord>
            {
                new ListRecord(
                    new List<ListCell>
                    {
                        new ListCell(ListCellKind.Text, Text: L["Budget_Rule_New_SaveFirst"].Value, Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true)
                    },
                    Item: null)
            };
            return;
        }

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: i.Interval),
            new ListCell(ListCellKind.Text, Text: i.Amount),
            new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.PurposePattern) ? "—" : i.PurposePattern),
            new ListCell(ListCellKind.Text, Text: i.Start),
            new ListCell(ListCellKind.Text, Text: i.End)
        }, i)).ToList();
    }

    /// <inheritdoc />
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var noContext = !HasContext;

        var tab = new UiRibbonTab(localizer["Ribbon_Group_Rules"], new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "NewRule",
                localizer["Ribbon_New"].Value,
                "<svg><use href='icons/sprite.svg#plus'/></svg>",
                UiRibbonItemSize.Large,
                noContext,
                noContext ? localizer["Budget_Rule_New_SaveFirst"].Value : null,
                () =>
                {
                    var payload = BudgetPurposeId is { } pid && pid != Guid.Empty
                        ? $"purpose,{pid}"
                        : BudgetCategoryId is { } cid && cid != Guid.Empty
                            ? $"category,{cid}"
                            : null;
                    if (payload is null)
                    {
                        return Task.CompletedTask;
                    }

                    RaiseUiActionRequested("NewRule", payload);
                    return Task.CompletedTask;
                })
        }, Sort: 60);

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { tab })
        };
    }
}
