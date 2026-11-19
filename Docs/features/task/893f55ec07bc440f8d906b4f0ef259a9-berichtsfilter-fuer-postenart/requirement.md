# Anforderung — Berichtsfilter für Postenart fehlerhaft

## Fachliche Zusammenfassung

Im Projekt „berichte-erweitern" (Commit `9bc6213b` „feat: Bankpostenarten in Berichten filtern", Merge `71db4c8d`) wurde ein Filter auf die Bankpostenart (`BankPostingType`: `Inflow`, `Outflow`, `Interest`, `Tax`) für Berichte eingeführt. Zwei Defekte wurden gemeldet:

1. **Hochrechnung ignoriert den Filter:** Die Projektion wiederkehrender Bankposten (`CompareProjection`) berücksichtigt die ausgewählten Postenarten nicht — erwartete Posten werden auch für nicht ausgewählte Postenarten berechnet und angezeigt.
2. **Filter geht beim Speichern verloren:** Wird ein Bericht mit gesetztem Postenartenfilter als Favorit gespeichert bzw. aktualisiert, ist der Filter danach nicht mehr vorhanden.

Vorgehensvorgabe: Zuerst Tests schreiben, die beide Fehlschläge reproduzieren; erst nach erfolgreicher Nachstellung die Korrekturen implementieren. Zusätzlich wird eine vollständige Code-Review des gesamten Features „Bankpostenart-Filter in Berichten" gefordert (nicht nur der beiden Fehlerstellen).

## Fachlicher Scope

- **Verarbeitungseinheit:** Der Berichtsfavorit (`ReportFavorite`) bzw. die Berichtsabfrage (`ReportAggregationQuery`/`ReportAggregatesRequest`) inkl. ihrer Filter (`ReportAggregationFilters.BankPostingTypes`, `ReportFavoriteFiltersDto.BankPostingTypes`) und der daraus abgeleiteten Hochrechnung (`ProjectionEvent`-Verarbeitung im `ReportAggregationService`).
- **Auslösende Ereignisse:**
  - Abruf der Berichtsaggregate mit `CompareProjection = true` und gesetztem `BankPostingTypes`-Filter (UI `ReportDashboard.razor`, API `ReportsController`, MCP `FmReportingAggregateTool`).
  - Speichern/Aktualisieren eines Berichtsfavoriten aus dem Favoriten-Dialog (`SubmitFavoriteDialogAsync` → `SaveFavoriteAsync`/`UpdateFavoriteAsync`).
- **Erwartete Wirkung:**
  - Die Hochrechnung (`ProjectionAmount`, `ProjectionExpectedItems`) basiert nur auf Bankposten der ausgewählten `BankPostingType`s — analog zum `AccountIds`-Filter und zu `BankTextFilters`, die im Projektionspfad bereits korrekt ankommen.
  - Der `BankPostingTypes`-Filter überlebt den kompletten Roundtrip: Filterdialog → `ReportAggregatesFiltersRequest` → `ReportFavoriteFiltersDto` → `ReportFavorite.BankPostingTypesCsv` → Persistenz → `GetFilters()` → Wiederanwendung beim Favoriten-Laden (UI + MCP + Home-KPI-Kacheln).
- **Umgang mit fehlenden Objekten:** Bestehende Favoriten ohne `BankPostingTypesCsv` (vor der Migration `AddReportFavoriteBankPostingTypes` bzw. alte Backups ohne das DTO-Feld) müssen weiterhin lesbar sein — bereits über Default-Parameter im `ReportFavoriteBackupDto` bzw. `null`-CSV abgesichert. `BankPostingType.Unspecified` ist bewusst keine Filterauswahl (Abgrenzung aus Schritt 3).
- **Scope-Ausschlüsse:**
  - Keine neue Klassifikationslogik; `Posting.PostingType` wird wie bisher verwendet.
  - Gemäß Schritt-3-Abgrenzung bleibt der Filter auf `PostingKind.Bank` beschränkt: Aus Bankposten abgeleitete Sparplan-Posten (`SavingsPlanPostingSource.LinkedAccountPostings`) und Sparplan-Hochrechnungen werden **nicht** mitgefiltert — dieses Verhalten ist zu verifizieren, nicht zu ändern (sofern die Review keinen Widerspruch ergibt).
  - Keine Erweiterung der Filterauswahl (kein `Unspecified`-Eintrag, keine weiteren Postenarten).

## Betroffene Klassen und Komponenten

Feature-Implementierung (Review-Gegenstand, alle Dateien aus Commit `9bc6213b`):

- `FinanceManager.Domain/Reports/ReportFavorite.cs` — `BankPostingTypesCsv`, `SetFilters`, `GetFilters`, `ReportFavoriteBackupDto`, `ToBackupDto`, `AssignBackupDto`.
- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` — Filteranwendung in `LoadTextFilteredRowsAsync` und `rawAnon`-Filterung; **Verdacht Fehler 1:** Projektionspfad Zeilen ~752–763 lädt Bank-`ProjectionEvent`s über `ReportProjectionEventLoader.LoadEntityEventsAsync` nur mit `accountFilter`/`bankPatterns` — `query.Filters?.BankPostingTypes` wird nicht übergeben/ausgewertet.
- `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs` — `LoadEntityEventsAsync` selektiert `p.PostingType` nicht und kennt keinen Postenart-Parameter.
- `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` — Drilldown-Filter `bankTypes.Contains((int)p.PostingType)` (Zeilen ~246–249).
- `FinanceManager.Infrastructure/Reports/ReportFavoriteService.cs` — `GetFiltersDto`, `ApplyFilters` (`SetFilters`), List-/Detail-Mapping `BankPostingTypesCsv` ↔ `BankPostingTypes`.
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` — `SelectedBankPostingTypes`, `TempBankPostingTypes`, `IsBankPostingTypeSelectedTemp`, `ToggleTempBankPostingType`, Filter-Counts/`HasFilterChanges`/`ClearFilterSelection`/`BuildFilters`; **Verdacht Fehler 2:** `SaveFavoriteAsync` (Zeilen ~758–772) und `UpdateFavoriteAsync` (Zeilen ~818–832) mappen `filters.BankPostingTypes` **nicht** in das `ReportFavoriteFiltersDto` — das Feld fällt genau dort weg.
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor` — Checkboxen (harte Enum-Ints 1–4), Favoriten-Laden (~1470), Filter-Payload für Speichern (~1501–1529).
- `FinanceManager.Web/Controllers/ReportsController.cs` — vier Filter-Mappings inkl. `BankPostingTypes`.
- MCP-Tools: `FmReportingAggregateTool` (beide Pfade korrekt), `FmReportsConfigurationManageTool` (`ParseFilters`), `FmConfigurationReadTool` (Ausgabe `bankPostingTypes`).
- DTOs: `ReportAggregatesFiltersRequest`, `ReportAggregationFilters`, `ReportFavoriteFiltersDto` (jeweils mit doppeltem `<param name="BankPostingTypes">`-Doc — Review-Punkt).
- Migration `AddReportFavoriteBankPostingTypes`, Tests in `FinanceManager.Tests/Reports/*` (`ReportAggregationServiceTextFilterTests`, `ReportPostingsQueryServiceTests`, `ReportFavoriteServiceFilterTests`, `ReportFavoriteBackupTests`).

## Implementierungsansatz

1. **Fehler zuerst testseitig nachstellen (explizit vorgegeben):**
   - Test für Fehler 1: Aggregation mit `PostingKind.Bank`, `CompareProjection = true` und `BankPostingTypes`-Teilmenge → Assertion, dass `ProjectionAmount`/`ProjectionExpectedItems` nur Posten der ausgewählten Arten enthalten (Zielklasse: `ReportAggregationService`, analog zu `ReportAggregationServiceTextFilterTests`/`ProjectionCalculator`-Tests).
   - Test für Fehler 2: Roundtrip `SaveFavoriteAsync`/`UpdateFavoriteAsync` (ViewModel-Test mit Fake-`IApiClient`, analog `ReportDashboardViewModelTests`/`ReportDashboardViewModelTextFilterTests`) → Assertion, dass `BankPostingTypes` im `ReportFavoriteFiltersDto`-Payload ankommt; ggf. ergänzend Service-Roundtrip in `ReportFavoriteServiceFilterTests`.
   - Erst wenn beide Tests rot sind, implementieren.
2. **Korrektur Fehler 1 (erwarteter Ansatz):** `BankPostingTypes` in den Bank-Projektionspfad durchreichen — `ReportProjectionEventLoader.LoadEntityEventsAsync` um einen optionalen Postenart-Filter erweitern bzw. `PostingType` in die Event-Selektion aufnehmen und in `ReportAggregationService` (`kinds.Contains(PostingKind.Bank)`-Block) `query.Filters?.BankPostingTypes` anwenden; Semantik analog `accountFilter`/`bankPatterns`.
3. **Korrektur Fehler 2 (erwarteter Ansatz):** `BankPostingTypes: filters.BankPostingTypes` in den `ReportFavoriteFiltersDto`-Konstruktoren von `SaveFavoriteAsync` und `UpdateFavoriteAsync` ergänzen.
4. **Vollständige Code-Review des Features** über alle oben gelisteten Komponenten, u. a. mit diesen Prüfpunkten:
   - Verhalten bei `SavingsPlanPostingSource.LinkedAccountPostings` in Kombination mit dem Filter (Abgrenzung aus Schritt 3 verifizieren).
   - `Unspecified`-Behandlung bei Altdaten (Backfill-Migration `PostingTypeMigrationBackfillTests`).
   - Filter-Count-/Badge-Logik, wenn `PrimaryKind != Bank` (`GetTotalActiveFilterCount`, `GetTempFilterCount` zählen `BankPostingTypes`/`SecuritySubTypes` kontextlos — Konsistenz prüfen).
   - Hartkodierte Enum-Ints (1–4) in `ReportDashboard.razor` statt `BankPostingType`-Werten.
   - Doppelte XML-Doc-`<param>`-Einträge in `ReportAggregatesFiltersRequest`/`ReportFavoriteFiltersDto`.
   - Vollständigkeit der Durchreichung an allen Endpunkten (Aggregate, Postings/Drilldown, Favoriten CRUD, MCP, Home-KPI-Pfad).
   - Backup/Restore-Konformität gemäß AGENT.md (Default-Wert im Backup-DTO, `TryGetProperty`-Guard nicht nötig — Feld an bestehender Entität).
   - Gefundene Review-Mängel dokumentieren und — soweit im Scope der Fehlerbehebung — beheben bzw. als Folgeaufgaben notieren.

## Konfiguration

Keine neue Konfiguration erforderlich. `BankPostingTypes` bleibt ein Filterfeld des Favoriten (persistiert als `BankPostingTypesCsv`); keine Änderungen an AppSettings, Ribbon oder Berechtigungen. Neue/angepasste Tests sind in `FinanceManager.Tests` zu platzieren; bei neuen UI-Texten resx de + en (nicht erwartet).

## Offene Fragen

- Soll die Review nur die Defekte bestätigen oder gefundene Mängel sofort im selben Schritt mitbeheben? (Annahme: beheben, soweit im Feature-Scope und risikoarm; größere Befunde als `continue.md`-Folgeaufgaben.)
- Soll der Postenartfilter auch auf die Sparplan-Hochrechnung aus verknüpften Konten wirken? (Annahme: Nein — Abgrenzung aus Schritt 3 bleibt bestehen; nur zu verifizieren.)
- Ist im Drilldown-Overlay („Posteneinsicht") ein Projektionsanteil für Bankposten vorgesehen, der ebenfalls prüfen wäre? (`ReportPostingsQueryService` aktiviert `compareProjection` nur für Security — Annahme: kein Handlungsbedarf für Bank.)
