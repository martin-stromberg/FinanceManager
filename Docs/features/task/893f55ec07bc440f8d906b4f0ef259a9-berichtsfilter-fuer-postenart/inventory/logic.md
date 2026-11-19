# Logik

## `ReportAggregationService`
Datei: `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `QueryAsync(ReportAggregationQuery, CancellationToken)` | public | Hauptpfad der Berichtsaggregation; kompletter Filterdatenfluss siehe unten |
| `LoadTextFilteredRowsAsync(...)` | private | Lädt Rohposten eines Kinds (Textfilter und/oder `bankPostingTypes`-Allow-List); wendet den Postenart-Filter in-memory an (Zeilen 930–995, Filter in Zeile 974) |
| `QuerySecurityDividendsNetAsync(...)` | private | Spezialpfad Nettodividenden (nur Security) — kein Postenart-Bezug |
| `LoadSuppressionKeysAsync(...)` | private | Lädt unterdrückte Erwartungen je Favorit |
| `LoadHoldingTransactionsBySecurityAsync(...)` / `HasPositiveHolding(...)` | private | Bestandsprüfung für Security-Erwartungen |

### Datenfluss des `BankPostingTypes`-Filters im Aggregationspfad
- Zeile 124–126: `bankPostingTypes`-HashSet wird nur gebildet, wenn `PostingKind.Bank` in `kinds` enthalten ist und `query.Filters?.BankPostingTypes` nicht leer.
- Zeile 127: `aggregateKinds` schließt `PostingKind.Bank` aus dem vorberechneten Aggregatpfad aus, sobald der Filter aktiv ist (Rohpostenpfad).
- Zeilen 153, 200–216: `rawAnon`-Tupelschema trägt `BankPostingType?`; für `rawKind == PostingKind.Bank` wird `bankPostingTypes` an `LoadTextFilteredRowsAsync` übergeben (Zeile 214).
- Zeilen 405–420: zweite (best-effort) Filterebene `allowedBankTypes` auf den bereits roh geladenen Zeilen.
- Zeilen 226–290: `SavingsPlanPostingSource.LinkedAccountPostings` leitet Sparplan-Zeilen aus Bank-Aggregaten/Posten ab — **ohne** Postenart-Auswertung (Abgrenzung laut Anforderung).

### Projektionspfad (Hochrechnung) — **Defekt 1 bestätigt**
- Zeilen 727–835: Bei `projectionEnabled` lädt `ReportProjectionEventLoader.LoadEntityEventsAsync` je Kind die `ProjectionEvent`s.
- Zeilen 752–763 (Bank-Block): Es werden nur `ownedAccountIds`, `accountFilter` (aus `query.Filters?.AccountIds`) und `bankPatterns` (Textfilter) übergeben — `query.Filters?.BankPostingTypes` wird **nicht** ausgewertet/übergeben. Die lokale Variable `bankPostingTypes` aus Zeile 124 wird im Projektionsblock nicht verwendet.
- `LoadEntityEventsAsync` (`ReportProjectionEventLoader.cs:41-87`) selektiert `p.PostingType` nicht (Select in Zeile 69 ohne `PostingType`) und besitzt keinen Postenart-Parameter — `ProjectionEvent` (Record in `ReportProjectionCalculator.cs:24`) führt ebenfalls keine Postenart.

## `ReportProjectionEventLoader`
Datei: `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `LoadEntityEventsAsync(kind, entityIds, entityIdFilter, textPatterns, windowStart, windowEnd, useValutaDate, ct)` | internal | Lädt `ProjectionEvent`s für Bank/Contact/SavingsPlan; Entity-Allow-List + Textmuster, kein Postenart-Parameter (Zeilen 41–87) |
| `LoadSavingsPlanAccountLinksAsync(ownerUserId, ct)` | internal | Sparplan→Konto-Links (Zeilen 95–104) |
| `LoadSavingsPlanEventsAsync(...)` | internal | Sparplan-Events bei `LinkedAccountPostings`; lädt Bank-Postings der verknüpften Konten **ohne** Postenart-Filter (Zeilen 120–175) |
| `LoadSecurityDividendEventsAsync(...)` | internal | Netto-Dividenden-Events pro (Security, Gruppe) (Zeilen 195–264) |
| `LoadSecurityHoldingTransactionsAsync(...)` | internal | Buy/Sell-Buchungen für die Bestandsprüfung (Zeilen 275–299) |

## `ReportPostingsQueryService` (Drilldown)
Datei: `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `QueryAsync(ReportPostingsQuery, CancellationToken)` | public | Postenliste hinter einer Berichtszelle |
| `LoadPostingsAsync(...)` | private | Zentrale Postenabfrage; **Postenart-Filter korrekt implementiert**: Zeilen 249–252 `bankTypes.Contains((int)p.PostingType)` bei `PostingKind.Bank` und `query.Filters?.BankPostingTypes` nicht leer (SQL-seitig) |
| `LoadLinkedSavingsPlanPostingsAsync(...)` | private | Verknüpfte Sparplanquelle; ruft `LoadPostingsAsync` mit `PostingKind.Bank` — der BankPostingTypes-Filter greift dort, wenn `Filters.BankPostingTypes` gesetzt ist (Zeile 221) |
| `QueryNetDividendPostingsAsync(...)` | private | Netto-Dividenden-Pfad (nur Security) |
| `FilterContext` (nested) | private | Entity-/Kategorie-Allow-Lists + Textmuster; kein Postenart-Member nötig, da der Filter direkt in `LoadPostingsAsync` sitzt |

Hinweis: `compareProjection` ist im Drilldown nur für `onlySecurity` aktiv (Zeile 83) — für Bankposten existiert im Overlay kein Projektionsanteil (Deckt sich mit Annahme der Anforderung).

## `ReportFavoriteService`
Datei: `FinanceManager.Infrastructure/Reports/ReportFavoriteService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ListAsync(ownerUserId, ct)` | public | Listet Favoriten; mappt `BankPostingTypesCsv` → `SetFilters(bankPostingTypes: ParseCsvInt(...))` (Zeilen 154, 178) |
| `GetAsync(id, ownerUserId, ct)` | public | Einzelner Favorit; gleiches Mapping (Zeilen 247, 269) |
| `CreateAsync(ownerUserId, request, ct)` | public | Legt Favoriten an; `ApplyFilters(entity, request.Filters)` (Zeile 327) |
| `UpdateAsync(id, ownerUserId, request, ct)` | public | Aktualisiert Favoriten; `ApplyFilters` (Zeile 374) |
| `DeleteAsync(id, ownerUserId, ct)` | public | Löscht Favoriten |
| `ToDtoFilters(e)` | private static | `GetFilters()` → `ReportFavoriteFiltersDto` inkl. `BankPostingTypes: f.BankPostingTypes` (Zeilen 60–85, Zeile 79); liefert `null`, wenn kein Filter gesetzt (inkl. `f.BankPostingTypes == null` in der Bedingung, Zeile 65) |
| `ApplyFilters(e, f)` | private static | `SetFilters(..., bankPostingTypes: f.BankPostingTypes, ...)` (Zeilen 93–115, Zeile 109); `null` → Filter leeren (Zeile 97) |
| `ParseKinds` / `EffectiveKinds` / `ParseCsv` / `ParseCsvInt` | private static | CSV-Hilfsfunktionen |

Der Service-Pfad ist damit vollständig: `BankPostingTypes` überlebt Create/Update/List/Get korrekt — die gemeldete Filterverlust-Stelle liegt nicht hier, sondern eine Ebene darüber im ViewModel.

## `ReportDashboardViewModel`
Datei: `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`

| Methode/Eigenschaft | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `SelectedBankPostingTypes` | public `HashSet<int>` | Aktiver Postenart-Filter (Zeile 165) |
| `TempBankPostingTypes` | public `HashSet<int>` | Temp-Puffer im Filterdialog (Zeile 1002) |
| `IsBankPostingTypeSelectedTemp(int)` | public | Temp-Auswahl prüfen (Zeile 1261) |
| `ToggleTempBankPostingType(int, bool)` | public | Temp-Auswahl umschalten (Zeile 1267) |
| `OpenFilterDialog()` | public | Kopiert `SelectedBankPostingTypes` → `TempBankPostingTypes` (Zeile 1034) |
| `ClearTempFilters()` | public | Leert u. a. `TempBankPostingTypes` (Zeile 1145) |
| `ApplyTempAndReloadAsync(...)` | public | Kopiert Temp → Selected (Zeile 1228), Dirty-Erkennung Zeile 1214 |
| `ClearFilters()` | public | Leert u. a. `SelectedBankPostingTypes` (Zeile 358) |
| `BuildFiltersPayload()` | public | `ReportAggregatesFiltersRequest` inkl. `BankPostingTypes` (Zeilen 304, 323) |
| `GetSelectedTempFiltersCount()` / `GetSelectedFiltersCount()` | public | Zählen `TempBankPostingTypes`/`SelectedBankPostingTypes` kontextlos mit (Zeilen 1163–1169, 1284–1290 — Review-Punkt: kein `PrimaryKind`-Bezug) |
| `LoadAsync(...)` | public | Aggregatabfrage; übergibt `filters` in `ReportAggregatesQueryRequest` (Zeile 717) |
| `ReloadAsync(...)` | public | Nutzt `BuildFiltersPayload()` (Zeile 382) |
| `SaveFavoriteAsync(...)` | public | **Defekt 2 bestätigt:** baut `ReportFavoriteFiltersDto` ohne `BankPostingTypes` (Zeilen 758–772) |
| `UpdateFavoriteAsync(...)` | public | **Defekt 2 bestätigt:** gleiche Stelle (Zeilen 818–832) |
| `DeleteFavoriteAsync(id, ct)` | public | Löscht Favoriten via API |
| `SubmitFavoriteDialogAsync(...)` | public | Verteilt auf `UpdateFavoriteAsync`/`SaveFavoriteAsync` (Zeilen 1400–1447); übergibt `filters` unverändert — der Filter geht erst in den beiden Mapping-Stellen verloren |
| `ShowPostingsAsync(...)` | public | Drilldown; übergibt `BuildFiltersPayload()` inkl. `BankPostingTypes` (Zeile 605) |

## `ReportDashboard.razor` (Seite)
Datei: `FinanceManager.Web/Components/Pages/ReportDashboard.razor`

| Stelle | Kurzbeschreibung |
|---|---|
| Zeilen 597–608 | Filterdialog: vier Checkboxen für Bank-Postenarten mit hartkodierten Enum-Ints `1`–`4` (`ToggleTempBankPostingType`), lokalisierte Labels `BankType_Inflow/Outflow/Interest/Tax` |
| Zeile 1399 | `SubmitFavoriteDialogAsync` ruft `_vm.SubmitFavoriteDialogAsync(..., BuildFilters(), ...)` — Filter-Payload enthält `BankPostingTypes` (Zeilen 1504, 1519) |
| Zeilen 1445–1484 | `ApplyFavorite` stellt `SelectedBankPostingTypes` aus `fav.Filters.BankPostingTypes` wieder her (Zeile 1470) |
| Zeilen 1492–1526 | `BuildFilters()` erzeugt `ReportAggregatesFiltersRequest` inkl. `BankPostingTypes` in beiden Zweigen |

## `ReportsController`
Datei: `FinanceManager.Web/Controllers/ReportsController.cs`

| Methode | Endpunkt | `BankPostingTypes`-Mapping |
|---|---|---|
| `QueryAsync` | `POST api/report-aggregates` | Zeile 121 → `ReportAggregationFilters` |
| `QueryPostingsAsync` | `POST api/report-postings` | Zeile 220 → `ReportAggregationFilters` |
| `CreateFavoriteAsync` | `POST api/report-favorites` | Zeile 311 → `ReportFavoriteFiltersDto` |
| `UpdateFavoriteAsync` | `PUT api/report-favorites/{id}` | Zeile 379 → `ReportFavoriteFiltersDto` |

Der Controller reicht `BankPostingTypes` an allen vier Stellen korrekt durch.

## `HomeKpiGrid.razor` (Home-KPI-Kacheln)
Datei: `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`

| Stelle | Kurzbeschreibung |
|---|---|
| `LoadFavoriteKpiDataAsync(k)` Zeilen 625–705 | Lädt Favorit (`_favorites`/`Reports_GetFavoriteAsync`), baut `ReportAggregatesQueryRequest` mit `ReportAggregatesFiltersRequest` (Zeilen 647–660) — **`BankPostingTypes` wird hier nicht übergeben** (dritte Verluststelle neben den beiden ViewModel-Mappings); KPI-Kacheln werten den Filter folglich nicht aus, selbst wenn er persistiert wäre |
| Zeile 597 | Klick auf Kachel navigiert zum Report-Dashboard (`?favoriteId=`) |

## MCP-Tools
| Datei | Stelle | Kurzbeschreibung |
|---|---|---|
| `FinanceManager.Web/Mcp/Tools/FmReportingAggregateTool.cs` | Zeile 115 | Favoriten-Pfad: `BankPostingTypes: f.BankPostingTypes` → `ReportAggregationFilters` (korrekt) |
| 〃 | Zeile 147 | Inline-Pfad: `bankPostingTypes`-Argument → `ReportAggregationFilters` (korrekt); JSON-Schema enthält `bankPostingTypes`-Property |
| `FinanceManager.Web/Mcp/Tools/FmReportsConfigurationManageTool.cs` | `ParseFilters` Zeile 567 | `bankPostingTypes`-Argument → `ReportFavoriteFiltersDto` für Favoriten-CRUD (korrekt) |
| `FinanceManager.Web/Mcp/Tools/FmConfigurationReadTool.cs` | `MapFavorite` Zeile 142 | Gibt `bankPostingTypes` in der Favoriten-Ausgabe aus (korrekt) |

## `ReportProjectionCalculator`
Datei: `FinanceManager.Infrastructure/Reports/ReportProjectionCalculator.cs`

| Element | Beschreibung |
|---|---|
| `ProjectionEvent` (Record, Zeile 24) | `Kind`, `EntityId`, `Date`, `Month`, `Amount`, `PostingId` — **kein** Postenart-Feld |
| `ReportProjectionContext` (Record, Zeile 11) | Intervall-/Fenster-Parameter der Projektion |
| `ApplyProjectionAmounts(points, events, context, nameResolver, hasActiveSecurity, securityDividendLeafOnly, suppressions)` | Schreibt `ProjectionAmount`/`ProjectionExpectedItems` auf die Punkte; aufgerufen in `ReportAggregationService` Zeile 834 (und im Netto-Dividenden-Pfad Zeilen 1217/1243) |
| `ProjectionSuppression` / `ProjectionCandidate` / `ProjectionPattern` | Unterdrückte Erwartungen bzw. Kandidaten-/Mustererkennung |

## `PostingTypeEvaluator`
Datei: `FinanceManager.Domain/Postings/PostingTypeEvaluator.cs`

| Methode | Kurzbeschreibung |
|---|---|
| `Evaluate(rules, subject, bookingDescription, amount)` | Klassifiziert via `AccountPostingRule`s (OrderIndex-Reihenfolge), Fallback `EvaluateByAmountSign` |
| `EvaluateByAmountSign(amount)` | Vorzeichen-Fallback: negativ → `Outflow`, sonst `Inflow` |
| `Resolve(proposed, amount)` | `Unspecified` → Vorzeichen-Fallback, sonst `proposed` |
| `NormalizeUmlauts` / `NormalizeForRuleMatch` | Textnormalisierung für Regelvergleich |

## `BackupService` / `SetupImportService`
| Datei | Stelle | Kurzbeschreibung |
|---|---|---|
| `FinanceManager.Infrastructure/Backups/BackupService.cs` | Zeilen 667–670, 800 | Exportiert `ReportFavorites` via `ToBackupDto()` (enthält `BankPostingTypesCsv`) |
| `FinanceManager.Infrastructure/Setup/SetupImportService.cs` | Zeilen 919–939 | Importiert Sektion `ReportFavorites` mit `TryGetProperty`-Guard; `AssignBackupDto` übernimmt `BankPostingTypesCsv`; Entity-IDs in den CSV-Filtern werden remapt (`BankPostingTypesCsv` enthält Int-Werte, kein Remap nötig) |
