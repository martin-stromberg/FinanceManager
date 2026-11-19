# Tasks: Berichtsfilter für Postenart — Fehlerbehebung und Feature-Review

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests (Reproduktion) | Neue Testklasse `ReportAggregationBankPostingTypeProjectionTests` anlegen (`FinanceManager.Tests/Reports/`) mit `QueryAsync_BankProjection_RespectsBankPostingTypes` — Fehler-1-Repro (Projektion ignoriert `BankPostingTypes`) | Offen | — |
| 2 | Tests (Reproduktion) | In `ReportAggregationBankPostingTypeProjectionTests` ergänzen: `QueryAsync_BankProjection_BankPostingTypes_UnselectedTypesProduceNoExpectation` (Umkehrsicht des Filters) | Offen | — |
| 3 | Tests (Reproduktion) | Neue Testklasse `ReportDashboardViewModelBankPostingTypeTests` anlegen (`FinanceManager.Tests/ViewModels/`) mit `SaveFavorite_ShouldMapBankPostingTypes` — Fehler-2-Repro (Create-Payload) | Offen | — |
| 4 | Tests (Reproduktion) | In `ReportDashboardViewModelBankPostingTypeTests` ergänzen: `UpdateFavorite_ShouldMapBankPostingTypes` — Fehler-2-Repro (Update-Payload) | Offen | — |
| 5 | Tests (Reproduktion) | Rot-Lauf der neuen Repro-Tests verifizieren und Fehlschlag als Defektnachweis festhalten (erst danach Korrekturen beginnen) | Offen | — |
| 6 | Korrektur Projektionspfad | `ReportProjectionEventLoader.LoadEntityEventsAsync`: optionalen Postenart-Parameter (Default `null`) ergänzen, `p.PostingType` in die Selektion aufnehmen, Allow-List-Filter anwenden; XML-Doc pflegen | Offen | — |
| 7 | Korrektur Projektionspfad | `ReportAggregationService`: im `kinds.Contains(PostingKind.Bank)`-Projektionsblock die berechnete `bankPostingTypes`-Variable an `LoadEntityEventsAsync` übergeben | Offen | — |
| 8 | Korrektur Favoriten-Payload | `ReportDashboardViewModel.SaveFavoriteAsync`: `BankPostingTypes: filters.BankPostingTypes` im `ReportFavoriteFiltersDto`-Konstruktor ergänzen | Offen | — |
| 9 | Korrektur Favoriten-Payload | `ReportDashboardViewModel.UpdateFavoriteAsync`: `BankPostingTypes: filters.BankPostingTypes` im `ReportFavoriteFiltersDto`-Konstruktor ergänzen | Offen | — |
| 10 | Korrektur Favoriten-Payload | Grün-Lauf der Repro-Tests + Regression der betroffenen Testklassen (`ReportAggregationCrossKindProjectionTests`, `ReportPostingsQueryServiceTests`, `ReportFavoriteServiceFilterTests`, `ReportDashboardViewModel*Tests`) verifizieren | Offen | — |
| 11 | Review | Filter-Durchreichung an allen Endpunkten prüfen: Aggregate, Postings/Drilldown, Favoriten-CRUD, MCP (`FmReportingAggregateTool`, `FmReportsConfigurationManageTool`, `FmConfigurationReadTool`), Home-KPI-Pfad | Offen | — |
| 12 | Review | `SavingsPlanPostingSource.LinkedAccountPostings`-Abgrenzung in Aggregation und Projektion verifizieren (Filter darf dort nicht wirken) | Offen | — |
| 13 | Review | `Unspecified`-/Altdaten-Behandlung prüfen (`PostingTypeMigrationBackfillTests`, Backfill aus Betragsvorzeichen) | Offen | — |
| 14 | Review | Filter-Count-/Badge-Logik bewerten (`GetSelectedTempFiltersCount`/`GetSelectedFiltersCount` ohne `PrimaryKind`-Bezug) und Entscheidung dokumentieren | Offen | — |
| 15 | Review | Doppelte `<param name="BankPostingTypes">`-XML-Docs in `ReportAggregatesFiltersRequest`/`ReportAggregationFilters`/`ReportFavoriteFiltersDto` verifizieren (laut Bestandsaufnahme nicht vorhanden) | Offen | — |
| 16 | Review | Backup-/Restore-Konformität verifizieren (`ReportFavoriteBackupDto`-Default, `ToBackupDto`/`AssignBackupDto`-Mapping, kein `TryGetProperty`-Guard nötig) | Offen | — |
| 17 | Review | `EnumCoverageTests`-Abdeckung und resx-Bedarf verifizieren (keine neuen Enum-Member/UI-Texte erwartet) | Offen | — |
| 18 | Review | Review-Ergebnisse dokumentieren: Mängelliste mit Einordnung (sofort fixen vs. `continue.md`-Folgeaufgabe) | Offen | — |
| 19 | Review-Korrekturen | `HomeKpiGrid.razor` `LoadFavoriteKpiDataAsync`: `BankPostingTypes: fav.Filters.BankPostingTypes` im `ReportAggregatesFiltersRequest`-Konstruktor ergänzen | Offen | — |
| 20 | Review-Korrekturen | `ReportDashboard.razor` (Zeilen ~602–605): hartkodierte Ints `1`–`4` durch `(int)BankPostingType.*`-Member ersetzen | Offen | — |
| 21 | Review-Korrekturen | Weitere risikoarme Review-Befunde im Feature-Scope beheben (sofern in Task 11–17 gefunden); größere Befunde als Folgeaufgaben notieren | Offen | — |
| 22 | Tests (Verifikation) | Pinning-Test `QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes` in `ReportAggregationBankPostingTypeProjectionTests` ergänzen (Scope-Ausschluss 2) | Offen | — |
| 23 | E2E | `ReportDashboardGateway`: Helper zum Umschalten/Prüfen der Bankpostenart-Checkboxen im Filterdialog (sprachunabhängig, de/en-Label-Regex) ergänzen | Offen | — |
| 24 | E2E | Seed-Infrastruktur für typisierte Bankposten bereitstellen: `AccountsApiSeedHelper.SeededPosting` um `BankPostingType` erweitern oder Direkt-Seed via `AppDbContext` (Entscheidung bei Umsetzung) | Offen | — |
| 25 | E2E | Neue E2E-Klasse `ReportBankPostingTypeFilterPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Reports/`): Szenario „Filter setzen → Hochrechnung zeigt nur gewählte Postenart" | Offen | — |
| 26 | E2E | In `ReportBankPostingTypeFilterPlaywrightTests`: Szenario „Filter setzen → Favorit speichern → Reload → Checkboxen wieder gesetzt" | Offen | — |
| 27 | E2E | In `ReportBankPostingTypeFilterPlaywrightTests`: Szenario „Favorit mit `BankPostingTypes` per API → Home-KPI-Kachel zeigt gefilterten Wert" | Offen | — |
| 28 | Abschluss | `dotnet build FinanceManager.sln` + `FinanceManager.Tests` (CI-Filter) fehlerfrei durchlaufen lassen | Offen | — |
| 29 | Abschluss | `FinanceManager.Tests.Integration` fehlerfrei durchlaufen lassen | Offen | — |
| 30 | Abschluss | `FinanceManager.Tests.E2E` fehlerfrei durchlaufen lassen; Ergebnisse dokumentieren | Offen | — |
