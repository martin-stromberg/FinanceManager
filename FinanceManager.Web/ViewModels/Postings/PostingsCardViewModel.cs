using FinanceManager.Shared;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Postings;

public sealed class PostingsCardViewModel : BaseCardViewModel<(string Key, string Value)>
{
    public PostingsCardViewModel(IServiceProvider sp) : base(sp) { }

    public Guid Id { get; private set; }
    public PostingServiceDto? Posting { get; private set; }

    public override string Title => Posting?.Subject ?? base.Title;

    public void Configure(Guid id) => Id = id;

    public override async Task LoadAsync(Guid id)
    {
        Id = id;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            if (id == Guid.Empty)
            {
                Posting = null;
                CardRecord = new CardRecord(new List<CardField>());
                return;
            }

            var api = ServiceProvider.GetRequiredService<IApiClient>();
            Posting = await api.Postings_GetByIdAsync(id);
            if (Posting == null)
            {
                SetError(api.LastErrorCode ?? null, api.LastError ?? "Posting not found");
                CardRecord = new CardRecord(new List<CardField>());
                return;
            }

            CardRecord = await BuildCardRecordsAsync(Posting);
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            CardRecord = new CardRecord(new List<CardField>());
        }
        finally
        {
            Loading = false; RaiseStateChanged();
        }
    }

    private Task<CardRecord> BuildCardRecordsAsync(PostingServiceDto p)
    {
        var fields = new List<CardField>
        {
            new CardField("Card_Caption_Posting_Date", CardFieldKind.Text, text: p.BookingDate.ToString("d")),
            new CardField("Card_Caption_Posting_Valuta", CardFieldKind.Text, text: p.ValutaDate.ToString("d")),
            new CardField("Card_Caption_Posting_Amount", CardFieldKind.Currency, amount: p.Amount),
            new CardField("Card_Caption_Posting_Kind", CardFieldKind.Text, text: (p.Kind == PostingKind.Security && p.SecuritySubType.HasValue) ? $"Security-{p.SecuritySubType}" : p.Kind.ToString()),
            new CardField("Card_Caption_Posting_Recipient", CardFieldKind.Text, text: p.RecipientName ?? string.Empty),
            new CardField("Card_Caption_Posting_Subject", CardFieldKind.Text, text: p.Subject ?? string.Empty),
            new CardField("Card_Caption_Posting_Description", CardFieldKind.Text, text: p.Description ?? string.Empty)
        };

        var record = new CardRecord(fields, p);
        return Task.FromResult(record);
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var kindLower = Posting?.Kind.ToString().ToLowerInvariant() switch
        {
            "bank" => $"account/{Posting.AccountId}",
            _ => Posting?.Kind.ToString().ToLowerInvariant()
        };
        var nav = new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back", kindLower); return Task.CompletedTask; })
        });

        var linked = new UiRibbonTab(localizer["Ribbon_Group_Linked"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction("OpenAccount", localizer["Ribbon_OpenAccount"].Value, "<svg><use href='/icons/sprite.svg#external'/></svg>", UiRibbonItemSize.Small, Posting?.AccountId == null, null, "OpenAccount", () => { RaiseUiActionRequested("OpenAccount"); return Task.CompletedTask; }),
            new UiRibbonAction("OpenContact", localizer["Ribbon_OpenContact"].Value, "<svg><use href='/icons/sprite.svg#external'/></svg>", UiRibbonItemSize.Small, Posting?.ContactId == null, null, "OpenContact", () => { RaiseUiActionRequested("OpenContact"); return Task.CompletedTask; }),
            new UiRibbonAction("OpenSavingsPlan", localizer["Ribbon_OpenSavingsPlan"].Value, "<svg><use href='/icons/sprite.svg#external'/></svg>", UiRibbonItemSize.Small, Posting?.SavingsPlanId == null, null, "OpenSavingsPlan", () => { RaiseUiActionRequested("OpenSavingsPlan"); return Task.CompletedTask; }),
            new UiRibbonAction("OpenSecurity", localizer["Ribbon_OpenSecurity"].Value, "<svg><use href='/icons/sprite.svg#external'/></svg>", UiRibbonItemSize.Small, Posting?.SecurityId == null, null, "OpenSecurity", () => { RaiseUiActionRequested("OpenSecurity"); return Task.CompletedTask; }),
            new UiRibbonAction("OpenAttachments", localizer["Ribbon_Attachments"].Value, "<svg><use href='/icons/sprite.svg#attachment'/></svg>", UiRibbonItemSize.Small, false, null, "OpenAttachments", () => { RaiseUiActionRequested("OpenAttachments"); return Task.CompletedTask; })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { nav, linked }) };
    }

    public override Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType) => Task.FromResult<Guid?>(null);
    public override Task ReloadAsync() => LoadAsync(Id);
}
