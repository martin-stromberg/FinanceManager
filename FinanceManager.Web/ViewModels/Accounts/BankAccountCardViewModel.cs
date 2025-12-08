using FinanceManager.Domain.Contacts;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.Components.Pages;
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
                CardRecord = await BuildCardRecordsAsync(Account);
            }
            catch (Exception ex)
            {
                CardRecord = new CardRecord(new List<CardField>());
                LastError = ex.Message;
            }
            finally { Loading = false; RaiseStateChanged(); }
        }

        private async Task<CardRecord> BuildCardRecordsAsync(AccountDto a)
        {
            // Build record from authoritative source (Account + contact lookup). Pending values are applied afterwards via ApplyPendingValues.
            var bankContactName = string.Empty;
            if (a.BankContactId != Guid.Empty)
            {
                try
                {
                    var api = _sp.GetRequiredService<IApiClient>();
                    var c = await api.Contacts_GetAsync(a.BankContactId);
                    if (c != null) bankContactName = c.Name;
                }
                catch
                {
                    // ignore
                }
            }

            var fields = new List<CardField>
            {
                new CardField("Card_Caption_Account_Name", CardFieldKind.Text, text: a.Name ?? string.Empty),
                new CardField("Card_Caption_Account_Iban", CardFieldKind.Text, text: a.Iban ?? string.Empty, symbolId: null),
                new CardField("Card_Caption_Account_Type", CardFieldKind.Text, text: $"$Card_Value_AccountType_{a.Type}"),
                new CardField("Card_Caption_Account_Balance", CardFieldKind.Currency, amount: a.CurrentBalance),
                new CardField("Card_Caption_Account_Symbol", CardFieldKind.Symbol, symbolId: a.SymbolAttachmentId, editable: true),
                new CardField("Card_Caption_Account_Contact", CardFieldKind.Text, text: bankContactName ?? "-", editable: true, lookupType: "Contact", lookupField: "Name", valueId: a.BankContactId, lookupFilter: "Type=Bank"),
            };

            var record = new CardRecord(fields, a);
            return ApplyPendingValues(record);
        }

        private AccountDto BuildDto(CardRecord record)
        {
            return new AccountDto(Account.Id, Account.Name, Account.Type, Account.Iban, Account.CurrentBalance, Account.BankContactId, Account.SymbolAttachmentId, Account.SavingsPlanExpectation)
            {
                Name = CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Name")?.Text ?? string.Empty,
                Type = Account?.Type ?? AccountType.Giro,
                Iban = CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Iban")?.Text ?? string.Empty,
                SymbolAttachmentId = Account.SymbolAttachmentId,
                BankContactId = CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Contact")?.ValueId ?? Guid.Empty,
                SavingsPlanExpectation = Account.SavingsPlanExpectation,
                CurrentBalance = Account.CurrentBalance,
                Id = Account.Id
            };
        }

        public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
        {
            // Group: Navigieren (Back)
            var navActions = new List<UiRibbonAction>
            {
                new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
            };

            // Group: Verwalten (Save, Reset)
            var manageActions = new List<UiRibbonAction>
            {
                new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>", UiRibbonItemSize.Large, !HasPendingChanges, null, "Save", async () => { await SavePendingAsync(); }),
                new UiRibbonAction("Reset", localizer["Ribbon_Reset"].Value, "<svg><use href='/icons/sprite.svg#undo'/></svg>", UiRibbonItemSize.Small, !HasPendingChanges, null, "Reset", async () => { await ResetPendingAsync(); })
            };

            // Group: Verknüpfte Informationen (OpenPostings)
            var linkedActions = new List<UiRibbonAction>
            {
                new UiRibbonAction("OpenPostings", localizer["Ribbon_OpenPostings"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, Account == null, null, "OpenPostings", () => { RaiseUiActionRequested("OpenPostings"); return Task.CompletedTask; })
            };

            var tabs = new List<UiRibbonTab>
            {
                new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, navActions),
                new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, manageActions),
                new UiRibbonTab(localizer["Ribbon_Group_Linked"].Value, linkedActions)
            };

            return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        }

        private async Task ResetPendingAsync()
        {
            // discard pending changes and restore card from authoritative Account
            try
            {
                ClearPendingChanges();
                if (Account != null)
                {
                    CardRecord = await BuildCardRecordsAsync(Account);
                }
            }
            catch
            {
                // ignore
            }
            RaiseStateChanged();
        }

        private async Task SavePendingAsync()
        {
            // Keep a snapshot of UI state so we can restore on error
            var prevAccount = Account;
            var prevRecord = CardRecord;

            Loading = true;
            LastError = null;
            RaiseStateChanged();
            var api = _sp.GetRequiredService<IApiClient>();
            try
            {
                if (_pendingFieldValues.Count == 0) return;

                var newDto = BuildDto(CardRecord);

                var req = new FinanceManager.Shared.Dtos.Accounts.AccountUpdateRequest(
                    newDto.Name,
                    newDto.Type,
                    newDto.Iban,
                    newDto.BankContactId,
                    null,
                    newDto.SymbolAttachmentId,
                    newDto.SavingsPlanExpectation,
                    false);

                var updated = await api.UpdateAccountAsync(Id, req);

                if (updated == null)
                {
                    // API reported not found or error; surface LastError from client if available
                    LastError = api.LastError ?? "Update failed";
                    LastErrorCode = api.LastErrorCode ?? null;
                    // restore previous UI state
                    Account = prevAccount;
                    CardRecord = prevRecord ?? new CardRecord(new List<CardField>());
                    ClearPendingChanges();
                    return;
                }

                // success
                Account = updated;
                CardRecord = await BuildCardRecordsAsync(Account);
                ClearPendingChanges();
            }
            catch (Exception ex)
            {
                // restore previous UI state and surface error
                LastError = api.LastError ?? ex.Message;
                LastErrorCode = api.LastErrorCode ?? null;
                Account = prevAccount;
                CardRecord = prevRecord ?? new CardRecord(new List<CardField>());
            }
            finally
            {
                Loading = false;
                RaiseStateChanged();
            }
        }

        public override async Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType)
        {
            try
            {
                var api = _sp.GetRequiredService<IApiClient>();
                // upload attachment
                var att = await api.Attachments_UploadFileAsync((short)FinanceManager.Domain.Attachments.AttachmentEntityKind.Account, Id, stream, fileName, contentType ?? "application/octet-stream");
                // set as account symbol
                await api.SetAccountSymbolAsync(Id, att.Id);
                // reload card data
                await InitializeAsync(Id);
                return att.Id;
            }
            catch
            {
                return null;
            }
        }
        public override async Task ReloadAsync()
        {
            await InitializeAsync(Id);
        }

        public override async Task<IReadOnlyList<LookupItem>> QueryLookupAsync(CardField field, string? q, int skip, int take)
        {
            try
            {
                if (string.Equals(field.LookupType, "Contact", StringComparison.OrdinalIgnoreCase))
                {
                    var api = _sp.GetRequiredService<IApiClient>();
                    // interpret LookupFilter (format: key=value) and map to API filters
                    ContactType? typeFilter = null;
                    if (!string.IsNullOrWhiteSpace(field.LookupFilter))
                    {
                        var parts = field.LookupFilter.Split('=', 2);
                        if (parts.Length == 2 && string.Equals(parts[0].Trim(), "Type", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Enum.TryParse<ContactType>(parts[1].Trim(), ignoreCase: true, out var ct)) typeFilter = ct;
                        }
                    }
                    var results = await api.Contacts_ListAsync(skip, take, typeFilter, false, q);
                    return results.Select(c => new LookupItem(c.Id, c.Name)).ToList();
                }
            }
            catch
            {
                // ignore and return empty
            }
            return Array.Empty<LookupItem>();
        }
    }
}
