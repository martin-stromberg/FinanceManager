# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs (`ReportProjectionEventLoader.LoadEntityEventsAsync`) - **Ineffiziente/inkonsistente Filteranwendung**

Der neue `bankPostingTypes`-Filter wird erst nach `ToListAsync` im Speicher ausgewertet (Z. 77), obwohl das Prädikat `bankPostingTypes.Contains(p.PostingType)` ins SQL übersetzbar ist. Das Drilldown-Pendant `ReportPostingsQueryService.LoadPostingsAsync` wendet exakt denselben Filter vor der Materialisierung auf dem `IQueryable` an (Z. 252–255). Bei aktivem Filter werden so alle Bankposten des Ladefensters materialisiert statt nur der erlaubten Typen; die anonyme Projektion muss `p.PostingType` zusätzlich selektieren.
Empfehlung: Den Filter für `PostingKind.Bank` auf `source` bzw. `postingsQuery` anwenden (`source = source.Where(p => bankPostingTypes.Contains(p.PostingType))`), analog zur Schwesterimplementierung; die In-Memory-`Where`-Klausel und der `PostingType`-Teil der Projektion entfallen dann.

### FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs (`ReportPostingsQueryService.LoadPostingsAsync`) - **Long Parameter List / uneinheitliche Filterübergabe**

Die Methode hat nach dem neuen Parameter 10 Argumente (Z. 235); die drei Aufrufstellen übergeben positional `..., null, false, null, ct` — ohne benannte Argumente nicht mehr nachvollziehbar, welcher `null` was ausschaltet. Zudem kommt `BankPostingTypes` jetzt als Parameter herein, während der gleichartige Art-Filter `SecuritySubTypes` weiterhin innen aus `query.Filters` gelesen wird (Z. 262): zwei gleichartige Filter, zwei Mechanismen. Die Repräsentation weicht ebenfalls ab — `IReadOnlyCollection<int>` hier vs. `HashSet<BankPostingType>` im Projektionspfad.
Empfehlung: Die artbezogenen Filter (TextPatterns, SubType-Constraint, BankPostingTypes) in ein Parameterobjekt bündeln (z. B. `PostingLoadFilters`) und die Repräsentation an einer Stelle normalisieren.

### FinanceManager.Web/Components/Pages/ReportDashboard.razor (`ReportDashboard.BuildFilters`) - **Doppelter Code / divergierende Semantik**

`BuildFilters()` (Z. 1492–1526) dupliziert `ReportDashboardViewModel.BuildFiltersPayload()` (Z. 277–331) nahezu wortidentisch (~35 Zeilen inkl. der zwei IncludeCategory-Varianten), weicht aber ab: Die Razor-Variante liefert nie `null` (die Leer-Prüfung `hasEntity/hasCats/...` fehlt). Folge: Über den Favoriten-Dialog wird stets ein `Filters`-Objekt mit lauter leeren Listen gespeichert, während der Abfragepfad `null` sendet. Diese Klasse von Mapping-Vervielfältigung hat bereits den vom Branch behobenen `BankPostingTypes`-Verlust erzeugt. Dieselbe Dublettenstruktur liegt im geänderten `ReportsController` weiterhin als identischer `ReportAggregationFilters`-Block zweimal vor (Z. 112–127 und 211–226).
Empfehlung: Die Razor-Aufrufstelle auf `_vm.BuildFiltersPayload()` umstellen (normalisiert zugleich das Null-Verhalten) bzw. die Methode entfernen; die beiden `ReportAggregationFilters`-Blöcke im Controller in eine private Mapping-Methode ziehen.

### FinanceManager.Infrastructure/Reports/ReportAggregationService.cs (`ReportAggregationService.QueryAsync`) - **God-Methode / doppelte Filtermaterialisierung**

`QueryAsync` umfasst ~880 Zeilen (Z. 44–929) inkl. mehrerer lokaler Funktionen; der Branch fädelt `bankPostingTypes` durch einen weiteren Abschnitt dieser Methode. Innerhalb derselben Methode wird dieselbe Allow-List zudem zweimal materialisiert: als `HashSet<BankPostingType>` (Z. 124–126, für Rohpfad + Projektion) und als `HashSet<int>` `allowedBankTypes` (Z. 405, für die Rohzeilen-Nachfilterung) — zwei Repräsentationen derselben Eingabe.
Empfehlung: Filterauflösung (Sets), Rohdaten-Laden je Kind und den Projektionsblock in eigene Methoden extrahieren; eine einzige typisierte Repräsentation der Allow-List wiederverwenden (z. B. `allowedBankTypes` auf `HashSet<BankPostingType>` umstellen und den `int`-Cast in Z. 420 entfernen). Vorbestehender struktureller Zustand, durch den Branch erweitert.

## Geprüfte Dateien

- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
- `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`
- `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs`
- `FinanceManager.Web/Controllers/ReportsController.cs`
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`
- `FinanceManager.Tests/Reports/ReportAggregationBankPostingTypeProjectionTests.cs`
- `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests_LinkedSourceBankPostingTypes.cs`
- `FinanceManager.Tests/ViewModels/ReportDashboardViewModelBankPostingTypeTests.cs`
- `FinanceManager.Tests.E2E/Tests/Reports/ReportBankPostingTypeFilterPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`
- `FinanceManager.Tests.E2E/Helpers/ReportDashboardGateway.cs`
