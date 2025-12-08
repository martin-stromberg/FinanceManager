using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using System.Diagnostics;

namespace FinanceManager.Web.ViewModels.Accounts
{
    // Card VM: builds key/value pairs for a single bank account
    public sealed class BankAccountCardViewModel : BaseCardViewModel<(string Key, string Value)>
    {
        private readonly IServiceProvider _sp;
        public BankAccountCardViewModel(IServiceProvider sp)
        {
            _sp = sp;
        }

        public Guid Id { get; private set; }
        public AccountDto? Account { get; private set; }

        public override async Task LoadAsync(Guid id)
        {
            Id = id;
            Loading = true; LastError = null; RaiseStateChanged();
            try
            {
                var api = _sp.GetRequiredService<IApiClient>();
                Account = await api.GetAccountAsync(id);
                if (Account == null)
                {
                    LastError = api.LastError;
                    CardRecord = new CardRecord(new List<CardField>());
                    return;
                }
                CardRecord = BuildCardRecords(Account);
            }
            catch (Exception ex)
            {
                CardRecord = new CardRecord(new List<CardField>());
                LastError = ex.Message;
            }
            finally { Loading = false; RaiseStateChanged(); }
        }

        private static CardRecord BuildCardRecords(AccountDto a)
        {
            var fields = new List<CardField>
            {
                new CardField("Card_Caption_Account_Name", CardFieldKind.Text, Text: a.Name ?? string.Empty),
                new CardField("Card_Caption_Account_Iban", CardFieldKind.Text, Text: a.Iban ?? string.Empty, SymbolId: null),
                new CardField("Card_Caption_Account_Type", CardFieldKind.Text, Text: $"$Card_Value_AccountType_{a.Type}"),
                new CardField("Card_Caption_Account_Balance", CardFieldKind.Currency, Amount: a.CurrentBalance)
            };
            return new CardRecord(fields, a);
        }

        public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
        {
            var actions = new List<UiRibbonAction>
            {
                new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; }),
                new UiRibbonAction("OpenPostings", localizer["Ribbon_OpenPostings"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, Account == null, null, "OpenPostings", () => { RaiseUiActionRequested("OpenPostings"); return Task.CompletedTask; })
            };
            var tabs = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, actions) };
            return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        }
    }
}
