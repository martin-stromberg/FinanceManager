# Bestandsaufnahme: Nachbesserungen Sparplan↔Konto-Verknüpfung

Analyse des bestehenden Codes bezogen auf die Rückmeldungen aus `continue.md`.

## Zusammenfassung

- Das Verknüpfungs-Feature existiert bereits vollständig: `SavingsPlanAccountLink` (Domain), `SavingsPlanAccountLinkService`, REST-Endpunkte `…/linked-accounts`, UI-Panel `SavingsPlanLinkedAccountsPanel.razor`, Analyse-Helper `SavingsPlanLinkAnalysis` (Saldo = Summe verknüpfter Kontosalden, informelle Planposten, Istmeldung-Sperre).
- Fehlt: Info-Erklärung im Panel, Savings-only-Einschränkung (UI zeigt aktuell alle Eigentümerkonten, Service prüft nur Existenz/Eigentum), Symbolbuttons, Saldospalte, „Eigenanteil" (Planposten-Saldo) in den Kopfdaten, Bauspar-Demodaten, Report-Quellenoption.
- Die Sparplanübersicht (`SavingsPlansListViewModel`) zeigt den Saldo bereits über `SavingsPlans_AnalyzeAsync` → `AnalyzeAsync`, das bei Links die Kontosaldensumme liefert — dieselbe Regel wie die Kopfdaten (`SavingsPlanDto.CurrentAmount` via `GetCurrentAmountAsync`).

Test-Ausgangszustand: `dotnet test FinanceManager.Tests --no-build` (Baseline, siehe `inventory/tests.md`).

## Details

### `SavingsPlanLinkedAccountsPanel.razor` (`FinanceManager.Web/Components/Shared`)
- Tabelle mit Header `LinkedAccounts_Section_Title`, Leer-Text `LinkedAccounts_Empty`, je Zeile Name+IBAN und Textbutton `LinkedAccounts_Remove_Button`; Auswahl-Dropdown (`Vm.LinkableAccounts`, `Vm.NewLinkedAccountId`) + `LinkedAccounts_Add_Button`. `Localizer` = `IStringLocalizer` (Pages). Aufruf in `CardPage.razor:53` mit `Vm="savingsVm"`.
- Kein Info-Button, kein Konto-Aufruf, keine Saldospalte, keine Typ-Einschränkung.

### `SavingsPlanCardViewModel` (`FinanceManager.Web/ViewModels/SavingsPlans`)
- `LinkedAccounts`/`LinkableAccounts` werden in `LoadLinkedAccountsAsync` (Z. 218-240) über `SavingsPlans_GetLinkedAccountsAsync` + `GetAccountsAsync` gefüllt; `LinkableAccounts` = alle nicht verknüpften Konten (kein `AccountType`-Filter).
- `AddAccountLinkAsync`/`RemoveAccountLinkAsync` rufen die API auf und laden Links+Analyse neu.
- `BuildCardRecordAsync` (Z. 544-655) erzeugt u. a. `Card_Caption_SavingsPlan_CurrentAmount` (Saldo) aus `dto.CurrentAmount`.
- `RaiseUiActionRequested("OpenAccountDetails", accountId)` wird in `CardPage.razor` (Z. 237-249) bereits behandelt (Navigation `/card/accounts/{id}?back=…`).
- Icons (`wwwroot/icons/sprite.svg`): `info`, `unlink`, `external`, `link`, `account` u. a. vorhanden. Symbolbutton-Konvention: `class="icon-btn small"` + `<svg><use href="/icons/sprite.svg#…"/></svg>`.

### `SavingsPlanAccountLinkService` (`FinanceManager.Infrastructure/Savings`)
- `AddLinkAsync` prüft Plan-/Konto-Existenz + Owner + Duplikat; kein `AccountType`-Check. `Account` (Domain) hat `Type` (`AccountType` aus Shared.Dtos.Accounts: `Giro=0, Savings=1, CreditCard=2`). Controller mappt `ArgumentException` → 400 (`SavingsPlansController.AddLinkedAccountAsync`).

### `SavingsPlanService.GetAsync` / `SavingsPlanDto`
- `GetAsync` (Z. 67-84) berechnet `accumulated` über `SavingsPlanLinkAnalysis.GetCurrentAmountAsync` (Kontosaldensumme bei Links, sonst Planposten) → `SavingsPlanDto.CurrentAmount`. `SavingsPlanDto` (Shared) hat noch kein Feld für den Planposten-Saldo („Eigenanteil").

### Reports
- `ReportFavorite` (Domain): bool-Flags `UseValutaDate` etc., `Update(...)`, `ReportFavoriteBackupDto` + `ToBackupDto`/`AssignBackupDto`. Neues Flag/Enum hier + `AppDbContext` (b.Property konventionell) + Migration.
- `ReportFavoriteService` bildet Entity↔DTO (ListAsync/GetAsync/CreateAsync/UpdateAsync; `entity.Update(..., request.UseValutaDate)` an 3 Stellen).
- DTOs: `ReportFavoriteDto`, `ReportFavoriteCreateRequest`, `ReportFavoriteUpdateRequest`, `ReportFavoriteCreateApiRequest`, `ReportFavoriteUpdateApiRequest`, `ReportAggregatesQueryRequest` — alle mit optionalen Default-Parametern (rückwärtskompatible Erweiterung möglich).
- `ReportAggregationQuery` (record, optionale Parameter am Ende) → `ReportsController.QueryAsync` (Z. 106) → `ReportAggregationService.QueryAsync`: liest `PostingAggregates` (Booking/Valuta), `rawAnon`-Liste `(Kind, PeriodStart, Amount, AccountId, ContactId, SavingsPlanId, SecurityId, SecuritySubType)`, gruppiert nach Entity (`SavingsPlan:{id}`). Umattribution verknüpfter Pläne auf Bank-Aggregate hier einbauen (nach Z. 142).
- `SavingsPlanAccountLink.OwnerUserId` vorhanden → Owner-Scope für Links direkt abfragbar.
- `ReportDashboard.razor`: Edit-Panel `FilterGroup_Data` (Z. 43-85) mit Kind-Auswahl `_selectedKinds`, Datumswahl `_dateKindString`/`OnDateKindChange` (Select-Pattern); `ApplyFavorite`, `SubmitFavoriteDialogAsync` (VM, Z. 1119) → `SaveFavoriteAsync`/`UpdateFavoriteAsync` bauen `ReportFavoriteCreate/UpdateApiRequest`.
- MCP: `FmReportingAggregateTool` (Favorit → Query, Z. 89-103; manueller Pfad Z. 119-134), `FmReportsConfigurationManageTool` (Schema-JSON + Create/Update-Pfade) — Durchreichen der Option.

### Demodaten `DemoDataService` (`FinanceManager.Infrastructure/Demo`, 1062 Z.)
- `CreateDemoDataSetAsync` legt Kontakte/Gruppen/Pläne/Budgetzecke/Konten an; `CreateMonthlyPostingPlanAsync` erzeugt pro Monat je Konto einen Statement-Draft (`CreateDraftAsync`/`AddDraftEntryAsync`/`BookDraftAsync`).
- Planposten entstehen beim Buchen nur für Einträge mit `ContactId == self` und `SavingsPlanId` (`StatementDraftService`, Z. 2502-2509): Betrag `-e.Amount`, Gruppenverknüpfung mit Bankposten → informell, wenn Bankgegenposten auf nicht-verknüpftem Konto.
- Budget: `IBudgetPurposeService.CreateAsync(userId, name, BudgetSourceType.SavingsPlan, planId, null, categoryId, ct)` + `IBudgetRuleService.CreateAsync` — Istwerte für SavingsPlan-Zwecke = `-Planposten` (Regressionstest `BudgetPurposeServiceSavingsPlanActualsTests`).
- `EnsureDefaultReportFavoritesAsync` (Z. 923-958) legt zwei Favoriten über `IReportFavoriteService.CreateAsync` an.
- `ISavingsPlanAccountLinkService` ist in `ServiceCollectionExtensions` registriert (Z. 110) und injizierbar.

### Tests (Konventionen)
- In-Memory Sqlite `AppDbContext` + `EnsureCreated()` (z. B. `BudgetPurposeServiceSavingsPlanActualsTests`).
- Link-Tests: `FinanceManager.Tests/Savings/SavingsPlanAccountLinkServiceTests_{AddLink,Queries,RemoveLink}.cs` (nutzen `AccountType.Savings`).
- `SavingsPlanLinkedAnalysisTests`, `SavingsPlanServiceTests`, `SavingsPlanServiceActualReportTests`.
- Report-Aggregation: `FinanceManager.Tests/Reports/ReportAggregationServiceTests.cs` (+ weitere `ReportAggregationService*Tests`), `ReportFavoriteServiceTests`.
- ViewModel-Tests: `FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs`.
- Integration: `FinanceManager.Tests.Integration/ApiClient/ApiClientSavingsPlanLinksTests.cs`, `ApiClientDemoDataTests.cs`.
- Testklassen-Split gemäß test-class-structure: `{Klasse}Tests_{Thema}`.
