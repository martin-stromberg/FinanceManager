# Code-Review: Berichtsfilter für Postenart

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Infrastructure/Reports/ReportAggregationService.cs (LoadTextFilteredRowsAsync) - **In-Memory-Filterung / inkonsistente Filteranwendung**

Der Bank-Posting-Type-Filter wird in `LoadTextFilteredRowsAsync` erst nach `ToListAsync` zeilenweise im Speicher geprüft (Z. 974: `if (bankPostingTypes != null && !bankPostingTypes.Contains(p.PostingType)) { continue; }`), während die identische Allow-List in `ReportProjectionEventLoader.LoadEntityEventsAsync` (Z. 70–74) und `ReportPostingsQueryService.LoadPostingsAsync` (Z. 272–275) SQL-seitig auf dem `IQueryable` angewendet wird (`source.Where(p => bankPostingTypes.Contains(p.PostingType))`, nachweislich übersetzbar). Kommt die Bank-Art allein über den Typfilter in den Raw-Pfad (`patterns` ist dann `Array.Empty<string>()`, Z. 213), wird das komplette Posting-Set aller Owner-Accounts inklusive Subject/Description materialisiert, obwohl die Datenbank die Selektion liefern könnte.

**Empfehlung:** `bankPostingTypes`-Prädikat vor der Materialisierung auf `postingsQuery` anwenden (Z. ~950, analog zur Loader-Implementierung); die In-Memory-Prüfung entfällt damit ersatzlos.

### FinanceManager.Infrastructure/Reports/ReportAggregationService.cs (LoadTextFilteredRowsAsync) - **Long Parameter List / uneinheitliche Refactoring-Tiefe**

Die Methode hat nach dem neuen `bankPostingTypes`-Parameter 10 Parameter (Z. 930–939: `kind`, `patterns`, `sourcePeriod`, `useValutaDate`, `ownerEntityIds`, `windowStart`, `windowEnd`, `bankPostingTypes`, `ct`). Die funktional parallele Methode `ReportPostingsQueryService.LoadPostingsAsync` wurde in derselben Refactoring-Runde mit dem Bündel-Record `PostingLoadFilters` entschlackt; hier blieb die Parameterliste unverändert lang.

**Empfehlung:** Filter-Parameter (`patterns`, `windowStart`, `windowEnd`, `bankPostingTypes`) analog zu `PostingLoadFilters` in einen Record bündeln, um die Aufrufstellen lesbarer und erweiterbar zu halten.

### FinanceManager.Infrastructure/Reports/ReportAggregationService.cs (QueryAsync) - **God-Methode (pre-existing, weiter gewachsen)**

`QueryAsync` umfasst ~870 Zeilen (Z. 44–912) und orchestriert Rohdaten-Load, Ownership-/Filteranwendung, Gruppierung, YTD-/AllHistory-Transformation, Projektion und Vergleiche in einer Methode. Der Branch hat weitere Filterlogik (Bank-Posting-Types an Z. 124–127, 201, 214, 405–420, 762) in dieselbe Methode eingefügt, statt sie in eine Filter-Auflösung zu extrahieren.

**Empfehlung:** Mittelfristig die Filter-Normalisierung (Textfilter je Kind, Allow-Listen, Raw-Kind-Entscheidung) in eine eigene Einheit auslagern; für diesen Branch als bekanntes Strukturdefizit akzeptieren.

### FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs (ToSubTypeSet) - **Namenskonvention / Einheitlichkeit**

`ToSubTypeSet` (Z. 398) erzeugt ein `HashSet<SecurityPostingSubType>`, während der direkt benachbarte Helper `ToBankPostingTypeSet` (Z. 401) den Zieltyp explizit im Namen trägt. `ToSubTypeSet` ist dadurch mehrdeutig (es existiert nur ein "SubType"-Konzept: Security).

**Empfehlung:** In `ToSecuritySubTypeSet` umbenennen.

### FinanceManager.Tests/Reports/ReportAggregationBankPostingTypeProjectionTests.cs (CreateDb) - **Ressourcenbehandlung in Tests**

`CreateDb` (Z. 22–30) öffnet eine `SqliteConnection`, gibt sie aber nicht zurück und kann sie daher nie disposen; die Tests räumen nur `db` per `using var db` ab (die DbContext-Disposal gibt eine extern geöffnete Connection nicht frei). Die Schwesterdatei `ReportPostingsQueryServiceTests_LinkedSourceBankPostingTypes` und die Bestandskonvention in `ReportPostingsQueryServiceTests` führen `conn` mit und rufen `conn.Dispose()` am Testende auf.

**Empfehlung:** Connection analog zur Schwesterdatei an die Tests durchreichen und am Ende disposen (oder `await using`).

## Geprüfte Dateien

- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
- `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` (inkl. neuem `PostingLoadFilters`-Record)
- `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`
- `FinanceManager.Web/Controllers/ReportsController.cs` (`MapFilters`-Extraktion)
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` (`ToFavoriteFilters`, `BuildFiltersPayload`, enum-typisierte Toggles)
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`
- `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`
- `FinanceManager.Tests.E2E/Helpers/ReportDashboardGateway.cs`
- `FinanceManager.Tests/Reports/ReportAggregationBankPostingTypeProjectionTests.cs` (neu)
- `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests_LinkedSourceBankPostingTypes.cs` (neu)
- `FinanceManager.Tests/ViewModels/ReportDashboardViewModelBankPostingTypeTests.cs` (neu)
- `FinanceManager.Tests.E2E/Tests/Reports/ReportBankPostingTypeFilterPlaywrightTests.cs` (neu)

Build-Verifikation: `dotnet build` für `FinanceManager.Web`, `FinanceManager.Tests` und `FinanceManager.Tests.E2E` jeweils erfolgreich (0 Fehler).
