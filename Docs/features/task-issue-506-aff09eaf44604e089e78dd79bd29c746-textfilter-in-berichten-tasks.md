# Tasks: Textfilter in Berichten

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ReportFavorite`: vier neue `string?`-Properties `BankTextFiltersJson`, `ContactTextFiltersJson`, `SavingsPlanTextFiltersJson`, `SecurityTextFiltersJson` anlegen | Offen | — |
| 2 | Datenmodell | `ReportFavorite.SetFilters` um vier optionale `IEnumerable<string>?`-Parameter erweitern (Trim/Dedupe/500-Zeichen-Validierung, JSON-Serialisierung via `ToJson`) | Offen | — |
| 3 | Datenmodell | `ReportFavorite.GetFilters` Rückgabetupel auf 13 Elemente erweitern (`FromJson`-Deserialisierung) | Offen | — |
| 4 | Datenmodell | `ReportFavoriteBackupDto` um vier `string?`-Parameter mit `= null`-Defaults erweitern; `ToBackupDto`/`AssignBackupDto` ergänzen | Offen | — |
| 5 | Datenmodell | EF-Migration `dotnet ef migrations add AddReportFavoriteTextFilters` erzeugen (vier nullable String-Spalten auf `ReportFavorites`) | Offen | — |
| 6 | DTOs | `ReportAggregatesFiltersRequest` um `BankTextFilters`/`ContactTextFilters`/`SavingsPlanTextFilters`/`SecurityTextFilters` (`IReadOnlyCollection<string>? = null`) erweitern | Offen | — |
| 7 | DTOs | `ReportAggregationFilters` um dieselben vier Parameter mit `= null`-Defaults erweitern | Offen | — |
| 8 | DTOs | `ReportFavoriteFiltersDto` um dieselben vier Parameter mit `= null`-Defaults erweitern | Offen | — |
| 9 | Aggregationslogik | `PostingAggregateService.GetPeriodStart` auf `internal static` heben (Wiederverwendung für identische Perioden-Bucketierung) | Offen | — |
| 10 | Aggregationslogik | `ReportAggregationService.QueryAsync`: `textFiltersByKind` aufbauen, Aggregat-Ladung auf nicht-textgefilterte Kinds beschränken, Early-Return nur bei insgesamt leerem `rawAnon` | Offen | — |
| 11 | Aggregationslogik | `ReportAggregationService.LoadTextFilteredRowsAsync` implementieren (Owner-eingeschränkte `_db.Postings`-Abfrage, `BudgetRulePatternMatcher.MatchesPosting`-OR-Match, `Amount==0`-Skip, Valuta-Fallback, `GetPeriodStart`-Gruppierung → Tupel-Schema) | Offen | — |
| 12 | Aggregationslogik | `LinkedAccountPostings`-Block: bei gesetzten `SavingsPlanTextFilters` abgeleitete Sparplan-Zeilen aus textgefilterten Bank-Postings bilden | Offen | — |
| 13 | Aggregationslogik | `QuerySecurityDividendsNetAsync`: `SecurityTextFilters` auf Dividend-Anker-Buchungen anwenden und `groupNets` auf Treffer-`GroupId`s einschränken | Offen | — |
| 14 | Services | `ReportFavoriteService`: anonyme Projektionen in `ListAsync`/`GetAsync` um `*TextFiltersJson` erweitern und `SetFilters`-Wiederaufbau ergänzen | Offen | — |
| 15 | Services | `ReportFavoriteService.ToDtoFilters`/`ApplyFilters` um Textfilter-Felder erweitern; `ParseJsonList`-Helfer ergänzen | Offen | — |
| 16 | Services | `ReportsController.QueryAsync`: Mapping `req.Filters` → `ReportAggregationFilters` um die vier Textfilter-Felder erweitern | Offen | — |
| 17 | Services | `ReportsController.CreateFavoriteAsync`/`UpdateFavoriteAsync`: `ReportFavoriteFiltersDto`-Konstruktion um Textfilter-Felder erweitern | Offen | — |
| 18 | MCP | `FmReportingAggregateHandler` (Favoriten-Pfad): `ReportAggregationFilters`-Konstruktion um `f.*TextFilters` ergänzen | Offen | — |
| 19 | ViewModel | `ReportDashboardViewModel`: `Selected*TextFilters`-/`Temp*TextFilters`-`List<string>` je `PostingKind` + `NewTextFilterInput` anlegen | Offen | — |
| 20 | ViewModel | `GetTempTextFilters`/`AddTempTextFilter`/`RemoveTempTextFilter` (Kind-Map, Trim, Dedupe `OrdinalIgnoreCase`) implementieren | Offen | — |
| 21 | ViewModel | `OpenFilterDialog`/`ApplyTempAndReloadAsync`/`ClearTempFilters`/`ClearFilters` um Textfilter-Sammlungen erweitern | Offen | — |
| 22 | ViewModel | `GetSelectedTempFiltersCount`/`GetSelectedFiltersCount` um Textfilter der `SelectedKinds` erweitern | Offen | — |
| 23 | ViewModel | `BuildFiltersPayload`: `*TextFilters` in beiden Zweigen befüllen und `hasText` in die `null`-Bedingungen aufnehmen | Offen | — |
| 24 | ViewModel | `SaveFavoriteAsync`/`UpdateFavoriteAsync`: `ReportFavoriteFiltersDto`-Mapping um Textfilter erweitern | Offen | — |
| 25 | UI | `ReportDashboard.razor` Filterdialog: Textfilter-Editor-Sektion je aktivem `PostingKind`-Tab (Liste + Entfernen-Button + Eingabe mit `maxlength=500` + Hinzufügen-Button) | Offen | — |
| 26 | UI | `ReportDashboard.razor`: `ApplyFavorite` um Übernahme von `fav.Filters.*TextFilters` in `Selected*TextFilters` erweitern | Offen | — |
| 27 | UI | `ReportDashboard.razor`: `BuildFilters()` beide `ReportAggregatesFiltersRequest`-Zweige um Textfilter-Listen erweitern | Offen | — |
| 28 | UI | resx de + en: `FilterGroup_TextFilters`, `TextFilter_Placeholder`, `TextFilter_Add`, `TextFilter_Remove`, `TextFilter_Hint` ergänzen | Offen | — |
| 29 | KPI/Cache | `HomeKpiGrid.razor`: `FavKpiData` um `DateTime FavoriteModifiedUtc` erweitern; `LoadFavoriteKpiDataAsync` Request-Mapping um `*TextFilters` und `fav.ModifiedUtc` ergänzen | Offen | — |
| 30 | KPI/Cache | `HomeKpiGrid.RestoreFavoriteCacheAsync`: Favorit laden, Cache-Wert nur bei übereinstimmendem `ModifiedUtc` übernehmen, sonst `RemoveAsync` | Offen | — |
| 31 | Tests | Neue `ReportAggregationTextFilterTests`: Match in `Subject`/`Description`, ODER-Verknüpfung, Case-Insensitivity, Per-Kind-Mischbetrieb, Owner-Ausschluss, `Amount==0`-Parität, Valuta-Modus, `IncludeCategory`-Kumulativ | Offen | — |
| 32 | Tests | `ReportAggregationServiceSavingsPlanSourceTests` (oder neue Klasse): `SavingsPlanTextFilters` wirken auf Quell-Bankpostings bei `LinkedAccountPostings` | Offen | — |
| 33 | Tests | `ReportAggregationProjectionTests` (oder neue Klasse): `SecurityTextFilters` filtern Dividend-Gruppen im Netto-Dividenden-Pfad | Offen | — |
| 34 | Tests | `ReportFavoriteServiceTests`: Roundtrip der vier Textfilter-Listen, `ArgumentException` bei >500 Zeichen, `Filters=null` leert Textfilter | Offen | — |
| 35 | Tests | `ReportDashboardViewModelTests`: Temp→Selected-Übernahme, Clear, Zähler, `BuildFiltersPayload` inkl. `null`-Fall, SaveFavorite-Mapping | Offen | — |
| 36 | Tests | `HomeKpiGridTests`: Textfilter-Propagation in den Aggregations-Request; Cache-Discard + `RemoveAsync` bei `ModifiedUtc`-Mismatch | Offen | — |
| 37 | Tests | `BackupRestoreCoverageTests`: Textfilter-Roundtrip über Backup/Restore; Alt-Backup ohne neue Felder importiert fehlerfrei | Offen | — |
| 38 | E2E-Tests | `ReportDashboardGateway` um `OpenFilterDialogAsync`/`AddTextFilterAsync`/`ApplyFilterDialogAsync` erweitern | Offen | — |
| 39 | E2E-Tests | Playwright-Test: Textfilter im Filterdialog hinzufügen + anwenden → Bericht zeigt nur Treffer (Happy Path) | Offen | — |
| 40 | E2E-Tests | Playwright-Test: Favorit mit Textfilter speichern und neu laden → Filter bleiben aktiv und im Dialog sichtbar | Offen | — |
| 41 | E2E-Tests | Playwright-Test: KPI-Kachel mit Textfilter-Favorit zeigt gefilterte Summe und nach Filteränderung trotz LocalStorage-Cache aktualisierte Werte | Offen | — |
| 42 | MCP | MCP-Parität umsetzen (entschieden, empfohlener Vorschlag): `*TextFilters`-Argumente in `fm_reporting_aggregate` (Inline) und `fm_reports_configuration_manage` (`ParseFilters`) inkl. `McpToolArguments.OptionalStringList`-Helper — sonst leert MCP-Update Textfilter still | Offen | — |
