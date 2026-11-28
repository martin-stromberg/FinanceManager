# Bestandsaufnahme — Schritt 2: Listenfilter

(Ergänzt die Projekt-`inventory.md` mit dem Detailbefund der drei betroffenen ViewModels.)

## Betroffene Dateien

| Datei | Befund |
|-------|--------|
| `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs` | `LoadPageAsync` lädt `_api.SavingsPlans_ListAsync(OnlyActive)`, ignoriert `Search`/`RangeFrom`/`RangeTo`. Projiziert DTO→`SavingsPlanListItem` + lädt Kategorienamen (`SavingsPlanCategories_ListAsync`) + Analyse pro Plan (N+1, `SavingsPlans_AnalyzeAsync`). |
| `FinanceManager.Web/ViewModels/Securities/SecuritiesListViewModel.cs` | `LoadAsync` lädt `Securities_ListAsync(OnlyActive)` vollständig; `Search` ungenutzt; `AllowRangeFiltering` nicht übersteuert. `SecurityListItem` hat Name/Identifier/AlphaVantageCode/CategoryName. |
| `FinanceManager.Web/ViewModels/Setup/UserListViewModel.cs` | `LoadAsync` lädt `Admin_ListUsersAsync` vollständig; `Search`/`Range` ungenutzt. `UserAdminDto`: Id, Username, IsAdmin, Active, LockoutEnd, LastLoginUtc, PreferredLanguage. |

## Infrastruktur

- `BaseListViewModel<T>`: `Search`, `RangeFrom`, `RangeTo` (Setter `SetSearch`/`SetRange`),
  `AllowRangeFiltering`/`AllowSearchFiltering` (virtual, `protected set`, Standard `true`).
- Konvention für deaktivierten Datumsfilter: `public override bool AllowRangeFiltering => false;`
  (`ContactListViewModel`, `SavingsPlanCategoryListViewModel`, `BankAccountListViewModel` u. a.).
- `GenericListPage`: rendert Range-UI nur bei `ShowRange && Provider.AllowRangeFiltering`.
- `SavingsPlanDto` enthält alle nötigen Felder: `Name`, `Type`, `TargetDate`, `Interval`,
  `CreatedUtc`, `ContractNumber`, `CategoryId` — kein API-Ausbau nötig (clientseitige Filterung,
  Liste ohnehin ungepagt vollständig geladen).
- `SavingsPlan.AddIntervalWithMonthEndRule` (private static, `SavingsPlan.cs:205`): Monatsend-
  Semantik; `AdvanceTargetDateIfDue` schreibt `TargetDate` auf nächste Fälligkeit fort.
- `BudgetPurposeListViewModel.GetEffectiveMonthlyRange`: Referenz fürs Von>Bis-Tauschen.

## Domänensemantik wiederkehrender Fälligkeiten

- Fälligkeiten = `TargetDate ± n·Interval` (Intervalle: Monthly=1, BiMonthly=2, Quarterly=3,
  SemiAnnually=6, Annually=12 Monate). `TargetDate` ist selbst eine Fälligkeit (die nächste).
- Rückwärts-Iteration nur bis `CreatedUtc` (frühere Fälligkeiten existieren nicht).
- Ohne `RangeTo`: zukünftige Fälligkeiten unbeschränkt → Recurring-Plan immer Treffer.
- Ohne `RangeFrom`: Untergrenze = `CreatedUtc`.

## Tests

- `FinanceManager.Tests/ViewModels/SavingsPlansViewModelTests.cs` (Mock<IApiClient>, TestLocalizer)
- `FinanceManager.Tests/ViewModels/SecuritiesListViewModelTests.cs`
- `FinanceManager.Tests/Web/UsersViewModelTests.cs`
- Domänentests `FinanceManager.Tests/Savings/` für neue `SubtractIntervalWithMonthEndRule`.

## Risiken

- Analyse-N+1 läuft pro geladenem Plan — Filterung vor Projektion begrenzt die Kosten.
- Rückwärts-Iteration bei sehr alten Plänen: O(Monate seit CreatedUtc) — vertretbar.
- Clientseitige Filterung bleibt korrekt, solange die API die volle Liste liefert (`CanLoadMore=false`).
