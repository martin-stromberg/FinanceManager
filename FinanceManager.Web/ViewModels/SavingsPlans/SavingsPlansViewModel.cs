using FinanceManager.Shared;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.SavingsPlans;

public sealed class SavingsPlansViewModel : ViewModelBase
{
    private readonly IApiClient _api;

    public SavingsPlansViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    public bool Loaded { get; private set; }
    public bool IsAuthenticated => base.IsAuthenticated;

    public bool ShowActiveOnly { get; private set; } = true;
    public List<SavingsPlanDto> Plans { get; private set; } = new();

    private readonly Dictionary<Guid, SavingsPlanAnalysisDto> _analysisByPlan = new();
    private readonly Dictionary<Guid, Guid?> _displaySymbolByPlan = new();

    // Ribbon: provide registers/tabs/actions via the new provider API
    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Actions"], new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "New",
                    Label: localizer["BtnNew"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#plus'/></svg>",
                    Size: UiRibbonItemSize.Large,
                    Disabled: false,
                    Tooltip: null,
                    Action: "New",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("New"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "Categories",
                    Label: localizer["Ribbon_Categories"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#groups'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Categories",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Categories"); return Task.CompletedTask; })
                )
            }),

            new UiRibbonTab(localizer["Ribbon_Group_Filter"], new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "ToggleActive",
                    Label: ShowActiveOnly ? localizer["OnlyActive"] : localizer["StatusArchived"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "ToggleActive",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("ToggleActive"); return Task.CompletedTask; })
                )
            })
        };

        var registers = new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        var baseRegs = base.GetRibbonRegisters(localizer);
        if (baseRegs != null) registers.AddRange(baseRegs);
        return registers;
    }

    public void ToggleActiveOnly()
    {
        ShowActiveOnly = !ShowActiveOnly;
        _ = InitializeAsync();
        RaiseStateChanged();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadPlansAsync(ct);
        Loaded = true;
        RaiseStateChanged();
    }

    private async Task LoadPlansAsync(CancellationToken ct)
    {
        Plans.Clear();
        _analysisByPlan.Clear();
        _displaySymbolByPlan.Clear();

        try
        {
            var list = await _api.SavingsPlans_ListAsync(ShowActiveOnly, ct);
            Plans = list.ToList();
        }
        catch
        {
            return;
        }

        // Load category symbols to use as fallback
        var categorySymbolMap = new Dictionary<Guid, Guid?>();
        try
        {
            var clist = await _api.SavingsPlanCategories_ListAsync(ct);
            foreach (var c in clist)
            {
                if (c.Id != Guid.Empty)
                {
                    categorySymbolMap[c.Id] = c.SymbolAttachmentId;
                }
            }
        }
        catch { }

        // Build display symbol mapping per plan (plan symbol -> contact/category fallback)
        foreach (var p in Plans)
        {
            Guid? display = null;
            if (p.SymbolAttachmentId.HasValue)
            {
                display = p.SymbolAttachmentId;
            }
            else if (p.CategoryId.HasValue && categorySymbolMap.TryGetValue(p.CategoryId.Value, out var catSym) && catSym.HasValue)
            {
                display = catSym;
            }
            _displaySymbolByPlan[p.Id] = display;
        }

        await LoadAnalysesAsync(ct);
    }

    private async Task LoadAnalysesAsync(CancellationToken ct)
    {
        _analysisByPlan.Clear();
        if (Plans.Count == 0) { return; }
        var tasks = Plans.Select(async p =>
        {
            try
            {
                var dto = await _api.SavingsPlans_AnalyzeAsync(p.Id, ct);
                if (dto != null) { _analysisByPlan[p.Id] = dto; }
            }
            catch { }
        });
        await Task.WhenAll(tasks);
    }

    public string GetStatusLabel(IStringLocalizer localizer, SavingsPlanDto plan)
    {
        var state = GetState(plan);
        return state switch
        {
            PlanState.Done => localizer["StatusDone"],
            PlanState.Unreachable => localizer["StatusUnreachable"],
            _ => plan.IsActive ? localizer["StatusActive"] : localizer["StatusArchived"],
        };
    }

    public (bool Reachable, bool Unreachable) GetStatusFlags(SavingsPlanDto plan)
    {
        var s = GetState(plan);
        return (s == PlanState.Done, s == PlanState.Unreachable);
    }

    private PlanState GetState(SavingsPlanDto plan)
    {
        if (!_analysisByPlan.TryGetValue(plan.Id, out var a) || a.TargetAmount is null || a.TargetDate is null)
        {
            return PlanState.Normal;
        }
        return a.TargetReachable ? PlanState.Done : PlanState.Unreachable;
    }

    private enum PlanState { Normal, Done, Unreachable }

    // Public helper for UI to get the display symbol attachment id (plan symbol or category fallback)
    public Guid? GetDisplaySymbolAttachmentId(SavingsPlanDto plan)
    {
        if (plan == null) return null;
        return _displaySymbolByPlan.TryGetValue(plan.Id, out var v) ? v : null;
    }

    // Public helpers to expose analysis values for the UI
    public decimal? GetAccumulatedAmount(SavingsPlanDto plan)
    {
        if (plan == null) return null;
        return _analysisByPlan.TryGetValue(plan.Id, out var a) ? a.AccumulatedAmount : (decimal?)null;
    }

    public decimal? GetRemainingAmount(SavingsPlanDto plan)
    {
        if (plan == null) return null;
        if (plan.Type == SavingsPlanType.Open) return null;
        if (!_analysisByPlan.TryGetValue(plan.Id, out var a)) return null;
        if (a.TargetAmount is null) return null;
        return a.TargetAmount.Value - a.AccumulatedAmount;
    }

    // New: determine completed / reachable / unreachable semantics for UI
    public bool IsCompleted(SavingsPlanDto plan)
    {
        if (plan == null) return false;
        if (plan.Type == SavingsPlanType.Open) return false;
        if (!_analysisByPlan.TryGetValue(plan.Id, out var a)) return false;
        if (a.TargetAmount is null) return false;
        // completed when accumulated already reached or exceeded target
        return a.AccumulatedAmount >= a.TargetAmount.Value;
    }

    public bool IsReachableButNotCompleted(SavingsPlanDto plan)
    {
        if (plan == null) return false;
        if (!_analysisByPlan.TryGetValue(plan.Id, out var a)) return false;
        if (a.TargetAmount is null) return false;
        return a.TargetReachable && a.AccumulatedAmount < a.TargetAmount.Value;
    }

    public bool IsUnreachable(SavingsPlanDto plan)
    {
        if (plan == null) return false;
        if (!_analysisByPlan.TryGetValue(plan.Id, out var a)) return false;
        if (a.TargetAmount is null) return false;
        return !a.TargetReachable && a.AccumulatedAmount < a.TargetAmount.Value;
    }

    public bool IsOverdue(SavingsPlanDto plan)
    {
        if (plan == null) return false;
        if (!_analysisByPlan.TryGetValue(plan.Id, out var a)) return false;
        if (plan.TargetDate is null) return false;
        if (plan.TargetDate == DateTime.MinValue) return false;
        if (plan.TargetDate > DateTime.Now) return false;
        var remainingAmount = GetRemainingAmount(plan) ?? 0;
        if (remainingAmount <= 0) return false;
        return true;
    }
}
