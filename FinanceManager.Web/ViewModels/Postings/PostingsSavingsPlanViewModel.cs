using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Postings;

public sealed class PostingsSavingsPlanViewModel : ViewModelBase
{
    private readonly Shared.IApiClient _api;

    public PostingsSavingsPlanViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public Guid PlanId { get; private set; }

    public string Search { get; private set; } = string.Empty;

    public bool Loading { get; private set; }
    public bool CanLoadMore { get; private set; } = true;
    public int Skip { get; private set; }

    public List<PostingItem> Items { get; } = new();

    public void Configure(Guid planId)
    {
        PlanId = planId;
    }

    public override async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            RequireAuthentication(null);
            return;
        }
        await LoadMoreAsync(ct);
        RaiseStateChanged();
    }

    public void SetSearch(string search)
    {
        if (Search != search)
        {
            Search = search ?? string.Empty;
        }
    }

    public void ResetAndSearch()
    {
        Items.Clear();
        Skip = 0; CanLoadMore = true;
        RaiseStateChanged();
    }

    public async Task LoadMoreAsync(CancellationToken ct = default)
    {
        if (Loading || !CanLoadMore) { return; }
        Loading = true; RaiseStateChanged();
        try
        {
            var firstPage = Skip == 0;
            var chunk = await _api.Postings_GetSavingsPlanAsync(PlanId, Skip, 50, null, null, Search, ct);
            var list = chunk ?? Array.Empty<PostingServiceDto>();
            Items.AddRange(list.Select(Map));
            Skip += list.Count;
            if (list.Count == 0 || (!firstPage && list.Count < 50)) { CanLoadMore = false; }
        }
        catch { }
        finally { Loading = false; RaiseStateChanged(); }
    }

    public void ClearSearch()
    {
        Search = string.Empty;
        ResetAndSearch();
    }

    public string GetExportUrl(string format)
    {
        var parts = new List<string> { $"format={Uri.EscapeDataString(format)}" };
        if (!string.IsNullOrWhiteSpace(Search)) { parts.Add($"q={Uri.EscapeDataString(Search)}"); }
        var qs = parts.Count > 0 ? ("?" + string.Join('&', parts)) : string.Empty;
        return $"/api/postings/savings-plan/{PlanId}/export{qs}";
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>();

        var nav = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", null)
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, nav));

        var filter = new List<UiRibbonAction>
        {
            new UiRibbonAction("ClearSearch", localizer["Ribbon_ClearSearch"].Value, "<svg><use href='/icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, string.IsNullOrWhiteSpace(Search), null, "ClearSearch", null)
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Filter"].Value, filter));

        var export = new List<UiRibbonAction>
        {
            new UiRibbonAction("ExportCsv", localizer["Ribbon_ExportCsv"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, Loading, null, "ExportCsv", null),
            new UiRibbonAction("ExportXlsx", localizer["Ribbon_ExportExcel"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, Loading, null, "ExportXlsx", null)
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Export"].Value, export));

        var registers = new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        var baseRegs = base.GetRibbonRegisters(localizer);
        if (baseRegs != null) registers.AddRange(baseRegs);
        return registers.Count == 0 ? null : registers;
    }

    private static PostingItem Map(PostingServiceDto p) => new()
    {
        Id = p.Id,
        BookingDate = p.BookingDate,
        ValutaDate = p.ValutaDate,
        Amount = p.Amount,
        Kind = p.Kind,
        AccountId = p.AccountId,
        ContactId = p.ContactId,
        SavingsPlanId = p.SavingsPlanId,
        SecurityId = p.SecurityId,
        GroupId = p.GroupId,
        SourceId = p.SourceId,
        Subject = p.Subject,
        RecipientName = p.RecipientName,
        Description = p.Description,
        SecuritySubType = p.SecuritySubType,
        Quantity = p.Quantity
    };

    public sealed class PostingItem
    {
        public Guid Id { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime ValutaDate { get; set; }
        public decimal Amount { get; set; }
        public PostingKind Kind { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? ContactId { get; set; }
        public Guid? SavingsPlanId { get; set; }
        public Guid? SecurityId { get; set; }
        public Guid GroupId { get; set; }
        public Guid SourceId { get; set; }
        public string? Subject { get; set; }
        public string? RecipientName { get; set; }
        public string? Description { get; set; }
        public SecurityPostingSubType? SecuritySubType { get; set; }
        public decimal? Quantity { get; set; }
    }
}
