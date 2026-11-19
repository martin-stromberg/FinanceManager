# Umsetzungsplan: Berichtsfilter für Postenart — Fehlerbehebung und Feature-Review

## Übersicht

Der im Projekt „berichte-erweitern" eingeführte `BankPostingTypes`-Filter hat zwei bestätigte Defekte: (1) Die Hochrechnung im `ReportAggregationService` lädt Bank-`ProjectionEvent`s über `ReportProjectionEventLoader.LoadEntityEventsAsync` ohne den Postenart-Filter — `PostingType` wird nicht selektiert und der Parameter existiert nicht. (2) `ReportDashboardViewModel.SaveFavoriteAsync`/`UpdateFavoriteAsync` mappen `BankPostingTypes` nicht in das `ReportFavoriteFiltersDto`, der Filter geht beim Speichern verloren. Vorgehen ist test-first (reproduzierende rote Tests vor der Korrektur); anschließend vollständige Code-Review des Features, in deren Zug auch der bereits bekannte dritte Durchreichungs-Lücke (`HomeKpiGrid.razor`) sowie weitere risikoarme Befunde behoben werden.

## Scope-Klärung

| Verarbeitungseinheit | Auslösende Ereignisse | Erwartete Wirkung | Umgang mit fehlenden Objekten |
|---|---|---|---|
| Berichtsabfrage `ReportAggregationQuery`/`ReportAggregatesRequest` inkl. Hochrechnung (`ProjectionEvent`-Verarbeitung im `ReportAggregationService`) | Abruf der Berichtsaggregate mit `CompareProjection = true` und gesetztem `BankPostingTypes`-Filter (UI `ReportDashboard.razor`, API `ReportsController`, MCP `FmReportingAggregateTool`) | `ProjectionAmount`/`ProjectionExpectedItems` basieren nur auf Bankposten der ausgewählten `BankPostingType`s — analog zum bereits korrekt durchgereichten `AccountIds`-Filter und `BankTextFilters` | Keine neuen Objekte: `BankPostingType.Unspecified` ist bewusst keine Filterauswahl; nicht klassifizierte Bankposten fallen bei aktivem Filter korrekt heraus |
| Berichtsfavorit `ReportFavorite` inkl. Filter (`ReportFavoriteFiltersDto.BankPostingTypes`, `ReportFavorite.BankPostingTypesCsv`) | Speichern/Aktualisieren eines Favoriten aus dem Favoriten-Dialog (`SubmitFavoriteDialogAsync` → `SaveFavoriteAsync`/`UpdateFavoriteAsync`); Laden eines Favoriten (UI, MCP, Home-KPI-Kacheln) | Der Filter überlebt den kompletten Roundtrip: Filterdialog → `ReportAggregatesFiltersRequest` → `ReportFavoriteFiltersDto` → `ReportFavorite.BankPostingTypesCsv` → Persistenz → `GetFilters()` → Wiederanwendung beim Favoriten-Laden | Bestehende Favoriten ohne `BankPostingTypesCsv` (vor Migration `AddReportFavoriteBankPostingTypes` bzw. alte Backups) bleiben lesbar — bereits über Default-Parameter im `ReportFavoriteBackupDto` und `null`-CSV abgesichert |

### Scope-Ausschlüsse

| # | Einschränkung | Begründung | Status |
|---|---|---|---|
| 1 | Keine neue Klassifikationslogik; `Posting.PostingType` wird wie bisher verwendet | Der Defekt liegt in der Filterdurchreichung, nicht in der Klassifikation | bestätigt (2026-11-19) |
| 2 | Filter bleibt auf `PostingKind.Bank` beschränkt: aus Bankposten abgeleitete Sparplan-Posten (`SavingsPlanPostingSource.LinkedAccountPostings`) und Sparplan-Hochrechnungen werden **nicht** mitgefiltert | Explizite Abgrenzung aus der Anforderung; zu verifizieren, nur zu ändern falls die Review einen Widerspruch ergibt | bestätigt (2026-11-19) |
| 3 | Keine Erweiterung der Filterauswahl (kein `Unspecified`-Eintrag, keine weiteren Postenarten) | Bewusste fachliche Entscheidung des ursprünglichen Features | bestätigt (2026-11-19) |

## Designentscheidungen

| Komponente | Gewählter Ansatz | Begründung |
|---|---|---|
| `ReportProjectionEventLoader.LoadEntityEventsAsync` | Optionaler Postenart-Parameter (z. B. `IReadOnlyCollection<BankPostingType>?`, Default `null`) am Ende der Signatur; `p.PostingType` wird in die anonyme Selektion aufgenommen und die Allow-List wird in-memory neben den bestehenden `Amount != 0`-/`textPatterns`-Filtern angewendet. `ProjectionEvent` bleibt unverändert. | Spiegelt exakt das Muster von `LoadTextFilteredRowsAsync` (`HashSet<BankPostingType>?`-Parameter, In-Memory-Filter) und des `textPatterns`-Filters in derselben Methode. Alternativ `ProjectionEvent` um ein Postenart-Feld zu erweitern und im `ReportProjectionCalculator` zu filtern wurde verworfen: mehr Durchreiche-Aufwand, der Filter gehört an die Ladequelle, und das Event-DTO müsste ohne fachlichen Nutzen wachsen. Optionaler Parameter hält die anderen Aufrufstellen (Contact, SavingsPlan, `LoadSavingsPlanEventsAsync`) unverändert. |
| `ReportAggregationService` (Projektionsblock) | Die bereits in Zeile 124 berechnete `bankPostingTypes`-`HashSet<BankPostingType>?`-Variable im `kinds.Contains(PostingKind.Bank)`-Block an `LoadEntityEventsAsync` übergeben | Die Variable ist bereits korrekt konditioniert (`kinds.Contains(PostingKind.Bank)` + nicht-leerer Filter); identische Semantik wie `accountFilter`/`bankPatterns`. |
| `HomeKpiGrid.razor` | `BankPostingTypes: fav.Filters.BankPostingTypes` im `ReportAggregatesFiltersRequest`-Konstruktor ergänzen | Gleiche Form wie alle anderen Filter-Felder an dieser Stelle; schließt die dritte bekannte Durchreichungs-Lücke, damit KPI-Kacheln den gespeicherten Filter anwenden. |
| `ReportDashboard.razor` (hartkodierte Ints) | Literal `1`–`4` durch `(int)BankPostingType.Inflow`/`Outflow`/`Interest`/`Tax` ersetzen; `ToggleTempBankPostingType(int, bool)`-Signatur bleibt | Risikoarme Review-Korrektur im Feature-Scope; verhindert stille Fehlzuordnung bei Enum-Erweiterungen. |
| Übrige Komponenten | Keine — folgt bestehenden Mustern | DTOs, Controller, Service- und Persistenzpfad sind nach Bestandsaufnahme bereits korrekt verdrahtet; keine Schema-, Validierungs- oder Konfigurationsänderungen nötig. |

## Programmabläufe

### Ablauf 1: Berichtsaggregation mit `BankPostingTypes` + `CompareProjection` (korrigiert)

1. Benutzer setzt im Filterdialog des `ReportDashboard` Postenarten (Temp-Buffer `TempBankPostingTypes`) und bestätigt → `ApplyTempAndReloadAsync` kopiert nach `SelectedBankPostingTypes` und baut via `BuildFiltersPayload()` die `ReportAggregatesFiltersRequest` inkl. `BankPostingTypes`.
2. `IApiClient.Reports_QueryAggregatesAsync` → `ReportsController.QueryAsync` mappt in `ReportAggregationFilters` (inkl. `BankPostingTypes`) → `ReportAggregationService.QueryAsync`.
3. Aggregatpfad (unverändert, bereits korrekt): `bankPostingTypes`-HashSet aus `query.Filters?.BankPostingTypes`; `PostingKind.Bank` läuft über den Rohpostenpfad (`LoadTextFilteredRowsAsync`) mit In-Memory-Allow-List plus `allowedBankTypes`-Nachfilterung.
4. Projektionspfad (korrigiert): Im `kinds.Contains(PostingKind.Bank)`-Block wird `bankPostingTypes` zusätzlich zu `accountFilter`/`bankPatterns` an `LoadEntityEventsAsync` übergeben; der Loader selektiert `p.PostingType` und verwirft Events nicht ausgewählter Arten.
5. `ReportProjectionCalculator.ApplyProjectionAmounts` schreibt `ProjectionAmount`/`ProjectionExpectedItems` ausschließlich aus den gefilterten Events → UI zeigt nur Erwartungen der ausgewählten Postenarten.
6. Unverändert: Contact-/SavingsPlan-/Security-Blöcke erhalten keinen Postenart-Parameter; `LoadSavingsPlanEventsAsync` (LinkedAccountPostings) bleibt filterlos (Scope-Ausschluss 2).

### Ablauf 2: Favorit speichern/aktualisieren und wieder laden (korrigierter Roundtrip)

1. `ReportDashboard.razor` `SubmitFavoriteDialogAsync` übergibt `BuildFilters()` (enthält `BankPostingTypes` korrekt) an `ReportDashboardViewModel.SubmitFavoriteDialogAsync`.
2. `SaveFavoriteAsync`/`UpdateFavoriteAsync` (korrigiert): `ReportFavoriteFiltersDto` erhält `BankPostingTypes: filters.BankPostingTypes` in beiden Konstruktionen.
3. `IApiClient` → `ReportsController` → `ReportFavoriteService.ApplyFilters` → `ReportFavorite.SetFilters(bankPostingTypes: …)` → `BankPostingTypesCsv` persistiert (unverändert korrekt).
4. Wieder laden: `ListAsync`/`GetAsync` → `GetFilters()`/`ToDtoFilters` → `ReportFavoriteDto.Filters.BankPostingTypes` → `ApplyFavorite` im Dashboard stellt `SelectedBankPostingTypes` wieder her; MCP-Lesepfad und `HomeKpiGrid` (korrigiert) reichen den Filter in die Aggregatabfrage weiter.

### Ablauf 3: Home-KPI-Kachel mit gefiltertem Favorit (korrigiert)

1. `HomeKpiGrid.LoadFavoriteKpiDataAsync` lädt den Favoriten (`_favorites`/`Reports_GetFavoriteAsync`).
2. Beim Bau der `ReportAggregatesFiltersRequest` wird jetzt `BankPostingTypes` aus `fav.Filters` übernommen.
3. `Reports_QueryAggregatesAsync` liefert die gefilterten Punkte; die Kachel aggregiert `Amount`-Werte → angezeigter Wert entspricht der Filterauswahl.

### Ablauf 4: Feature-Review (Prüfschritte)

1. Durchreichung an allen Endpunkten abgleichen: Aggregate, Postings/Drilldown, Favoriten-CRUD, MCP (`FmReportingAggregateTool`, `FmReportsConfigurationManageTool.ParseFilters`, `FmConfigurationReadTool`), Home-KPI-Pfad.
2. `SavingsPlanPostingSource.LinkedAccountPostings` in Aggregation und Projektion: Filterfreiheit verifizieren (Scope-Ausschluss 2).
3. `Unspecified`-Behandlung bei Altdaten: `PostingTypeMigrationBackfillTests`-Abdeckung prüfen (Backfill aus Betragsvorzeichen).
4. Filter-Count-/Badge-Logik (`GetSelectedTempFiltersCount`/`GetSelectedFiltersCount` zählen `BankPostingTypes`/`SecuritySubTypes` kontextlos ohne `PrimaryKind`-Bezug): Konsistenz bewerten, Entscheidung dokumentieren.
5. Hartkodierte Enum-Ints (1–4) in `ReportDashboard.razor` → Korrekturaufgabe.
6. Doppelte `<param name="BankPostingTypes">`-XML-Docs in den DTOs: laut Bestandsaufnahme nicht vorhanden — verifizieren und als „kein Mangel" dokumentieren.
7. Backup/Restore-Konformität: `ReportFavoriteBackupDto.BankPostingTypesCsv` Default-Wert vorhanden; Feld an bestehender Entität → kein `TryGetProperty`-Guard nötig — verifizieren.
8. `EnumCoverageTests`: keine neuen Enum-Member — keine Änderung erwartet, verifizieren.
9. resx: keine neuen UI-Texte erwartet — verifizieren (Review-Korrekturen erzeugen keine neuen Labels).
10. Alle Befunde dokumentieren; risikoarme Mängel im Feature-Scope sofort beheben, größere als Folgeaufgaben (`continue.md`) notieren.

## Neue Klassen

| Klasse | Typ | Zweck |
|---|---|---|
| `ReportAggregationBankPostingTypeProjectionTests` | Testklasse (`FinanceManager.Tests/Reports/`) | Reproduziert Fehler 1 (Projektion ignoriert `BankPostingTypes`) und pinnt die `LinkedAccountPostings`-Abgrenzung; Hilfsmethoden analog `ReportAggregationCrossKindProjectionTests` (`CreateDb`, `AddBankPosting` mit `SetPostingType`, `RebuildAggregatesAsync`, `CreateSut`) |
| `ReportDashboardViewModelBankPostingTypeTests` | Testklasse (`FinanceManager.Tests/ViewModels/`) | Reproduziert Fehler 2 (Save/Update-Mapping) nach dem Muster von `ReportDashboardViewModelTextFilterTests` (Moq-`IApiClient`, Request-Capture) |
| `ReportBankPostingTypeFilterPlaywrightTests` | E2E-Testklasse (`FinanceManager.Tests.E2E/Tests/Reports/`) | Funktionsnachweis des Benutzerflusses Filter setzen → Hochrechnung → Favorit speichern/laden → KPI-Kachel |

## Änderungen an bestehenden Klassen

### `ReportProjectionEventLoader` (`FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`)
- `LoadEntityEventsAsync`: neuer optionaler Parameter für die Postenart-Allow-List (Default `null`, am Ende der Signatur); `p.PostingType` in die anonyme Selektion (Zeile 69) aufnehmen; zusätzlicher In-Memory-`Where`-Filter neben `Amount`/`textPatterns`; XML-Doc um `<param>`-Eintrag ergänzen.
- Alle übrigen Methoden und Aufrufstellen unverändert (Default-Parameter).

### `ReportAggregationService` (`FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`)
- Im `kinds.Contains(PostingKind.Bank)`-Projektionsblock (Zeilen ~752–763): Übergabe der bereits berechneten `bankPostingTypes`-Variable an `LoadEntityEventsAsync`. Keine weiteren Änderungen.

### `ReportDashboardViewModel` (`FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`)
- `SaveFavoriteAsync` (Zeilen ~758–772): `BankPostingTypes: filters.BankPostingTypes` im `ReportFavoriteFiltersDto`-Konstruktor ergänzen.
- `UpdateFavoriteAsync` (Zeilen ~818–832): gleiche Ergänzung.

### `HomeKpiGrid.razor` (`FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`)
- `LoadFavoriteKpiDataAsync` (Zeilen ~647–660): `BankPostingTypes: fav.Filters.BankPostingTypes` im `ReportAggregatesFiltersRequest`-Konstruktor ergänzen.

### `ReportDashboard.razor` (`FinanceManager.Web/Components/Pages/ReportDashboard.razor`)
- Zeilen ~602–605: Literal-Ints `1`–`4` durch `(int)BankPostingType.*`-Member ersetzen (Review-Korrektur, risikoarm).

### `ReportDashboardGateway` (`FinanceManager.Tests.E2E/Helpers/ReportDashboardGateway.cs`)
- Neuer Helper zum Umschalten/Prüfen einer Bankpostenart-Checkbox im Filterdialog (sprachunabhängig per Label-Regex über de/en-Texte, Muster wie `ProjectionCheckbox` in `ReportProjectionPlaywrightTests`).

### `AccountsApiSeedHelper.SeededPosting` (`FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`) — optional
- Optionales `BankPostingType`-Feld + `SetPostingType`-Aufruf beim Seed; Alternative: Direkt-Seed der Postings via `AppDbContext` in der neuen E2E-Klasse (Muster `SeedContactWithPriorYearPostingAsync`). Entscheidung bei Umsetzung nach Aufwand.

## Datenbankmigrationen

Keine. `ReportFavorite.BankPostingTypesCsv` (TEXT, nullable) existiert bereits über Migration `AddReportFavoriteBankPostingTypes`.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Keine AppSettings-, Ribbon- oder Berechtigungsänderungen; keine neuen resx-Texte erwartet.

## Seiteneffekte und Risiken

- **Signaturänderung `LoadEntityEventsAsync`:** `internal`; drei weitere Aufrufstellen (Contact, SavingsPlan direkt, `LoadSavingsPlanEventsAsync`-Delegat) bleiben über den Default-Parameter kompatibel — Missbrauchsrisiko gering.
- **`Unspecified`-Postings bei aktivem Filter:** Bankposten ohne klassifizierte Postenart werden herausgefiltert — fachlich korrekt, aber Test-Seeds müssen `SetPostingType` explizit setzen, sonst bricht die Projektion „unerwartet" (Default = `Unspecified`). Bestehende Projektionstests nutzen keinen Filter und sind unberührt.
- **LinkedAccountPostings-Falle:** `LoadSavingsPlanEventsAsync` lädt ebenfalls Bank-Postings — der neue Filter darf dort bewusst **nicht** ankommen (Scope-Ausschluss 2); wird per Pinning-Test abgesichert.
- **Review-Nebenfunde:** Die geforderte Gesamt-Review kann weitere Mängel liefern; Einordnung (sofort fixen vs. `continue.md`-Folgeaufgabe) nach Risiko und Feature-Scope nötig — Zeitrisiko.
- **Filter-Counter-Semantik:** Falls die Review eine `PrimaryKind`-Abhängigkeit fordert, ändern sich `GetSelectedTempFiltersCount`/`GetSelectedFiltersCount` — potenzieller Eingriff in Badge-Anzeige mit UI-Effekt; daher bewusst als Review-Entscheidung, nicht vorweg gefixt.
- **Regression:** Bestehende Projektions-, Filter-, Favoriten- und Backup-Tests (u. a. `ReportAggregationCrossKindProjectionTests`, `ReportPostingsQueryServiceTests`, `ReportFavoriteServiceFilterTests`, `ReportFavoriteBackupTests`, `PostingTypeMigrationBackfillTests`) müssen unverändert grün bleiben.

## Umsetzungsreihenfolge

1. **Repro-Test Fehler 1 schreiben:** neue Testklasse `ReportAggregationBankPostingTypeProjectionTests` mit Szenario „zwei Vorjahres-Bankposten unterschiedlicher Postenart + `BankPostingTypes`-Teilmenge + `CompareProjection` → `ProjectionAmount`/`ProjectionExpectedItems` nur der ausgewählten Arten".
   - Voraussetzungen (vorhanden): SQLite-InMemory-`AppDbContext`-Muster, `PostingAggregateService.RebuildForUserAsync`, `Posting.SetPostingType`, `ReportAggregationQuery.Filters`.
2. **Repro-Tests Fehler 2 schreiben:** neue Testklasse `ReportDashboardViewModelBankPostingTypeTests` mit `SaveFavorite_ShouldMapBankPostingTypes` und `UpdateFavorite_ShouldMapBankPostingTypes` (Request-Capture auf `Reports_CreateFavoriteAsync`/`Reports_UpdateFavoriteAsync`).
   - Voraussetzungen (vorhanden): `CreateVm`/`SetupAggregates`-Muster aus `ReportDashboardViewModelTextFilterTests`, Moq-`IApiClient`.
3. **Rot-Lauf verifizieren:** Beide neuen Tests müssen fehlschlagen (Fehlernachweis); Ergebnis festhalten.
4. **Korrektur Fehler 1:** `ReportProjectionEventLoader.LoadEntityEventsAsync` um optionalen Postenart-Filter erweitern (Selektion + Filter), `ReportAggregationService` Bank-Block übergibt `bankPostingTypes`.
5. **Korrektur Fehler 2:** `BankPostingTypes`-Mapping in `SaveFavoriteAsync`/`UpdateFavoriteAsync` ergänzen.
6. **Grün-Lauf der Repro-Tests verifizieren** plus Regression der unmittelbar betroffenen Testklassen.
7. **Vollständige Feature-Review** gemäß Ablauf 4 (Checkliste abarbeiten, Befunde dokumentieren).
   - Voraussetzungen: Schritte 4–5; Review umfasst auch die bereits aus der Bestandsaufnahme bekannten Funde.
8. **Review-Korrekturen im Scope umsetzen:** `HomeKpiGrid.razor`-Mapping, Enum-Ints in `ReportDashboard.razor`, ggf. weitere risikoarme Befunde; größere Befunde als Folgeaufgaben dokumentieren.
9. **Pinning-Test für Scope-Ausschluss 2** (`LinkedAccountPostings` ignoriert `BankPostingTypes` in Aggregation/Projektion) in `ReportAggregationBankPostingTypeProjectionTests`.
10. **E2E-Abdeckung:** Gateway-Helper + neue `ReportBankPostingTypeFilterPlaywrightTests` (Szenarien siehe E2E-Tabelle); ggf. `SeededPosting`-Erweiterung oder Direkt-Seed.
    - Voraussetzungen: Schritte 4–5 und 8; bestehende E2E-Infrastruktur (`PlaywrightWebAppFixture`, `AuthGateway`, `TestUserSeeder`, `BrowserApiHelper`, `ReportDashboardGateway`).
11. **Gesamtverifikation:** `dotnet build` + `FinanceManager.Tests` + `FinanceManager.Tests.Integration` + `FinanceManager.Tests.E2E` (CI-konforme Befehle/Filter aus `inventory/tests.md`).

## Tests

### Neue Tests

| Test | Testklasse / Datei | Prüfung |
|---|---|---|
| `QueryAsync_BankProjection_RespectsBankPostingTypes` | `ReportAggregationBankPostingTypeProjectionTests` (neu, `FinanceManager.Tests/Reports/`) | **Repro Fehler 1:** Konto mit zwei Vorjahres-Posten unterschiedlicher `BankPostingType`; Filter auf Teilmenge → `ProjectionAmount` und `ProjectionExpectedItems` enthalten nur die ausgewählte Art |
| `QueryAsync_BankProjection_BankPostingTypes_UnselectedTypesProduceNoExpectation` | `ReportAggregationBankPostingTypeProjectionTests` | Umkehrsicht: ein Posten nicht ausgewählter Art erzeugt weder `ProjectionAmount` noch Expected-Item (Schärfung des Repro-Nachweises) |
| `QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes` | `ReportAggregationBankPostingTypeProjectionTests` | Pinning Scope-Ausschluss 2: `SavingsPlanPostingSource.LinkedAccountPostings` liefert Sparplan-Erwartungen trotz gesetztem `BankPostingTypes`-Filter |
| `SaveFavorite_ShouldMapBankPostingTypes` | `ReportDashboardViewModelBankPostingTypeTests` (neu, `FinanceManager.Tests/ViewModels/`) | **Repro Fehler 2:** `BankPostingTypes` aus `ReportAggregatesFiltersRequest` landet im `ReportFavoriteFiltersDto` der Create-Payload |
| `UpdateFavorite_ShouldMapBankPostingTypes` | `ReportDashboardViewModelBankPostingTypeTests` | **Repro Fehler 2 (Update-Pfad):** gleiche Assertion auf `ReportFavoriteUpdateApiRequest` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund |
|---|---|
| `ReportAggregationCrossKindProjectionTests`, `ReportAggregationProjectionTests`, `ReportAggregationServiceTextFilterTests` | Regression: optionale Signaturerweiterung + Filterung im Bank-Projektionspfad; müssen unverändert grün bleiben |
| `ReportPostingsQueryServiceTests` | Regression: Drilldown-Filter bereits korrekt, unverändert |
| `ReportFavoriteServiceFilterTests`, `ReportFavoriteBackupTests`, `ReportDashboardViewModelTests`, `ReportDashboardViewModelTextFilterTests` | Regression: Favoriten-Roundtrip/-Backup unverändert grün |
| `EnumCoverageTests` | Keine neuen Enum-Member — keine Änderung, Review-Verifikation |
| `PostingTypeMigrationBackfillTests` | Review-Verifikation der `Unspecified`-/Altdaten-Behandlung |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei | Akzeptanzkriterium | Warum E2E nötig |
|---|---|---|---|---|
| P1 | Bankposten zweier Postenarten seeden → Filterdialog: eine Postenart wählen → Hochrechnung aktivieren → Reload | `FinanceManager.Tests.E2E/Tests/Reports/ReportBankPostingTypeFilterPlaywrightTests.cs` | Projektionsspalte sichtbar; Expected-Items-Overlay zeigt nur Erwartungen der gewählten Postenart (Beträge/Anzahl passen) | Nachweis Fehler-1-Korrektur über den realen End-to-End-Pfad (UI-Dialog → API → Aggregation → Rendering); Unit-Test deckt nur die Service-Ebene |
| P1 | Filter setzen → „Speichern unter" als Favorit → vollständiger Reload der Favoriten-Ansicht → Filterdialog erneut öffnen | `ReportBankPostingTypeFilterPlaywrightTests` | Die gewählten Postenart-Checkboxen sind nach dem Reload wieder gesetzt | Nachweis Fehler-2-Korrektur über den kompletten Roundtrip inkl. Persistenz und Wiederanwendung in der UI (Muster `SaveFavorite_ShouldPersistTextFilter_AcrossReload`) |
| P2 | Favorit mit `BankPostingTypes` per API anlegen → KPI-Kachel (`HomeKpiKind.ReportFavorite`) anlegen → Home-Seite öffnen | `ReportBankPostingTypeFilterPlaywrightTests` | Kachel rendert und zeigt den gefilterten Wert (nur Posten der ausgewählten Art summiert) | Deckt die `HomeKpiGrid`-Lücke ab — Razor-Komponente ohne Unit-Testzugang (Muster `KpiTile_WithTextFilterFavorite_…`) |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|---|---|---|---|---|---|
| 1 | Hochrechnung respektiert Postenartfilter (Happy Path) | Benutzer wählt im Filterdialog eine Postenart-Teilmenge und aktiviert „Hochrechnung" | Konto mit wiederkehrenden Bankposten verschiedener Postenarten; Bank-Bericht geöffnet | Filter setzen → Hochrechnung aktivieren → Bericht neu laden | Projektionsspalte und Expected-Items-Overlay zeigen ausschließlich Erwartungen der ausgewählten Postenarten; nicht ausgewählte Arten erzeugen keine `ProjectionAmount` | Unit `QueryAsync_BankProjection_RespectsBankPostingTypes`; E2E Szenario P1-1 |
| 2 | Favorit behält Postenartfilter (Happy Path) | Benutzer speichert den gefilterten Bericht als Favorit bzw. aktualisiert ihn | Wie #1, zusätzlich Speicher-Dialog | Filter setzen → „Speichern unter" → Seite/Favorit vollständig neu laden → Filterdialog öffnen | Checkboxen der zuvor gewählten Postenarten sind wieder gesetzt; Bericht und Hochrechnung nutzen den Filter weiter | Unit `SaveFavorite_ShouldMapBankPostingTypes` + `UpdateFavorite_ShouldMapBankPostingTypes`; E2E Szenario P1-2 |
| 3 | KPI-Kachel wendet gespeicherten Filter an | Home-KPI-Kachel für einen Favoriten mit `BankPostingTypes` | Favorit mit Filter (API-seitig angelegt), Posten mehrerer Arten im Zeitraum | Kachel anlegen → Home öffnen | Kachelwert summiert nur Posten der ausgewählten Postenarten | E2E Szenario P2 |
| 4 | Scope-Abgrenzung verifiziert | Review/Pinning-Test | Sparplan mit verknüpftem Konto (`LinkedAccountPostings`), `BankPostingTypes`-Filter gesetzt | Aggregation/Projektion mit Sparplan-Kind ausführen | Sparplan-Posten/-Erwartungen erscheinen ungefiltert (Verhalten unverändert) | Unit `QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes` |

## Offene Punkte

Keine — alle Klärungen vom Anwender bestätigt (2026-11-19). Die getroffenen Entscheidungen sind im Plan verankert: Review-Mängel werden im Feature-Scope risikoarm sofort behoben, größere Befunde als Folgeaufgaben dokumentiert; `LinkedAccountPostings` bleibt ungefiltert (Pinning-Test); im Drilldown-Overlay ist für Bankposten kein Projektionsanteil aktiv (`compareProjection` nur für Security) — kein Handlungsbedarf; die Filter-Counter-Logik wird in der Review bewertet, Änderung nur bei fachlichem Widerspruch.
