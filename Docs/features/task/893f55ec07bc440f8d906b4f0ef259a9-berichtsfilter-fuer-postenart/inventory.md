# Bestandsaufnahme: Berichtsfilter für Postenart fehlerhaft

Bestandsaufnahme des Features „Bankpostenart-Filter in Berichten" (`BankPostingType`-Filter auf `PostingKind.Bank`) vor der Behebung zweier gemeldeter Defekte: (1) die Hochrechnung ignoriert den Filter, (2) der Filter geht beim Speichern des Berichtsfavoriten verloren.

## Zusammenfassung

- **Filterdatenfluss vorhanden:** `BankPostingTypes` existiert durchgehend als Filterdimension — `BankPostingType`-Enum (0–4), `ReportAggregatesFiltersRequest` → `ReportAggregationFilters` → `ReportAggregationService`, `ReportFavoriteFiltersDto` → `ReportFavorite.BankPostingTypesCsv` (Migration `AddReportFavoriteBankPostingTypes`, nullable TEXT) → `GetFilters()`/`ToDtoFilters`. API-Controller, Drilldown (`ReportPostingsQueryService`), Backup/Restore und MCP-Tools sind korrekt verdrahtet.
- **Defekt 1 bestätigt:** Im Projektionspfad von `ReportAggregationService.QueryAsync` (Zeilen 752–763) lädt `ReportProjectionEventLoader.LoadEntityEventsAsync` Bank-`ProjectionEvent`s nur mit `accountFilter`/`bankPatterns`; `query.Filters?.BankPostingTypes` wird nicht übergeben. `LoadEntityEventsAsync` selektiert `p.PostingType` nicht, und `ProjectionEvent` führt keine Postenart. Der Aggregatpfad dagegen filtert korrekt (Zeilen 124–127, 214, 405–420, 974).
- **Defekt 2 bestätigt:** `ReportDashboardViewModel.SaveFavoriteAsync` (Zeilen 758–772) und `UpdateFavoriteAsync` (Zeilen 818–832) mappen `filters.BankPostingTypes` **nicht** in das `ReportFavoriteFiltersDto`. Die Payload aus dem Filterdialog (`ReportDashboard.razor` `BuildFilters()`, Zeilen 1504/1519) und der Service-Pfad (`ReportFavoriteService` inkl. `BankPostingTypesCsv`-Persistenz) sind dagegen korrekt — der Filter fällt genau an diesen zwei Stellen weg.
- **Zusatzfunde für die Review:**
  - `HomeKpiGrid.razor` `LoadFavoriteKpiDataAsync` (Zeilen 647–660) baut `ReportAggregatesFiltersRequest` für KPI-Kacheln **ohne** `BankPostingTypes` — dritte Durchreichungs-Lücke.
  - `ReportDashboard.razor` verwendet hartkodierte Enum-Ints `1`–`4` (Zeilen 602–605) statt `BankPostingType`-Werte.
  - Filter-Zähler (`GetSelectedTempFiltersCount`/`GetSelectedFiltersCount`) zählen `BankPostingTypes` kontextlos ohne `PrimaryKind`-Bezug.
  - `SavingsPlanPostingSource.LinkedAccountPostings`-Pfade (Aggregation und Projektion) werten den Filter nicht aus — deckt sich mit der Scope-Abgrenzung (zu verifizieren, nicht zu ändern).
  - Der genannte Review-Punkt „doppelte `<param name=\"BankPostingTypes\">`-Doc-Einträge" ist im aktuellen Code **nicht** vorhanden (je DTO genau ein Eintrag).
- **Test-Ausgangszustand:** Alle Baseline-Läufe grün — `FinanceManager.Tests` 2396/2396, `FinanceManager.Tests.Integration` 176/176 (inkl. ApiClient-Tests), `FinanceManager.Tests.E2E` 173/173; das Projekt `FinanceManager.Tests.Integration.ApiClient` existiert nur als Einzeldatei ohne `.csproj` und ist nicht in der Solution (Details in [Tests](inventory/tests.md)).
- **Testlücken:** Kein Test für Projektion + `BankPostingTypes` (Fehler 1) und kein Test für das ViewModel-Favoriten-Mapping (Fehler 2); Muster dafür vorhanden (`ReportAggregationCrossKindProjectionTests`, `ReportDashboardViewModelTextFilterTests.SaveFavorite_ShouldMapTextFilters`).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
