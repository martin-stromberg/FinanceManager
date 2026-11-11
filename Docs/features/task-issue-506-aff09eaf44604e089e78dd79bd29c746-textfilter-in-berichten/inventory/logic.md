# Logik

## `ReportAggregationService`
Datei: `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` (implementiert `IReportAggregationService`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `QueryAsync(ReportAggregationQuery, ct)` | public | Hauptpfad: lädt `PostingAggregates` (`sourcePeriod` je `Interval`, `DateKind` nach `UseValutaDate`) in Tupel-Liste `rawAnon` (Kind, PeriodStart, Amount, AccountId, ContactId, SavingsPlanId, SecurityId, SecuritySubType) |
| `QuerySecurityDividendsNetAsync(ReportAggregationQuery, ct)` | private | Spezialpfad: Netto-Dividenden je Wertpapier direkt aus `_db.Postings` gruppiert nach `GroupId`; Präzedenzfall für Rohposting-Auswertung |
| `ParseKindFromKey`, `TryParseSecurityKey` | private static | GroupKey-Parsing |
| `ApplyProjectionAmounts`, `DetectDividendPattern`, `HasPositiveHolding`, `FindExpectedDividends` u. a. | private (lokal) | Dividenden-Projektion |

Wesentliche Abschnitte in `QueryAsync`:
- **Sonderpfad-Weiche** (Z. 50–63): nur `Security` + (`IncludeDividendRelated` oder `CompareProjection`) → `QuerySecurityDividendsNetAsync`; ebenso YTD + Dividend-Subtyp (Z. 80–87).
- **Aggregat-Ladung** (Z. 94–142): `_db.PostingAggregates` gefiltert nach `Period`, `kinds`, `DateKind` (Valuta vs. Booking als zwei getrennte Zeilenmengen — kein Valuta-Fallback auf Booking im Standardpfad; der Sonderpfad nutzt `(DateTime?)p.ValutaDate ?? p.BookingDate`).
- **`LinkedAccountPostings`** (Z. 149–187): ersetzt SavingsPlan-Zeilen verknüpfter Pläne durch abgeleitete Zeilen aus Bank-Aggregaten (`SavingsPlanAccountLinks` mit `OwnerUserId == query.OwnerUserId`).
- **Ownership-Filterung** (Z. 189–226): lädt `accountNames`/`contactNames`/`savingsNames`/`securityNames` jeweils mit `OwnerUserId == query.OwnerUserId` und entfernt `rawAnon`-Zeilen ohne Besitzer-Match — das ist die Besitzerkette, die ein Rohposting-Pfad ebenfalls abbilden müsste (`Posting` hat selbst kein `OwnerUserId`).
- **Kategorie-Mappings** (Z. 228–280): `contactCategoryMap`/`savingsCategoryMap`/`securityCategoryMap` + Namen bei `IncludeCategory`.
- **Top-Level-Filter** (Z. 282–362): `ToSet`-Allow-Listen aus `query.Filters`; bei `IncludeCategory` ersetzen Kategoriefilter die Entitätsfilter je Kind; Security-Subtyp-Filter nur wenn `Security` in `kinds`. Hier müsste eine Textfilter-Dimension angebunden werden.
- **Punktaufbau** (Z. 364–532): Entity-Gruppen (Security split per `SecuritySubType`), Kategorie- und `Type:`-Aggregate, `ParentGroupKey`-Hierarchie.
- **Transformationen** (Z. 534–743): YTD-/AllHistory-Umbau, `ComputeLatestPeriod`, `ComparePrevious`/`CompareYear`, `Take`-Trimming, Sortierung.

`QuerySecurityDividendsNetAsync` (Z. 753–1197) liest `_db.Postings` (Security, `SecurityId` ∈ owned, Datumsfenster `loadStartMonth`..`endExclusive`, `SecuritySubType != null`), netted Dividende+Fee+Tax je `GroupId` und berücksichtigt `SecurityIds`-/`SecurityCategoryIds`-Filter — Filter auf `Subject`/`Description` wären hier separat anzubringen.

## `ReportFavoriteService`
Datei: `FinanceManager.Infrastructure/Reports/ReportFavoriteService.cs` (implementiert `IReportFavoriteService`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ListAsync(ownerUserId, ct)` | public | Anonyme Projektion aller `*Csv`-Spalten + Flags (Z. 97–124), rekonstruiert Entity über Konstruktor + `SetFilters` + `Update` und baut `ReportFavoriteDto` |
| `GetAsync(id, ownerUserId, ct)` | public | Gleiche anonyme Projektion für einen Favoriten (Z. 167–195) |
| `CreateAsync(ownerUserId, ReportFavoriteCreateRequest, ct)` | public | Dubletten-Check je Name/Owner, `ApplyFilters(entity, request.Filters)` → `SetFilters` |
| `UpdateAsync(id, ownerUserId, ReportFavoriteUpdateRequest, ct)` | public | `Rename` + `Update` + `SetPostingKinds` + `ApplyFilters` |
| `DeleteAsync(id, ownerUserId, ct)` | public | Löscht eigenen Favoriten |
| `ToDtoFilters(ReportFavorite)` | private static | `GetFilters()` → `ReportFavoriteFiltersDto`; `null` wenn keine Filter gesetzt |
| `ApplyFilters(ReportFavorite, ReportFavoriteFiltersDto?)` | private static | `SetFilters(...)`; `null` leert alle Filter |
| `ParseCsv` / `ParseCsvInt` / `ParseKinds` | private static | CSV-Parsing-Hilfen (GUID/int/Kind) |

Neue Textfilter-Felder müssten hier in den anonymen Projektionen (`ListAsync`/`GetAsync`), `ToDtoFilters`, `ApplyFilters` und den `SetFilters`-Aufrufen ergänzt werden.

## `ReportsController`
Datei: `FinanceManager.Web/Controllers/ReportsController.cs`

| Methode | Route | Kurzbeschreibung |
|---------|-------|------------------|
| `QueryAsync` | POST `api/report-aggregates` | `Take`-Validierung (1..200), Multi-Kind-Fallback via Query-String, mappt `req.Filters` → `ReportAggregationFilters` (Z. 101–106), baut `ReportAggregationQuery` mit `_current.UserId` |
| `ListFavoritesAsync` / `GetFavoriteAsync` | GET `api/report-favorites[/{id}]` | Delegiert an `IReportFavoriteService` |
| `CreateFavoriteAsync` | POST `api/report-favorites` | Mappt `req.Filters` → `ReportFavoriteFiltersDto` (Z. 181) → `ReportFavoriteCreateRequest` |
| `UpdateFavoriteAsync` | PUT `api/report-favorites/{id}` | Analog (Z. 235) |
| `DeleteFavoriteAsync` | DELETE `api/report-favorites/{id}` | Löscht Favoriten |

## `FmReportingAggregateHandler` / `FmReportingAggregateDescriptor`
Datei: `FinanceManager.Web/Mcp/Tools/FmReportingAggregateTool.cs`

MCP-Tool `fm_reporting_aggregate` (Scopes: Postings/Reports, Lesen). `InvokeAsync` baut `ReportAggregationQuery` auf zwei Wegen:
- **Favoriten-Pfad** (Z. 76–111): `_favorites.GetAsync` → `new ReportAggregationFilters(f.AccountIds, …, f.SecuritySubTypes, f.IncludeDividendRelated)` — neue Filterdimensionen müssten hier manuell ergänzt werden.
- **Inline-Pfad** (Z. 112–141): Filter aus JSON-Argumenten via `McpToolArguments.OptionalGuidList`/`OptionalIntList`; das `JsonSchema` (Z. 20) listet die Filter-Argumente explizit.
- `year`-Nachtragsfilter auf `PeriodStart.Year` (Z. 145–151); Ergebnis als anonymes Objekt mit `coverage` und `points` (Namen als `untrusted_data` markiert).

## `PostingAggregateService`
Datei: `FinanceManager.Infrastructure/Aggregates/PostingAggregateService.cs`

Baut/pflegt `PostingAggregates`: `UpsertForPostingAsync` (überspringt `Amount == 0`, `ValutaDate == DateTime.MinValue` → `SetValutaDate(BookingDate)`), `RebuildForUserAsync` (set-basiertes Löschen + Neuverdichtung aus allen Postings des Owners). **Relevant für Parität:** Weder `IsPreliminary` noch Storno-Felder werden ausgewertet — alle Buchungen (inkl. Stornos mit negativem `Amount` und vorläufigen Buchungen) fließen in die Aggregate ein; ein Rohposting-Textfilterpfad liefert also mit denselben Regeln (alle Postings, `Amount`, Periode nach Buchungs-/Valutadatum) parallele Summen.

## `BudgetRulePatternMatcher`
Datei: `FinanceManager.Shared/Dtos/Budget/BudgetRulePatternMatcher.cs`

Statische Hilfsklasse. `MatchesPosting(subject, description, pattern, useRegex, regexTimeout = 1s)`: leeres Pattern → `true`; kombinierter Text `subject + " " + description`; Regex-Modus `IgnoreCase | CultureInvariant` mit `RegexMatchTimeoutException`/`ArgumentException` → `false`; sonst `IndexOf(OrdinalIgnoreCase) >= 0`.

Aufrufer: `Budgetbericht` (Domain, Z. 812/832 — gegen `Purpose`/`Description`), `BudgetImpactEvaluationService` (Z. 262/290 — gegen `Subject`/`Description` bzw. `BookingDescription`), `FmBudgetRulesSuggestTool` (Z. 287).

## `ReportDashboardViewModel`
Datei: `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `BuildFiltersPayload()` | public | Baut `ReportAggregatesFiltersRequest` aus `Selected*`-Sets; `null` bei leeren Filtern; bei `IncludeCategory` nur Kategorien + Accounts + Subtypes |
| `ReloadAsync(analysisDate, ct)` | public | Synct VM-Flags → `LoadAsync` → `Reports_QueryAggregatesAsync` |
| `LoadAsync(...)` | public | Baut `ReportAggregatesQueryRequest` |
| `SaveFavoriteAsync` / `UpdateFavoriteAsync` / `DeleteFavoriteAsync` | public | Mappt `ReportAggregatesFiltersRequest` → `ReportFavoriteFiltersDto` (Z. 573–583 / 625–635) in `*ApiRequest` |
| `SubmitFavoriteDialogAsync(...)` | public | Create-/Update-Dialog-Fluss |
| `OpenFilterDialog` / `CloseFilterDialog` | public | Kopiert `Selected*` → `Temp*`, lädt `FilterOptionsByKind`, setzt `ActiveFilterTabKind` |
| `ApplyTempAndReloadAsync` | public | Kopiert `Temp*` → `Selected*`, schließt Dialog, `ReloadAsync` |
| `ClearTempFilters` / `ClearFilters` | public | Leert Temp- bzw. Selected-Sets (inkl. `IncludeDividendRelated = false`) |
| `GetSelectedTempFiltersCount` / `GetSelectedFiltersCount` | public | Filterzähler für Ribbon-/Dialog-Buttons (Multi- vs. Single-Kind-Logik) |
| `IsOptionSelectedTemp` / `ToggleTempForKind` / `IsSecuritySubTypeSelectedTemp` / `ToggleTempSecuritySubType` | public | Temp-Selektion je Kind/Subtype |
| `LoadFilterOptionsAsync` | public | Lädt Optionslisten je `SelectedKinds` (Entitäten oder Kategorien je `IncludeCategory`) |
| `GetRibbonRegisterDefinition` | protected override | Ribbon-Gruppen Navigation/Aktionen/Filter |
| div. Anzeigehelfer (`GetTopLevelRows`, `GetChildRows`, `HasChildren`, `IsNegative`, `GetTotals`, `GetChartByPeriod`, `ToggleExpanded`) | public | Tabellen-/Chart-Logik |

Zustand: `Selected*`/`Temp*`-`HashSet<Guid>` je Entität/Kategorie + `HashSet<int>` (Subtypes), Flags `IncludeDividendRelated`, `UseValutaDate`, `SavingsPlanPostingSource`, `ShowFilterDialog`, `ActiveFilterTabKind`, `FilterOptionsByKind`, `SimpleOption`. **Keine Textfilter-Sammlungen vorhanden.**

## `KpiLocalStorageCache` (`IKpiLocalStorageCache`)
Datei: `FinanceManager.Web/Services/KpiLocalStorageCache.cs`

LocalStorage-Cache über `IJSRuntime`, Schlüsselpräfix `fm.kpi.` (+ `{userId}.` bei gesetztem `UserId`), `KpiLocalStorageContext(Enabled, UserId)`; Methoden `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`, `RemoveAllAsync` (Prefix-Löschung via JS-Funktion `financeManager.localStorage.removeByPrefix`). Registrierung scoped in `ProgramExtensions.cs:255`; `SetContext` wird u. a. von `Home.razor`/`SetupProfileViewModel` genutzt. **Keine zeit- oder inhaltsbasierte Invalidierung** — Einträge bleiben, bis sie überschrieben oder gelöscht werden.

## `HomeKpiGrid` (Code-Behind)
Datei: `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` (`@code`-Block)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `OnAfterRenderAsync` | Einmal-Ladung: Cache lesen (`home.kpi.list`), `RestoreFavoriteCacheAsync`, dann `LoadKpisAsync` |
| `LoadKpisAsync` | `Api.HomeKpis_ListAsync`, schreibt `home.kpi.list` |
| `RestoreFavoriteCacheAsync` | Liest `home.kpi.favorite-{k.Id}` je ReportFavorite-KPI in `_favData` |
| `LoadFavoriteKpiDataAsync(HomeKpiDto)` | Lädt Favoriten via `Reports_GetFavoriteAsync`, baut `ReportAggregatesQueryRequest` **komplett aus `fav.Filters`** (Z. 593–609), aggregiert Serie, schreibt `_favData[k.Id]` und Cache-Eintrag `home.kpi.favorite-{k.Id}` |
| `RenderKpiTile` | Rendert Kacheln (`ReportKpiTile` für Favoriten, `AggregateBarChart`/`NumericKpi`/`MonthlyBudgetKpi` für Predefined) |
| `AddKpiAsync`/`DeleteKpiAsync`/`MoveKpiAsync`/`OnTitleChangedAsync` | KPI-Verwaltung |

Cache-Verhalten: `FavKpiData` enthält keinen Favoriten-Versions-/Filter-Bezug; nach einer Filteränderung am `ReportFavorite` würde der Cache unverändert Alt-Daten liefern (der frische API-Call in `LoadFavoriteKpiDataAsync` läuft zwar immer, wird aber erst nach dem initialen Cache-Restore ausgeführt — `RestoreFavoriteCacheAsync` befüllt `_favData` synchron aus dem Cache, sodass zwischenzeitlich veraltete Werte sichtbar sind).

## `BackupService`
Datei: `FinanceManager.Infrastructure/Backups/BackupService.cs`

- `RequiredDataProperties` (Z. ~28–52): enthält `ReportFavorites` und `HomeKpis` als Pflichtsektionen.
- `BuildBackupDataAsync` (Z. 666–675): exportiert `ReportFavorites`/`HomeKpis` des Users via `ToBackupDto()` — neue `ReportFavorite`-Felder müssen im `ReportFavoriteBackupDto` enthalten sein, um exportiert zu werden.

## `SetupImportService`
Datei: `FinanceManager.Infrastructure/Setup/SetupImportService.cs`

- `ReportFavorites`-Import (Z. 883–897): `TryGetProperty("ReportFavorites")`-Guard, deserialisiert `ReportFavoriteBackupDto`-Liste, `new ReportFavorite(...)` + `AssignBackupDto(dto)`, `reportMap` für `HomeKpi.ReportFavoriteId`-Remapping (Z. 905–906). DTO-Parameter-Defaults halten Alt-Backups ladbar.
