using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using FinanceManager.Domain.Attachments;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Accounts
{
    // Card VM: builds key/value pairs for a single bank account
    public sealed class BankAccountCardViewModel : BaseCardViewModel<(string Key, string Value)>, FinanceManager.Web.ViewModels.Common.IDeletableViewModel, ISymbolAssignableCard
    {
        public BankAccountCardViewModel(IServiceProvider sp)
            : base(sp)
        {
        }

        public Guid Id { get; private set; }
        public AccountDto? Account { get; private set; }
        public override string Title => Account?.Name ?? base.Title;
        public override async Task LoadAsync(Guid id)
        {
            Id = id;
            Loading = true; SetError(null, null); RaiseStateChanged();
            var api = ServiceProvider.GetRequiredService<IApiClient>();
            try
            {
                if (id == Guid.Empty)
                {
                    Account = new AccountDto(Guid.Empty, string.Empty, AccountType.Giro, null, 0m, Guid.Empty, null, SavingsPlanExpectation.Optional);
                    CardRecord = await BuildCardRecordsAsync(Account);
                    return;
                }

                Account = await api.GetAccountAsync(id);
                if (Account == null)
                {
                    SetError(api.LastErrorCode ?? null, api.LastError ?? "Account not found");
                    CardRecord = new CardRecord(new List<CardField>());
                    return;
                }
                CardRecord = await BuildCardRecordsAsync(Account);
            }
            catch (Exception ex)
            {
                CardRecord = new CardRecord(new List<CardField>());
                SetError(api.LastErrorCode ?? null, api.LastError ?? ex.Message);
            }
            finally { Loading = false; RaiseStateChanged(); }
        }

        public override AggregateBarChartViewModel? ChartViewModel
        {
            get
            {
                var title = Localizer?["Chart_Title_Account_Aggregates"] ?? "Account";
                var endpoint = $"/api/accounts/{Id}/aggregates";
                return new AggregateBarChartViewModel(ServiceProvider, endpoint, title);
            }
        }

        private async Task<CardRecord> BuildCardRecordsAsync(AccountDto a)
        {
            // Build record from authoritative source (Account + contact lookup). Pending values are applied afterwards via ApplyPendingValues.
            var bankContactName = string.Empty;
            if (a.BankContactId != Guid.Empty)
            {
                try
                {
                    var api = ServiceProvider.GetRequiredService<IApiClient>();
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
                new CardField("Card_Caption_Account_Name", CardFieldKind.Text, text: a.Name ?? string.Empty, editable: true),
                new CardField("Card_Caption_Account_Iban", CardFieldKind.Text, text: a.Iban ?? string.Empty, symbolId: null, editable: true),
                new CardField("Card_Caption_Account_Type", CardFieldKind.Text, text: a.Type.ToString(), editable: (a.Id == Guid.Empty), lookupType: "Enum:AccountType"),
                new CardField("Card_Caption_Account_Balance", CardFieldKind.Currency, amount: a.CurrentBalance),
                new CardField("Card_Caption_Account_Symbol", CardFieldKind.Symbol, symbolId: a.SymbolAttachmentId, editable: a.Id != Guid.Empty),
                new CardField("Card_Caption_Account_Contact", CardFieldKind.Text, text: bankContactName ?? "-", editable: true, lookupType: "Contact", lookupField: "Name", valueId: a.BankContactId, lookupFilter: "Type=Bank"),
                new CardField("Card_Caption_Account_SavingsPlanExpectation", CardFieldKind.Text, text: a.SavingsPlanExpectation.ToString(), editable: true, lookupType: "Enum:SavingsPlanExpectation"),
            };

            var record = new CardRecord(fields, a);
            record = ApplyEnumTranslations(record);
            return ApplyPendingValues(record);
        }

        private AccountDto BuildDto(CardRecord record)
        {
            var name = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Name")?.Text ?? Account?.Name ?? string.Empty;
            var iban = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Iban")?.Text ?? Account?.Iban ?? string.Empty;
            var typeText = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Type")?.Text;
            AccountType type = Account?.Type ?? AccountType.Giro;
            if (!string.IsNullOrWhiteSpace(typeText) && Enum.TryParse<AccountType>(typeText, ignoreCase: true, out var parsedType))
            {
                type = parsedType;
            }

            var spText = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_SavingsPlanExpectation")?.Text;
            var enumType = typeof(SavingsPlanExpectation);
            SavingsPlanExpectation spExpectation = Account?.SavingsPlanExpectation ?? SavingsPlanExpectation.Optional;
            if (!string.IsNullOrWhiteSpace(spText))
            {
                foreach (var v in Enum.GetValues<SavingsPlanExpectation>())
                {
                    var key = $"EnumType_{enumType.Name}_{v}";
                    var val = Localizer?[key];
                    if (!string.IsNullOrWhiteSpace(val) && string.Equals(val.Value, spText, StringComparison.OrdinalIgnoreCase))
                    {
                        spExpectation = v;
                        break;
                    }
                }
                if (!Enum.TryParse<SavingsPlanExpectation>(spText, ignoreCase: true, out var parsedSp))
                {
                    // parsedSp not used
                }
                else
                {
                    spExpectation = parsedSp;
                }
            }

            var symbolId = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Symbol")?.SymbolId ?? Account?.SymbolAttachmentId;
            var bankContactId = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Account_Contact")?.ValueId ?? Account?.BankContactId ?? Guid.Empty;

            return new AccountDto(Account?.Id ?? Guid.Empty, name, type, string.IsNullOrWhiteSpace(iban) ? null : iban, Account?.CurrentBalance ?? 0m, bankContactId, symbolId, spExpectation)
            {
                Name = name,
                Type = type,
                Iban = string.IsNullOrWhiteSpace(iban) ? null : iban,
                SymbolAttachmentId = symbolId,
                BankContactId = bankContactId,
                SavingsPlanExpectation = spExpectation,
                CurrentBalance = Account?.CurrentBalance ?? 0m,
                Id = Account?.Id ?? Guid.Empty
            };
        }

        public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
        {
            // Group: Navigieren (Back)
            var navActions = new List<UiRibbonAction>
            {
                new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
            };

            // Group: Verwalten (Save, Reset, Delete)
            var manageActions = new List<UiRibbonAction>
            {
                new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>", UiRibbonItemSize.Large, !HasPendingChanges, null, "Save", async () => { await SavePendingAsync(); }),
                new UiRibbonAction("Reset", localizer["Ribbon_Reset"].Value, "<svg><use href='/icons/sprite.svg#undo'/></svg>", UiRibbonItemSize.Small, !HasPendingChanges, null, "Reset", async () => { await ResetPendingAsync(); }),
                new UiRibbonAction("Delete", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#trash'/></svg>", UiRibbonItemSize.Small, Account == null || Account.Id == Guid.Empty, null, "Delete", () => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })
            };

            // Group: Verknüpfte Informationen (OpenPostings, OpenBankContact, OpenAttachments)
            var linkedActions = new List<UiRibbonAction>
            {
                new UiRibbonAction("OpenPostings", localizer["Ribbon_OpenPostings"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, Account == null, null, "OpenPostings", () => {
                    // Provide target URL as payload so the page doesn't need to know ViewModel type
                    var url = $"/list/postings/account/{Id}";
                    RaiseUiActionRequested("OpenPostings", url);
                    return Task.CompletedTask;
                }),
                new UiRibbonAction("OpenBankContact", localizer["Ribbon_OpenBankContact"].Value, "<svg><use href='/icons/sprite.svg#bank'/></svg>", UiRibbonItemSize.Small, Account == null || Account.BankContactId == Guid.Empty, null, "OpenBankContact", () => {
                    if (Account != null && Account.BankContactId != Guid.Empty)
                    {
                        var payload = $"contacts,{Account.BankContactId}";
                        RaiseUiActionRequested("OpenBankContact", payload);
                    }
                    return Task.CompletedTask;
                }),
                new UiRibbonAction("OpenAttachments", localizer["Ribbon_Attachments"].Value, "<svg><use href='/icons/sprite.svg#attachment'/></svg>", UiRibbonItemSize.Small, Account == null, null, "OpenAttachments", () => {
                    // Request attachments overlay with ParentKind/ParentId payload
                    RequestOpenAttachments(FinanceManager.Domain.Attachments.AttachmentEntityKind.Account, Id);
                    return Task.CompletedTask;
                })
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
            SetError(null, null);
            RaiseStateChanged();
            var api = ServiceProvider.GetRequiredService<IApiClient>();
            try
            {
                if (_pendingFieldValues.Count == 0) return;

                var newDto = BuildDto(CardRecord);

                if (Id == Guid.Empty)
                {
                    // Create new account
                    var createReq = new AccountCreateRequest(
                        newDto.Name,
                        newDto.Type,
                        newDto.Iban,
                        newDto.BankContactId == Guid.Empty ? null : newDto.BankContactId,
                        null,
                        newDto.SymbolAttachmentId,
                        newDto.SavingsPlanExpectation);

                    var created = await api.CreateAccountAsync(createReq);
                    if (created == null)
                    {
                        SetError(api.LastErrorCode ?? null, api.LastError ?? "Create failed");
                        Account = prevAccount;
                        CardRecord = prevRecord ?? new CardRecord(new List<CardField>());
                        ClearPendingChanges();
                        return;
                    }

                    Id = created.Id;
                    Account = created;
                    CardRecord = await BuildCardRecordsAsync(Account);
                    ClearPendingChanges();

                    RaiseUiActionRequested("Saved", Id.ToString());
                    return;
                }

                // existing account: update
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
                    SetError(api.LastErrorCode ?? null, api.LastError ?? "Update failed");
                    Account = prevAccount;
                    CardRecord = prevRecord ?? new CardRecord(new List<CardField>());
                    ClearPendingChanges();
                    return;
                }

                Account = updated;
                CardRecord = await BuildCardRecordsAsync(Account);
                ClearPendingChanges();
                RaiseUiActionRequested("Saved");
            }
            catch (Exception ex)
            {
                SetError(api.LastErrorCode ?? null, api.LastError ?? ex.Message);
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
                var api = ServiceProvider.GetRequiredService<IApiClient>();
                var att = await api.Attachments_UploadFileAsync((short)FinanceManager.Domain.Attachments.AttachmentEntityKind.Account, Id, stream, fileName, contentType ?? "application/octet-stream");
                await api.SetAccountSymbolAsync(Id, att.Id);
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
                    var api = ServiceProvider.GetRequiredService<IApiClient>();
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
            return await base.QueryLookupAsync(field, q, skip, take);
        }

        public override void ValidateLookupField(CardField field, LookupItem? item)
        {
            // For enum lookups, accept the selected name as pending value (no DB id required)
            if (!string.IsNullOrWhiteSpace(field.LookupType) && field.LookupType.StartsWith("Enum:", StringComparison.OrdinalIgnoreCase))
            {
                var selected = item?.Name ?? string.Empty;
                field.Text = selected;
                ValidateFieldValue(field, selected);
                return;
            }

            base.ValidateLookupField(field, item);
        }

        /// <summary>
        /// Deletes the current account via API. Returns true on success.
        /// </summary>
        public override async Task<bool> DeleteAsync()
        {
            // No-op when no account loaded
            if (Account == null) return false;

            Loading = true; SetError(null, null); RaiseStateChanged();
            var api = ServiceProvider.GetRequiredService<IApiClient>();
            try
            {
                var ok = await api.DeleteAccountAsync(Id);
                if (!ok)
                {
                    SetError(api.LastErrorCode ?? null, api.LastError ?? "Delete failed");
                    return false;
                }

                RaiseUiActionRequested("Deleted");
                return true;
            }
            catch (Exception ex)
            {
                SetError(api.LastErrorCode ?? null, api.LastError ?? ex.Message);
                return false;
            }
            finally
            {
                Loading = false; RaiseStateChanged();
            }
        }

        private Type? ResolveEnumType(string enumName)
        {
            // Try known namespace first
            var candidates = new[] {
                $"FinanceManager.Shared.Dtos.Accounts.{enumName}",
                enumName
            };
            foreach (var n in candidates)
            {
                try
                {
                    var t = Type.GetType(n, throwOnError: false, ignoreCase: true);
                    if (t != null && t.IsEnum) return t;
                }
                catch { }
            }

            // Search loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                try
                {
                    var t = asm.GetTypes().FirstOrDefault(x => x.IsEnum && (string.Equals(x.Name, enumName, StringComparison.OrdinalIgnoreCase) || string.Equals(x.FullName, enumName, StringComparison.OrdinalIgnoreCase) || x.FullName?.EndsWith("." + enumName, StringComparison.OrdinalIgnoreCase) == true));
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
