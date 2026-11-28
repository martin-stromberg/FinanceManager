# Bestandsaufnahme – Schritt 3: Analyse-Buttons (#603)

## Betroffene Stellen

### `FinanceManager.Web/ViewModels/Budget/BudgetPurposeListViewModel.cs`

- `GetRibbonRegisterDefinition` → `analysis`-Tab (Sort 40): aktuell 4 Buttons.
  - `PrevMonth` (Z. 152-160): `today`-verankert, Zeitraum = Kalendervormonat.
  - `ThisMonth` (Z. 161-169): Zeitraum = aktueller Kalendermonat (korrekt).
  - `PrevYear` (Z. 170-178): `today`-verankert, **Breite = ganzes Kalendervorjahr**.
  - `ThisYear` (Z. 179-187): Zeitraum = aktuelles Kalenderjahr.
- `RangeFrom`/`RangeTo` (Basis `BaseListViewModel<TItem>`): frei wählbarer Zeitraum;
  `InitializeAsync` setzt Default = aktueller Kalendermonat. `ClearFilter`-Ribbon-Aktion
  setzt beide auf `null` (`ListPage.razor` case "ClearFilter": `ClearRange()`).
- `SetRange(from,to)` + `RaiseUiActionRequested("Reload")` → `ListPage.razor` case
  "Reload" → `Provider.ResetAndSearch()` + `LoadAsync()` → `LoadPageAsync` fragt
  `GetEffectiveMonthlyRange()` (Swap-Normalisierung vorhanden).
- `GenericListPage`: Datumsinputs `.list-filter-range` synchronisieren `_from/_to`
  bei `OnProviderStateChanged` aus `Provider.RangeFrom/To` → Buttons beeinflussen
  die sichtbaren Felder korrekt.

### `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs`

- `quickRange`-Tab (Sort 40): bereits 5 Buttons in der Zielreihenfolge.
- `ShiftAsOfMonthAsync(±1)`: `Settings.AsOfDate.AddMonths(±1)` → Monatsende → `LoadAsync`.
- `SetAsOfToCurrentMonthAsync`: `AsOfDate` = Monatsende heute → `LoadAsync`.
- `ShiftAsOfYearAsync(±1)`: Jahr±1, gleicher Monat, Monatsende → `LoadAsync`.
- `LoadAsync`: Fenster = `Months` Monate rückwärts ab `AsOfDate` (1. des Startmonats
  bis `AsOfDate`). `Settings` ist Record → `Settings with { AsOfDate = … }`.
- → **spezifikationskonform, keine Änderung nötig.**

### Testinfrastruktur

- VM-Tests: `vm.GetRibbonRegisters(localizer)` liefert `UiRibbonRegister` →
  `Tabs[].Items[]` (`UiRibbonAction` mit `Id` + `Func<Task>? Callback`) — Callbacks
  im Test direkt aufrufbar (Muster: `SavingsPlansViewModelTests.GetRibbon_Returns_Registers`).
- `IStringLocalizer`-Passthrough: `TestLocalizer<T>` (Indexer liefert Schlüsselname).
- `Mock<IApiClient>` + `TestCurrentUserService` für `InitializeAsync`/`SetRange`-Tests;
  `BudgetPurposeListViewModel.LoadPageAsync` ruft `Budgets_ListPurposesAsync(skip, take,
  contactId?, search?, DateOnly from, DateOnly to)` — `from`/`to` per `Verify`/`Capture`
  assertierbar.
- E2E: `PlaywrightWebAppFixture` + `AuthGateway`; Ribbon-Buttons rendern mit
  `button[title]`/Text; Range-Inputs `.list-filter-range input`.
  Budget-Übersicht: `/list/budget/purposes`.

## Risiken / Nebenpfade

- `SetRange` auf `null`-Bounds (nach ClearFilter): Shift-Anker definieren —
  `RangeFrom ?? Monatsanfang heute`, `RangeTo ?? Monatsende heute` (dokumentiert).
- Zeitreise-Tests: `DateTime.Today` wird produktiv gelesen — Tests müssen relativ zu
  `DateTime.Today` rechnen, keine hartkodierten Daten.
- Kein `UiRibbonAction.OnClick`-Interface — Callback ist `Func<Task>`; Invoke im Test.
