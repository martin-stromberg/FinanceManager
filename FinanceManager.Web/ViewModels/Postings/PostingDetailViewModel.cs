using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Postings;

public sealed class PostingDetailViewModel : ViewModelBase
{
    private readonly Shared.IApiClient _api;

    public PostingDetailViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public Guid Id { get; private set; }

    public bool Loading { get; private set; }
    public bool LinksLoading { get; private set; }

    public PostingServiceDto? Detail { get; private set; }

    public Guid? LinkedAccountId { get; private set; }
    public Guid? LinkedContactId { get; private set; }
    public Guid? LinkedPlanId { get; private set; }
    public Guid? LinkedSecurityId { get; private set; }

    public void Configure(Guid id)
    {
        Id = id;
    }

    public override async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            RequireAuthentication(null);
            return;
        }
        await LoadAsync(ct);
        RaiseStateChanged();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (Loading) { return; }
        Loading = true; RaiseStateChanged();
        try
        {
            var dto = await _api.Postings_GetByIdAsync(Id, ct);
            Detail = dto;
            if (Detail != null)
            {
                await ResolveGroupLinksAsync(ct);
            }
        }
        catch { }
        finally { Loading = false; RaiseStateChanged(); }
    }

    private async Task ResolveGroupLinksAsync(CancellationToken ct)
    {
        LinkedAccountId = Detail?.AccountId;
        LinkedContactId = Detail?.ContactId;
        LinkedPlanId = Detail?.SavingsPlanId;
        LinkedSecurityId = Detail?.SecurityId;
        if (Detail == null || Detail.GroupId == Guid.Empty) { return; }
        try
        {
            LinksLoading = true; RaiseStateChanged();
            var dto = await _api.Postings_GetGroupLinksAsync(Detail.GroupId, ct);
            if (dto != null)
            {
                LinkedAccountId = dto.AccountId ?? LinkedAccountId;
                LinkedContactId = dto.ContactId ?? LinkedContactId;
                LinkedPlanId = dto.SavingsPlanId ?? LinkedPlanId;
                LinkedSecurityId = dto.SecurityId ?? LinkedSecurityId;
            }
        }
        catch { }
        finally { LinksLoading = false; RaiseStateChanged(); }
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        // Navigation group
        var nav = new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "Back",
                localizer["Ribbon_Back"].Value,
                "<svg><use href='/icons/sprite.svg#back'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                "Back",
                new Func<Task>(async () => { RaiseUiActionRequested("Back"); await Task.CompletedTask; }))
        });

        // Linked group - navigation requires page to perform (navigation), so raise UI action
        var linked = new UiRibbonTab(localizer["Ribbon_Group_Linked"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "OpenAccount",
                localizer["Ribbon_OpenAccount"].Value,
                "<svg><use href='/icons/sprite.svg#external'/></svg>",
                UiRibbonItemSize.Small,
                LinksLoading || !HasId(LinkedAccountId),
                null,
                "OpenAccount",
                new Func<Task>(async () => { RaiseUiActionRequested("OpenAccount"); await Task.CompletedTask; })),
            new UiRibbonAction(
                "OpenContact",
                localizer["Ribbon_OpenContact"].Value,
                "<svg><use href='/icons/sprite.svg#external'/></svg>",
                UiRibbonItemSize.Small,
                LinksLoading || !HasId(LinkedContactId),
                null,
                "OpenContact",
                new Func<Task>(async () => { RaiseUiActionRequested("OpenContact"); await Task.CompletedTask; })),
            new UiRibbonAction(
                "OpenSavingsPlan",
                localizer["Ribbon_OpenSavingsPlan"].Value,
                "<svg><use href='/icons/sprite.svg#external'/></svg>",
                UiRibbonItemSize.Small,
                LinksLoading || !HasId(LinkedPlanId),
                null,
                "OpenSavingsPlan",
                new Func<Task>(async () => { RaiseUiActionRequested("OpenSavingsPlan"); await Task.CompletedTask; })),
            new UiRibbonAction(
                "OpenSecurity",
                localizer["Ribbon_OpenSecurity"].Value,
                "<svg><use href='/icons/sprite.svg#external'/></svg>",
                UiRibbonItemSize.Small,
                LinksLoading || !HasId(LinkedSecurityId),
                null,
                "OpenSecurity",
                new Func<Task>(async () => { RaiseUiActionRequested("OpenSecurity"); await Task.CompletedTask; })),
            new UiRibbonAction(
                "OpenAttachments",
                localizer["Ribbon_Attachments"].Value,
                "<svg><use href='/icons/sprite.svg#attachment'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                "OpenAttachments",
                new Func<Task>(async () => { RaiseUiActionRequested("OpenAttachments"); await Task.CompletedTask; }))
        });

        var registers = new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { nav, linked })
        };
        return registers;
    }

    private static bool HasId(Guid? id) => id.HasValue && id.Value != Guid.Empty;
}
