namespace FinanceManager.Web.ViewModels.Postings;

public sealed class PostingsAccountViewModel : ViewModelBase
{
    private readonly Shared.IApiClient _api;

    public PostingsAccountViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public Guid AccountId { get; private set; }
    public bool Loaded { get; private set; }

    public string Search { get; private set; } = string.Empty;
    public DateTime? From { get; private set; }
    public DateTime? To { get; private set; }

    public bool Loading { get; private set; }
    public bool CanLoadMore { get; private set; } = true;
    public int Skip { get; private set; }

    public Guid? SelectedPostingId { get; private set; }
    public Guid? LinkedAccountId { get; private set; }
    public Guid? LinkedContactId { get; private set; }
    public Guid? LinkedPlanId { get; private set; }
    public Guid? LinkedSecurityId { get; private set; }

    public List<PostingItem> Items { get; } = new();

    public void Configure(Guid accountId)
    {
        AccountId = accountId;
    }

    public override async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            RequireAuthentication(null);
            return;
        }
        await LoadMoreAsync(ct);
        Loaded = true;
        RaiseStateChanged();
    }

    public void SetSearch(string search)
    {
        if (Search != search)
        {
            Search = search ?? string.Empty;
        }
    }

    public void SetRange(DateTime? from, DateTime? to)
    {
        From = from; To = to;
    }

    public void ResetAndSearch()
    {
        Items.Clear();
        Skip = 0; CanLoadMore = true; SelectedPostingId = null;
        LinkedAccountId = LinkedContactId = LinkedPlanId = LinkedSecurityId = null;
        RaiseStateChanged();
    }

    public async Task LoadMoreAsync(CancellationToken ct = default)
    {
        if (Loading || !CanLoadMore) { return; }
        Loading = true; RaiseStateChanged();
        try
        {
            var firstPage = Skip == 0;
            var chunk = await _api.Postings_GetAccountAsync(AccountId, Skip, 50, Search, From, To, ct);
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

    public void ClearRange()
    {
        From = null; To = null;
        ResetAndSearch();
    }

    // Selection retained internal but not exposed via ribbon anymore
    public void Select(Guid id)
    {
        if (SelectedPostingId == id)
        {
            SelectedPostingId = null;
            LinkedAccountId = LinkedContactId = LinkedPlanId = LinkedSecurityId = null;
            RaiseStateChanged();
            return;
        }
        SelectedPostingId = id;
        _ = ResolveSelectedLinksAsync();
    }

    private async Task ResolveSelectedLinksAsync()
    {
        LinkedAccountId = LinkedContactId = LinkedPlanId = LinkedSecurityId = null;
        var sel = Items.FirstOrDefault(i => i.Id == SelectedPostingId);
        if (sel == null) { return; }
        if (sel.GroupId == Guid.Empty)
        {
            LinkedAccountId = sel.AccountId; LinkedContactId = sel.ContactId; LinkedPlanId = sel.SavingsPlanId; LinkedSecurityId = sel.SecurityId; RaiseStateChanged(); return;
        }
        try
        {
            var dto = await _api.Postings_GetGroupLinksAsync(sel.GroupId, CancellationToken);
            LinkedAccountId = dto?.AccountId; LinkedContactId = dto?.ContactId; LinkedPlanId = dto?.SavingsPlanId; LinkedSecurityId = dto?.SecurityId;
        }
        catch { }
        finally { RaiseStateChanged(); }
    }

    public string GetExportUrl(string format)
    {
        var parts = new List<string> { $"format={Uri.EscapeDataString(format)}" };
        if (!string.IsNullOrWhiteSpace(Search)) { parts.Add($"q={Uri.EscapeDataString(Search)}"); }
        if (From.HasValue) { parts.Add($"from={From:yyyy-MM-dd}"); }
        if (To.HasValue) { parts.Add($"to={To:yyyy-MM-dd}"); }
        var qs = parts.Count > 0 ? ("?" + string.Join('&', parts)) : string.Empty;
        return $"/api/postings/account/{AccountId}/export{qs}";
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>();
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", new Func<Task>(()=>{ RaiseUiActionRequested("Back"); return Task.CompletedTask; }))
        }));

        var filterItems = new List<UiRibbonAction>
        {
            new UiRibbonAction("ClearSearch", localizer["Ribbon_ClearSearch"].Value, "<svg><use href='/icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, string.IsNullOrWhiteSpace(Search), null, "ClearSearch", new Func<Task>(()=>{ RaiseUiActionRequested("ClearSearch"); return Task.CompletedTask; }))
        };
        if (From.HasValue || To.HasValue)
        {
            filterItems.Add(new UiRibbonAction("ClearRange", localizer["Ribbon_ClearRange"].Value, "<svg><use href='/icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, false, null, "ClearRange", new Func<Task>(() => { RaiseUiActionRequested("ClearRange"); return Task.CompletedTask; })));
        }
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Filter"].Value, filterItems));

        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Export"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction("ExportCsv", localizer["Ribbon_ExportCsv"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, Loading, null, "ExportCsv", new Func<Task>(()=>{ RaiseUiActionRequested("ExportCsv"); return Task.CompletedTask; })),
            new UiRibbonAction("ExportXlsx", localizer["Ribbon_ExportExcel"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, Loading, null, "ExportXlsx", new Func<Task>(()=>{ RaiseUiActionRequested("ExportXlsx"); return Task.CompletedTask; }))
        }));

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
