# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ReportDashboardViewModel.cs (ReportDashboardViewModel)
- **Doppelter Code** — Der Mapping-Block `ReportAggregatesFiltersRequest` → `ReportFavoriteFiltersDto` ist in `SaveFavoriteAsync` (Z. 758–773) und `UpdateFavoriteAsync` (Z. 819–834) wortidentisch dupliziert; derselbe Block existiert zusätzlich zweimal in `ReportsController` (Z. 302–316, Z. 370–384). Beide Records sind feldidentisch (gleiche Parameterliste und -reihenfolge). Genau diese Vervielfältigung hat den vom Branch behobenen Fehler verursacht: `BankPostingTypes` wurde im VM-Mapping vergessen, während der Controller es bereits mapt.
  Empfehlung: In `ReportDashboardViewModel` eine private statische Methode `ToFavoriteFilters(ReportAggregatesFiltersRequest filters)` extrahieren und in beiden Methoden aufrufen; mittelfristig einen gemeinsamen Mapper (z. B. Extension/Factory im Shared- oder Web-Layer) für die vier identischen Blöcke (VM ×2, Controller ×2) schaffen.

### ReportProjectionEventLoader.cs (ReportProjectionEventLoader)
- **Fehlende Validierung** — In `LoadEntityEventsAsync` wird die Allow-List `bankPostingTypes` unabhängig vom `kind`-Parameter angewendet (Z. 77: `.Where(r => bankPostingTypes is null || bankPostingTypes.Contains(r.PostingType))`). Die Methode bedient auch `PostingKind.Contact` und `PostingKind.SavingsPlan`; deren Postings tragen stets `BankPostingType.Unspecified`. Übergibt ein künftiger Aufrufer die Liste versehentlich für eine Nicht-Bank-Art, werden alle Events dieser Art still verworfen.
  Empfehlung: Die Bedingung um `kind == PostingKind.Bank &&` ergänzen (`kind == PostingKind.Bank && bankPostingTypes is not null && !bankPostingTypes.Contains(r.PostingType)` als Ausschluss-Kriterium), damit der dokumentierte Parameter-Scope („only bank postings") erzwungen statt nur konventionell eingehalten wird.
- **Namenskonvention/Struktur** — Der neue optionale Parameter `bankPostingTypes` wurde hinter `ct` angehängt (Z. 50–51: `CancellationToken ct, HashSet<BankPostingType>? bankPostingTypes = null`). Damit steht der CancellationToken nicht mehr an letzter Stelle — abweichend von der .NET-Konvention und von der Schwester-Signatur `LoadTextFilteredRowsAsync` in `ReportAggregationService.cs` (Z. 939–940), die `bankPostingTypes` vor `ct` führt.
  Empfehlung: Parameterreihenfolge zu `..., HashSet<BankPostingType>? bankPostingTypes, CancellationToken ct` ändern und die drei internen Aufrufstellen (Z. 140 in dieser Datei sowie `ReportAggregationService.cs` Z. 760–763, 774–776, 796–798) entsprechend anpassen.

### AccountsApiSeedHelper.cs (AccountsApiSeedHelper)
- **Randfälle datenabhängiger Prüfungen / Testdatenrealismus** — Geseedete Bankposten ohne explizites `SeededPosting.PostingType` behalten `BankPostingType.Unspecified` (Z. 119–122: `SetPostingType` wird nur bei `HasValue` aufgerufen, der `Posting`-Konstruktor initialisiert mit `Unspecified`). Produktiv tragen Bankposten dagegen immer einen konkreten Typ: die Migration und `Posting.AssignBackupDto` klassifizieren per `PostingTypeEvaluator.EvaluateByAmountSign`, der Import über `PostingTypeEvaluator.Evaluate` (liefert nie `Unspecified`). Ein zukünftiger E2E-Test, der Bankposten ohne Typ seedet und einen `BankPostingTypes`-Filter nutzt, würde die Postings still verlieren — ein Verhalten, das produktiv nicht auftreten kann.
  Empfehlung: Beim Seeden für `posting.Kind == PostingKind.Bank` und fehlendem `PostingType` `entity.SetPostingType(PostingTypeEvaluator.EvaluateByAmountSign(posting.Amount))` aufrufen (Backfill-Semantik wie im Backup-Restore), oder die Diskrepanz zumindest im XML-Doc von `SeededPosting.PostingType` festhalten.

### ReportDashboard.razor (ReportDashboard)
- **Primitive Obsession** — Die im Branch angepassten Aufrufstellen (Z. 602–605) casten `BankPostingType` nach `int`, weil `IsBankPostingTypeSelectedTemp(int)` und `ToggleTempBankPostingType(int, bool)` in `ReportDashboardViewModel` (Z. 1263, 1269) rohe `int`-Werte entgegennehmen, obwohl das Domain-Enum existiert und die Aufrufer es bereits typisiert vorliegen haben.
  Empfehlung: Die beiden VM-Methoden auf `BankPostingType` umstellen (interne `HashSet<int>`-Ablage kann bestehen bleiben, da die DTOs `int` verwenden — Cast dann zentral in der Methode); die Razor-Aufrufe übergeben das Enum ohne Cast.

## Geprüfte Dateien
- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
- `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`
- `FinanceManager.Tests/Reports/ReportAggregationBankPostingTypeProjectionTests.cs`
- `FinanceManager.Tests/ViewModels/ReportDashboardViewModelBankPostingTypeTests.cs`
- `FinanceManager.Tests.E2E/Tests/Reports/ReportBankPostingTypeFilterPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`
- `FinanceManager.Tests.E2E/Helpers/ReportDashboardGateway.cs`
